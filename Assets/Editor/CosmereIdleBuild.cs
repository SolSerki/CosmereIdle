using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// Build reproducible, para que no dependa de que cada uno se acuerde de dejar
/// bien la ventana de Build Profiles.
///
/// Arregla dos cosas que ya nos pasaron:
///
///   - La lista de escenas se pasa a mano y no se lee del proyecto. La del
///     proyecto se ensucio con Menu.unity y a partir de ahi el build salia
///     distinto segun quien lo hiciera.
///
///   - Antes de buildear revisa los ajustes de los que depende la ventana
///     transparente. Si alguno esta mal el juego compila igual, pero se ve con
///     el fondo negro, y no hay forma de enterarse hasta correr el .exe.
///
/// Queda un ajuste que NO se puede revisar desde aca porque vive en la escena y
/// no en Player Settings: la Main Camera tiene que estar en Solid Color con
/// alpha 0 y con HDR apagado.
///
/// Despues del build compila el instalador con Inno Setup, porque un build sin
/// instalador no se puede publicar y el paso suelto ya se olvido una vez.
/// </summary>
public static class CosmereIdleBuild
{
    private const string Scene = "Assets/Scenes/Main.unity";
    private const string OutputFolder = "Build";
    private const BuildTarget Target = BuildTarget.StandaloneWindows64;

    /// <summary>Lo genera el build para que el instalador no repita el numero de version.</summary>
    private const string VersionInclude = "Installer/Version.iss";

    private const string InstallerScript = "Installer/CosmereIdle.iss";
    private const string InstallerOutput = "Installer/Output";

    /// <summary>Si ISCC tarda mas que esto, algo se colgo.</summary>
    private const int InstallerTimeoutMs = 10 * 60 * 1000;

    // ------------------------------------------------------------------
    // Menu
    // ------------------------------------------------------------------

    [MenuItem("Tools/CosmereIdle/Buildear", false, 1)]
    public static void BuildFromMenu()
    {
        List<string> problems = CheckWindowSettings();

        if (problems.Count > 0)
        {
            string detail = " - " + string.Join("\n - ", problems);

            Debug.LogError("[Build] Cancelado, hay ajustes que rompen la ventana transparente:\n" + detail);

            EditorUtility.DisplayDialog("No se puede buildear",
                "Estos ajustes van a hacer que el juego se vea con el fondo negro:\n\n" + detail +
                "\n\nPodés arreglarlos con Tools > CosmereIdle > Arreglar ajustes de ventana.",
                "Entendido");
            return;
        }

        BuildReport report = BuildNow();
        if (report == null) return;

        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("El build falló",
                $"Resultado: {summary.result}\nErrores: {summary.totalErrors}\n\nMirá la consola.", "Cerrar");
            return;
        }

