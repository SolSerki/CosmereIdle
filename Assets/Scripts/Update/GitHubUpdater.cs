using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

/// <summary>
/// Busca versiones nuevas en los Releases de GitHub y se actualiza solo.
///
///   - Al arrancar: si hay una version nueva la baja y la instala sin preguntar.
///     El instalador cierra el juego, lo pisa y lo vuelve a abrir.
///   - Mientras corre: revisa cada tanto y, si aparece una, solo AVISA. Nadie
///     quiere que la mascota desaparezca de golpe en medio de algo; se instala
///     cuando el usuario aprieta el boton o en el proximo inicio.
///
/// Una version es un Release con tag "v1.2.0" (igual al Version de Player
/// Settings) y el instalador de Inno Setup adjunto ("*-setup.exe"). Ver la
/// seccion "Publicar una version" de CONVENTIONS.md.
///
/// Solo se instala solo si el juego vino del instalador (hay un unins000.exe al
/// lado). Un build suelto o un zip avisa igual, pero el boton abre la pagina del
/// release en vez de instalar: si no, un build de desarrollo se cerraria para
/// instalar otra copia en otro lado.
///
/// No tiene UI propia: UpdateButton lee el estado y lo muestra.
/// </summary>
[DisallowMultipleComponent]
public class GitHubUpdater : MonoBehaviour
{
    public enum UpdateState
    {
        /// <summary>No hay nada nuevo, o todavia no se reviso.</summary>
        UpToDate,

        /// <summary>Hay una version nueva esperando a que alguien la instale.</summary>
        Available,

        /// <summary>Bajando el instalador.</summary>
        Downloading,

        /// <summary>El instalador ya arranco y el juego se esta cerrando.</summary>
        Installing
    }

    // Forma de la respuesta de /releases/latest. Solo los campos que usamos.
    [Serializable]
    private class ReleaseJson
    {
        public string tag_name;
        public string html_url;
        public AssetJson[] assets;
    }

    [Serializable]
    private class AssetJson
    {
        public string name;
        public string browser_download_url;
        public long size;
        public string digest; // "sha256:<hex>"
    }

    public static GitHubUpdater Instance { get; private set; }

    [Header("Repositorio")]
    [SerializeField] private string owner = "SolSerki";
    [SerializeField] private string repository = "CosmereIdle";

    [Tooltip("Como termina el nombre del instalador adjunto al release.")]
    [SerializeField] private string installerSuffix = "-setup.exe";

    [Header("Cuando revisar")]
    [Tooltip("Espera antes de la primera revision, para no competir con el arranque.")]
    [SerializeField] private float firstCheckDelay = 3f;

    [Tooltip("Cada cuanto se vuelve a revisar mientras el juego esta abierto. " +
             "GitHub deja 60 consultas por hora por IP sin autenticar: no bajar de minutos.")]
    [SerializeField] private float checkIntervalHours = 6f;

    [Tooltip("Instalar sin preguntar si la version nueva aparece al arrancar.")]
    [SerializeField] private bool installOnStartup = true;

    [Tooltip("Revisar tambien en el Editor (solo avisa, nunca instala).")]
    [SerializeField] private bool checkInEditor = false;

    /// <summary>Recuerda que version ya se intento instalar sola. Ver StartupInstallAllowed.</summary>
    private const string TriedKey = "Updater.AutoInstallTried";

    private const string InstallerArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";

    public UpdateState State { get; private set; } = UpdateState.UpToDate;

    /// <summary>La version nueva, sin la "v" del tag. Null si no hay.</summary>
    public string LatestVersion { get; private set; }

    /// <summary>De 0 a 1 mientras State es Downloading.</summary>
    public float DownloadProgress { get; private set; }

    /// <summary>Que salio mal en el ultimo intento de instalar. Null si nada.</summary>
    public string LastError { get; private set; }

    /// <summary>
    /// True si la instalacion en curso es la automatica del arranque y no la
    /// pidio el usuario. Sirve para que la UI diga "me actualizo" y no "hay una
    /// version nueva".
    /// </summary>
    public bool IsStartupInstall { get; private set; }

    /// <summary>Si este juego vino del instalador y puede actualizarse a si mismo.</summary>
    public bool CanSelfInstall { get; private set; }

    private ReleaseJson latest;
    private AssetJson latestInstaller;

    private void Awake()
    {
        Instance = this;
        CanSelfInstall = DetectInstalledCopy();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (!checkInEditor) return;
#endif
        DeleteOldInstallers();
        StartCoroutine(CheckLoop());
    }