        InstallerFromMenu(afterBuild: true);
    }

    /// <summary>
    /// Solo el instalador, para cuando el build ya esta hecho y no hace falta
    /// esperar a que Unity lo repita.
    /// </summary>
    [MenuItem("Tools/CosmereIdle/Compilar solo el instalador", false, 4)]
    public static void InstallerOnlyFromMenu() => InstallerFromMenu(afterBuild: false);

    private static void InstallerFromMenu(bool afterBuild)
    {
        string setup = BuildInstaller(out string error);

        if (setup != null)
        {
            EditorUtility.RevealInFinder(setup);
            return;
        }

        EditorUtility.DisplayDialog("No se pudo armar el instalador",
            (afterBuild ? "El build salió bien, pero el instalador no.\n\n" : "") + error, "Cerrar");
    }

    [MenuItem("Tools/CosmereIdle/Revisar ajustes de ventana", false, 2)]
    public static void CheckFromMenu()
    {
        List<string> problems = CheckWindowSettings();

        EditorUtility.DisplayDialog("Ajustes de ventana",
            problems.Count == 0
                ? "Todo en orden. La ventana transparente va a funcionar."
                : "Hay que corregir:\n\n - " + string.Join("\n - ", problems),
            "Cerrar");
    }

    /// <summary>
    /// Deja los ajustes como tienen que estar. Es un menu aparte y no algo que
    /// haga el build solo: cambiar Player Settings por atras de quien aprieta
    /// "buildear" es justo el tipo de sorpresa que despues nadie entiende.
    /// </summary>
    [MenuItem("Tools/CosmereIdle/Arreglar ajustes de ventana", false, 3)]
    public static void FixFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Arreglar ajustes de ventana",
                "Voy a tocar Player Settings: Graphics API, swapchain, modo de ventana, " +
                "run in background y color space.\n\n¿Sigo?", "Sí, arreglalos", "Cancelar"))
            return;

        PlayerSettings.SetUseDefaultGraphicsAPIs(Target, false);
        PlayerSettings.SetGraphicsAPIs(Target, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.useFlipModelSwapchain = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.colorSpace = ColorSpace.Gamma;

        AssetDatabase.SaveAssets();
        Debug.Log("[Build] Ajustes de ventana corregidos.");
    }

    // ------------------------------------------------------------------
    // Entrada por linea de comandos
    // ------------------------------------------------------------------

    /// <summary>
    /// Para buildear sin abrir el Editor:
    ///
    ///   Unity.exe -quit -batchmode -projectPath "." -executeMethod CosmereIdleBuild.BuildFromCommandLine
    ///
    /// Ojo: el modo batch aborta si el Editor ya esta abierto sobre el mismo
    /// proyecto, y el codigo de salida igual puede dar 0. Cerra el Editor antes.
    /// </summary>
    public static void BuildFromCommandLine()
    {
        List<string> problems = CheckWindowSettings();

        if (problems.Count > 0)
        {
            Debug.LogError("[Build] Cancelado:\n - " + string.Join("\n - ", problems));
            EditorApplication.Exit(1);
            return;
        }

        BuildReport report = BuildNow();
        bool ok = report != null && report.summary.result == BuildResult.Succeeded
                  && BuildInstaller(out _) != null;

        EditorApplication.Exit(ok ? 0 : 1);
    }

    // ------------------------------------------------------------------

    /// <summary>Buildea sin preguntar nada. Los dialogos los pone el menu.</summary>
    public static BuildReport BuildNow()
    {
        if (!File.Exists(Scene))
        {
            Debug.LogError($"[Build] No encuentro la escena {Scene}.");
            return null;
        }

        string exe = Path.Combine(OutputFolder, PlayerSettings.productName + ".exe");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            // A mano y no EditorBuildSettings.scenes: la lista del proyecto se
            // ensucia sola y el build tiene que salir siempre igual.
            scenes = new[] { Scene },
            locationPathName = exe,
            target = Target,
            options = BuildOptions.None
        });

        BuildSummary summary = report.summary;

        Debug.Log($"[Build] {summary.result} — {summary.totalErrors} errores, " +
                  $"{summary.totalSize / (1024 * 1024)} MB, {(int)summary.totalTime.TotalSeconds}s\n{exe}");

        if (summary.result == BuildResult.Succeeded) WriteVersionInclude();

        return report;
    }

    /// <summary>
    /// Escribe la version en un .iss que incluye el instalador, asi el numero
    /// vive en un solo lado (Player Settings) y no hay que acordarse de
    /// cambiarlo en dos archivos.
    /// </summary>
    private static void WriteVersionInclude()
    {
        string dir = Path.GetDirectoryName(VersionInclude);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(VersionInclude,
            "; Generado por Tools > CosmereIdle > Buildear. No editar a mano.\n" +
            $"#define AppVersion \"{PlayerSettings.bundleVersion}\"\n" +
            $"#define ExeName \"{PlayerSettings.productName}.exe\"\n");
    }

    // ------------------------------------------------------------------
    // Instalador
    // ------------------------------------------------------------------

    /// <summary>
    /// Compila Installer/CosmereIdle.iss con el build que haya en Build/. Devuelve
    /// la ruta del setup.exe, o null con el motivo en <paramref name="error"/>.
    /// Sin dialogos, igual que BuildNow.
    /// </summary>
    public static string BuildInstaller(out string error)
    {
        string iscc = FindInnoCompiler();
        if (iscc == null)
        {
            error = "No encuentro Inno Setup. Instalalo (6.3 o más nuevo) desde " +
                    "https://jrsoftware.org/isdl.php y volvé a probar con " +
                    "Tools > CosmereIdle > Compilar solo el instalador.";
            Debug.LogError("[Instalador] " + error);
            return null;
        }

        string script = Path.GetFullPath(InstallerScript);
        DateTime started = DateTime.Now;
        var log = new StringBuilder();

        var start = new ProcessStartInfo(iscc, $"/Q \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(script)
        };

        int exitCode;
        try
        {
            using (Process process = Process.Start(start))
            {
                // Lectura asincronica: si ISCC llena el buffer de salida y nadie
                // lo lee, se queda esperando y WaitForExit no vuelve nunca.
                process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Comprimir ~40 MB con lzma2/max tarda. Sin barra de progreso
                // parece que Unity se colgo.
                while (!process.WaitForExit(250))
                {
                    if ((DateTime.Now - started).TotalMilliseconds > InstallerTimeoutMs)
                    {
                        process.Kill();
                        break;
                    }

                    if (!Application.isBatchMode)
                        EditorUtility.DisplayProgressBar("Instalador",
                            $"Comprimiendo con Inno Setup... {(int)(DateTime.Now - started).TotalSeconds}s", 0.5f);
                }

                process.WaitForExit(); // vacia los eventos de salida pendientes
                exitCode = process.ExitCode;
            }
        }
        catch (Exception e)
        {
            error = $"No pude ejecutar Inno Setup: {e.Message}";
            Debug.LogError("[Instalador] " + error);
            return null;
        }
        finally
        {
            if (!Application.isBatchMode) EditorUtility.ClearProgressBar();
        }

        if (exitCode != 0)
        {
            error = $"Inno Setup terminó con código {exitCode}:\n{log.ToString().Trim()}";
            Debug.LogError("[Instalador] " + error);
            return null;
        }

        // El nombre lo arma el .iss (AppName-AppVersion-setup). En vez de repetir
        // esa regla aca, se toma el que acaba de escribir.
        string setup = NewestSetupSince(started);
        if (setup == null)
        {
            error = $"Inno Setup dijo que terminó bien, pero no aparece ningún setup nuevo en {InstallerOutput}.";
            Debug.LogError("[Instalador] " + error);
            return null;
        }

        error = null;
        Debug.Log($"[Instalador] Listo: {setup} ({new FileInfo(setup).Length / (1024 * 1024)} MB, " +
                  $"{(int)(DateTime.Now - started).TotalSeconds}s)");
        return setup;
    }

    /// <summary>
    /// Inno Setup se puede instalar para todos (Program Files) o solo para el
    /// usuario (AppData\Local\Programs), segun lo que se eligio al instalarlo.
    /// </summary>
    private static string FindInnoCompiler()
    {
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

            // "Inno Setup 6", y el que venga despues.
            string[] installs = Directory.GetDirectories(root, "Inno Setup*");
            Array.Sort(installs);
            Array.Reverse(installs);

            foreach (string dir in installs)
            {
                string iscc = Path.Combine(dir, "ISCC.exe");
                if (File.Exists(iscc)) return iscc;
            }
        }

        return null;
    }

    private static string NewestSetupSince(DateTime since)
    {
        if (!Directory.Exists(InstallerOutput)) return null;

        string newest = null;
        DateTime newestTime = since.AddSeconds(-2); // margen por la resolucion del reloj del disco

        foreach (string file in Directory.GetFiles(InstallerOutput, "*-setup.exe"))
        {
            DateTime written = File.GetLastWriteTime(file);
            if (written < newestTime) continue;

            newest = file;
            newestTime = written;
        }

        return newest != null ? Path.GetFullPath(newest) : null;
    }

    /// <summary>Que hay mal en los ajustes de los que depende la ventana. Vacia = todo bien.</summary>
    public static List<string> CheckWindowSettings()
    {
        var problems = new List<string>();

        if (PlayerSettings.GetUseDefaultGraphicsAPIs(Target))
        {
            problems.Add("Auto Graphics API está prendido. Tiene que estar apagado, o Unity " +
                         "elige D3D12 y D3D12 nunca le pasa el alpha a Windows.");
        }
        else
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(Target);

            if (apis.Length == 0 || apis[0] != GraphicsDeviceType.Direct3D11)
                problems.Add("La primera Graphics API tiene que ser Direct3D11.");
        }

        if (PlayerSettings.useFlipModelSwapchain)
            problems.Add("Use DXGI Flip Model Swapchain está prendido. Tiene que estar apagado.");

        if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)
            problems.Add($"Fullscreen Mode está en {PlayerSettings.fullScreenMode}. Tiene que ser Windowed.");

        if (!PlayerSettings.runInBackground)
            problems.Add("Run In Background está apagado. Sin eso la mascota se congela al perder el foco.");

        if (!PlayerSettings.visibleInBackground)
            problems.Add("Visible In Background está apagado.");

        if (PlayerSettings.colorSpace != ColorSpace.Gamma)
            problems.Add($"Color Space está en {PlayerSettings.colorSpace}. Tiene que ser Gamma.");

        return problems;
    }
}