    // ------------------------------------------------------------------
    // API para la UI
    // ------------------------------------------------------------------

    /// <summary>
    /// Instala la version nueva ahora. Si el juego no vino del instalador, abre
    /// la pagina del release para bajarla a mano.
    /// </summary>
    public void InstallNow()
    {
        if (State != UpdateState.Available) return;

        if (!CanSelfInstall)
        {
            if (latest != null && !string.IsNullOrEmpty(latest.html_url))
                Application.OpenURL(latest.html_url);
            return;
        }

        IsStartupInstall = false;
        StartCoroutine(DownloadAndInstall());
    }

    // ------------------------------------------------------------------
    // Revision
    // ------------------------------------------------------------------

    private IEnumerator CheckLoop()
    {
        yield return new WaitForSecondsRealtime(firstCheckDelay);

        bool startup = true;

        while (true)
        {
            // Mientras se baja o instala no tiene sentido volver a preguntar.
            if (State == UpdateState.UpToDate || State == UpdateState.Available)
                yield return CheckLatest();

            if (startup && State == UpdateState.Available && installOnStartup && StartupInstallAllowed())
            {
                IsStartupInstall = true;
                yield return DownloadAndInstall();
            }

            startup = false;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, checkIntervalHours) * 3600f);
        }
    }

    private IEnumerator CheckLatest()
    {
        string url = $"https://api.github.com/repos/{owner}/{repository}/releases/latest";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // GitHub rechaza pedidos sin User-Agent.
            request.SetRequestHeader("User-Agent", $"{Application.productName}/{Application.version}");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.timeout = 20;

            yield return request.SendWebRequest();

            // 404 = el repo todavia no tiene releases. Sin internet, rate limit,
            // etc: no es asunto del jugador, se reintenta en la proxima vuelta.
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode != 404)
                    Debug.Log($"[GitHubUpdater] No pude revisar: {request.responseCode} {request.error}");
                yield break;
            }

            ReleaseJson release;
            try { release = JsonUtility.FromJson<ReleaseJson>(request.downloadHandler.text); }
            catch (Exception e)
            {
                Debug.LogWarning($"[GitHubUpdater] Respuesta ilegible: {e.Message}");
                yield break;
            }

            if (release == null || !TryParseVersion(release.tag_name, out Version remote)) yield break;
            if (!TryParseVersion(Application.version, out Version local))
            {
                Debug.LogWarning($"[GitHubUpdater] La version local '{Application.version}' no es un numero de version.");
                yield break;
            }

            if (remote <= local) yield break;

            AssetJson installer = FindInstaller(release);
            if (installer == null)
            {
                Debug.LogWarning($"[GitHubUpdater] El release {release.tag_name} no tiene un '*{installerSuffix}'.");
                yield break;
            }

            // Se muestra tal cual el tag ("1.2"), no el Version normalizado ("1.2.0.0").
            string shown = release.tag_name.Trim().TrimStart('v', 'V');

            if (LatestVersion != shown)
                Debug.Log($"[GitHubUpdater] Hay version nueva: {Application.version} -> {shown}");

            latest = release;
            latestInstaller = installer;
            LatestVersion = shown;
            State = UpdateState.Available;
        }
    }

    private AssetJson FindInstaller(ReleaseJson release)
    {
        if (release.assets == null) return null;

        foreach (AssetJson asset in release.assets)
        {
            if (asset != null && !string.IsNullOrEmpty(asset.name) &&
                asset.name.EndsWith(installerSuffix, StringComparison.OrdinalIgnoreCase))
                return asset;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Descarga e instalacion
    // ------------------------------------------------------------------

    private IEnumerator DownloadAndInstall()
    {
        AssetJson asset = latestInstaller;
        string version = LatestVersion;
        if (asset == null) yield break;

        State = UpdateState.Downloading;
        DownloadProgress = 0f;
        LastError = null;

        string path = Path.Combine(Application.temporaryCachePath, asset.name);

        using (UnityWebRequest request = new UnityWebRequest(asset.browser_download_url, UnityWebRequest.kHttpVerbGET))
        {
            request.downloadHandler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
            request.SetRequestHeader("User-Agent", $"{Application.productName}/{Application.version}");

            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            while (!op.isDone)
            {
                DownloadProgress = request.downloadProgress;
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"No se pudo descargar ({request.error}).");
                yield break;
            }
        }

        DownloadProgress = 1f;

        // Hash en otro hilo: son decenas de MB y en el hilo principal la mascota
        // se congelaria un instante.
        Task<string> check = Task.Run(() => VerifyDownload(path, asset));
        while (!check.IsCompleted) yield return null;

        string problem = check.IsFaulted ? check.Exception?.GetBaseException().Message : check.Result;
        if (problem != null)
        {
            TryDelete(path);
            Fail(problem);
            yield break;
        }

        // Se anota ANTES de lanzar: si el release esta mal armado (tag mas nuevo
        // que el Version del build que trae adentro), el juego reinstalado seguiria
        // viendo una version "nueva" y se reinstalaria en cada arranque para siempre.
        PlayerPrefs.SetString(TriedKey, version);
        PlayerPrefs.Save();

        try
        {
            var start = new ProcessStartInfo(path, InstallerArgs)
            {
                UseShellExecute = true,
                WorkingDirectory = Application.temporaryCachePath
            };
            Process.Start(start);
        }
        catch (Exception e)
        {
            Fail($"No se pudo abrir el instalador ({e.Message}).");
            yield break;
        }

        Debug.Log($"[GitHubUpdater] Instalando {version}, cerrando el juego.");
        State = UpdateState.Installing;

        // Un rato para que la UI alcance a mostrar "instalando". El instalador
        // igual cierra el juego solo (CloseApplications) si tardamos de mas.
        yield return new WaitForSecondsRealtime(1f);
        Application.Quit();
    }

    /// <summary>Null si el archivo esta bien; si no, el motivo.</summary>
    private static string VerifyDownload(string path, AssetJson asset)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return "La descarga no dejo ningun archivo.";
        if (asset.size > 0 && info.Length != asset.size) return "La descarga llego incompleta.";

        // GitHub publica el SHA-256 de cada adjunto. Los releases viejos no lo
        // tienen, y ahi alcanza con que venga por HTTPS desde github.com.
        const string prefix = "sha256:";
        if (string.IsNullOrEmpty(asset.digest) ||
            !asset.digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string expected = asset.digest.Substring(prefix.Length);

        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? null
                : "El instalador descargado no coincide con el publicado.";
        }
    }

    private void Fail(string message)
    {
        Debug.LogWarning($"[GitHubUpdater] {message}");
        LastError = message;
        IsStartupInstall = false;
        State = UpdateState.Available;
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    /// <summary>
    /// La instalacion automatica del arranque se intenta UNA vez por version.
    /// Si despues de instalarla seguimos en una version vieja, el release esta
    /// mal armado y reintentar solo armaria un loop de cerrar y reabrir. En ese
    /// caso queda el aviso, y el boton sigue funcionando a mano.
    /// </summary>
    private bool StartupInstallAllowed()
    {
        if (!CanSelfInstall) return false;
        if (PlayerPrefs.GetString(TriedKey, "") != LatestVersion) return true;

        Debug.LogWarning($"[GitHubUpdater] Ya se instalo {LatestVersion} y seguimos en " +
                         $"{Application.version}. ¿El tag del release coincide con el Version del build?");
        return false;
    }

    /// <summary>
    /// Inno Setup deja el desinstalador al lado del .exe. Si no esta, es un build
    /// suelto o un zip, y no nos corresponde instalar nada.
    /// </summary>
    private static bool DetectInstalledCopy()
    {
#if UNITY_EDITOR || !UNITY_STANDALONE_WIN
        return false;
#else
        string gameFolder = Path.GetDirectoryName(Application.dataPath);
        return gameFolder != null && File.Exists(Path.Combine(gameFolder, "unins000.exe"));
#endif
    }

    /// <summary>Los instaladores de actualizaciones anteriores ya no sirven.</summary>
    private void DeleteOldInstallers()
    {
        try
        {
            foreach (string file in Directory.GetFiles(Application.temporaryCachePath, "*" + installerSuffix))
                TryDelete(file);
        }
        catch (Exception) { /* carpeta de cache: si no se puede limpiar, no importa */ }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception) { }
    }

    /// <summary>
    /// "v1.2" -> 1.2.0.0. Completa con ceros porque System.Version considera
    /// 1.0 menor que 1.0.0, y eso bastaria para "encontrar" una version nueva
    /// que es la misma.
    /// </summary>
    private static bool TryParseVersion(string text, out Version version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string s = text.Trim().TrimStart('v', 'V');

        int suffix = s.IndexOfAny(new[] { '-', '+', ' ' });
        if (suffix >= 0) s = s.Substring(0, suffix);
        if (!s.Contains(".")) s += ".0";

        if (!Version.TryParse(s, out Version parsed)) return false;

        version = new Version(parsed.Major, parsed.Minor,
                              Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }
}
