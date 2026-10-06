using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Publica una version nueva de punta a punta, para que los jugadores la
/// reciban con la actualizacion automatica:
///
///   1. Sube el Version de Player Settings.
///   2. Buildea el juego y el instalador (CosmereIdleBuild).
///   3. Commitea y pushea.
///   4. Crea el Release de GitHub con el tag "v" + Version y el instalador adjunto.
///
/// Antes de tocar nada revisa todo lo que puede hacer fallar un paso a mitad
/// de camino: git y gh instalados, gh logueado, la rama al dia con GitHub, y que
/// el tag no exista. Hacer esto a mano ya salio mal una vez (un instalador
/// viejo, un comando corrido desde otra carpeta, gh sin instalar).
///
/// Necesita la CLI de GitHub: winget install --id GitHub.cli, y gh auth login.
/// </summary>
public class CosmereIdleRelease : EditorWindow
{
    private const int FetchTimeoutMs = 2 * 60 * 1000;

    // El build va al repo y pesa: el push y la subida del instalador pueden
    // tardar bastante con una conexion lenta.
    private const int PushTimeoutMs = 20 * 60 * 1000;
    private const int UploadTimeoutMs = 20 * 60 * 1000;

    private string newVersion = "";
    private string notes = "";
    private bool includePending;
    private List<string> pending = new List<string>();
    private string git;
    private string gh;
    private Vector2 scroll;

    private static string ProjectRoot => Directory.GetCurrentDirectory();

    [MenuItem("Tools/CosmereIdle/Publicar versión...", false, 20)]
    public static void Open()
    {
        var window = GetWindow<CosmereIdleRelease>(true, "Publicar versión");
        window.minSize = new Vector2(480, 420);
        window.Reload();
    }

    private void Reload()
    {
        git = FindGit();
        gh = FindGh();
        newVersion = NextVersion(PlayerSettings.bundleVersion);
        RefreshPending();
    }

    private void OnFocus()
    {
        if (git == null) git = FindGit();
        if (gh == null) gh = FindGh();
        RefreshPending();
    }

    private void RefreshPending()
    {
        pending = git == null ? new List<string>() : PendingChanges();
        if (pending.Count == 0) includePending = false;
    }

    // ------------------------------------------------------------------
    // Ventana
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Versión actual", PlayerSettings.bundleVersion);
        newVersion = EditorGUILayout.TextField("Versión nueva", newVersion).Trim();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Qué cambió (lo ven en la página del release)");
        notes = EditorGUILayout.TextArea(notes, GUILayout.MinHeight(110));

        EditorGUILayout.Space();
        List<string> blockers = Blockers();

        if (pending.Count > 0)
        {
            string list = string.Join("\n", pending.Take(12));
            if (pending.Count > 12) list += $"\n... y {pending.Count - 12} más";

            EditorGUILayout.HelpBox(
                "Hay cambios sin commitear. Van a entrar en el commit de la versión:\n\n" + list,
                MessageType.Warning);
            includePending = EditorGUILayout.ToggleLeft("Sí, incluirlos en esta versión", includePending);
        }

        foreach (string blocker in blockers)
            EditorGUILayout.HelpBox(blocker, MessageType.Error);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Actualizar", GUILayout.Width(90))) Reload();

            GUI.enabled = blockers.Count == 0 && (pending.Count == 0 || includePending);
            if (GUILayout.Button($"Publicar v{newVersion}", GUILayout.Height(28)))
            {
                // Fuera de OnGUI: un build adentro de un evento de la ventana
                // rompe el layout de IMGUI.
                EditorApplication.delayCall += Publish;
                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;
        }
    }

    /// <summary>Lo que se puede saber sin correr nada lento. Vacia = se puede publicar.</summary>
    private List<string> Blockers()
    {
        var list = new List<string>();

        if (git == null)
            list.Add("No encuentro git.");

        if (gh == null)
            list.Add("No encuentro la CLI de GitHub (gh). Instalala con \"winget install --id GitHub.cli\", " +
                     "logueate con \"gh auth login\" y reiniciá Unity.");

        if (!TryParseVersion(newVersion, out Version next))
            list.Add($"\"{newVersion}\" no es un número de versión (tiene que ser como 1.3 o 1.3.1).");
        else if (TryParseVersion(PlayerSettings.bundleVersion, out Version current) && next <= current)
            list.Add($"La versión nueva tiene que ser mayor que {PlayerSettings.bundleVersion}: " +
                     "el juego solo se actualiza si el número sube.");

        if (string.IsNullOrWhiteSpace(notes))
            list.Add("Escribí qué cambió.");

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            list.Add("Salí de Play mode.");

        return list;
    }

    // ------------------------------------------------------------------
    // Publicar
    // ------------------------------------------------------------------

    private void Publish()
    {
        string oldVersion = PlayerSettings.bundleVersion;
        string version = newVersion;
        string tag = "v" + version;

        if (!EditorUtility.DisplayDialog($"Publicar {tag}",
                $"Voy a:\n\n" +
                $"1. Cambiar el Version de {oldVersion} a {version}.\n" +
                "2. Buildear el juego y el instalador.\n" +
                $"3. Commitear{(pending.Count > 0 ? $" (con tus {pending.Count} cambios)" : "")} y pushear.\n" +
                $"4. Crear el release {tag} en GitHub con el instalador.\n\n" +
                "Los jugadores la reciben la próxima vez que abran el juego.",
                "Publicar", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.SaveAssets();

        // ---- 1. Revisar todo antes de tocar nada ----
        string problem = Preflight(tag);
        if (problem != null)
        {
            Fail("No publiqué nada", problem);
            return;
        }

        // ---- 2. Version, build e instalador ----
        PlayerSettings.bundleVersion = version;
        AssetDatabase.SaveAssets();

        BuildReport report = CosmereIdleBuild.BuildNow();
        if (report == null || report.summary.result != BuildResult.Succeeded)
        {
            RevertVersion(oldVersion);
            Fail("No publiqué nada", "El build falló. Mirá la consola.\n\nEl Version volvió a " + oldVersion + ".");
            return;
        }

        string setup = CosmereIdleBuild.BuildInstaller(out string installerError);
        if (setup == null)
        {
            RevertVersion(oldVersion);
            Fail("No publiqué nada", installerError + "\n\nEl Version volvió a " + oldVersion + ".");
            return;
        }

        // ---- 3. Commit y push ----
        if (!Git("add -A", "Preparando el commit", FetchTimeoutMs, out string output) ||
            !Git($"commit -m \"Versión {version}\"", "Commiteando", FetchTimeoutMs, out output))
        {
            RevertVersion(oldVersion);
            Fail("No publiqué nada", "No pude commitear:\n\n" + output + "\n\nEl Version volvió a " + oldVersion + ".");
            return;
        }

        Git("rev-parse HEAD", "Leyendo el commit", FetchTimeoutMs, out string sha);
        sha = sha.Trim();

        string releaseCommand = $"gh release create {tag} \"{RelativeToProject(setup)}\" " +
                                $"--title {tag} --target {sha} --notes \"...\"";

        if (!Git("push", "Subiendo a GitHub", PushTimeoutMs, out output))
        {
            EditorGUIUtility.systemCopyBuffer = releaseCommand;
            Fail("El commit quedó hecho, pero no se subió",
                 "git push falló:\n\n" + output +
                 "\n\nCuando lo resuelvas, hacé git push y después creá el release. Te dejé el comando " +
                 "copiado (cambiá las notas):\n\n" + releaseCommand);
            return;
        }

        // ---- 4. Release ----
        string notesFile = Path.Combine(Path.GetTempPath(), $"cosmereidle-{tag}-notes.md");
        File.WriteAllText(notesFile, notes.Trim() + "\n", new UTF8Encoding(false));

        bool released = Gh($"release create {tag} \"{setup}\" --title {tag} --target {sha} " +
                           $"--notes-file \"{notesFile}\"",
                           "Creando el release y subiendo el instalador", UploadTimeoutMs, out output);
        TryDelete(notesFile);

        if (!released)
        {
            EditorGUIUtility.systemCopyBuffer = releaseCommand;
            Fail("Se subió el código, pero no el release",
                 "gh release create falló:\n\n" + output +
                 "\n\nLos jugadores todavía no lo reciben. Te dejé el comando copiado para crearlo " +
                 "a mano (cambiá las notas):\n\n" + releaseCommand);
            return;
        }

        // Para que el tag que creo GitHub tambien este en el repo local.
        Git("fetch --tags", "Bajando el tag", FetchTimeoutMs, out _);

        string url = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("https://"));
        Debug.Log($"[Publicar] {tag} publicada. {url}");

        notes = "";
        Reload();
        Repaint();

        if (EditorUtility.DisplayDialog($"{tag} publicada",
                "Los jugadores la reciben la próxima vez que abran el juego " +
                "(o en unas horas, si ya lo tienen abierto).",
                url != null ? "Ver el release" : "Cerrar", "Cerrar") && url != null)
            Application.OpenURL(url);
    }

    /// <summary>
    /// Todo lo que puede romper un paso a mitad de camino, revisado antes de
    /// empezar. Null = todo bien; si no, que hacer.
    /// </summary>
    private string Preflight(string tag)
    {
        List<string> window = CosmereIdleBuild.CheckWindowSettings();
        if (window.Count > 0)
            return "Hay ajustes que rompen la ventana transparente:\n - " + string.Join("\n - ", window) +
                   "\n\nArreglalos con Tools > CosmereIdle > Arreglar ajustes de ventana.";

        if (Gh("auth status", "Revisando el login de GitHub", FetchTimeoutMs, out string output) == false)
            return "gh no está logueado. Corré \"gh auth login\" en una terminal.\n\n" + output;

        if (!Git("fetch", "Revisando GitHub", FetchTimeoutMs, out output))
            return "No pude hablar con GitHub (git fetch):\n\n" + output;

        if (!Git("rev-list --count HEAD..@{u}", "Revisando la rama", FetchTimeoutMs, out output))
            return "La rama no sigue a ninguna rama de GitHub, así que no sé adónde pushear:\n\n" + output;

        if (output.Trim() != "0")
            return $"GitHub tiene {output.Trim()} commit(s) que vos no tenés. Hacé pull primero.";

        Git($"ls-remote --tags origin refs/tags/{tag}", "Revisando el tag", FetchTimeoutMs, out output);
        if (!string.IsNullOrWhiteSpace(output))
            return $"El tag {tag} ya existe en GitHub. Elegí otro número de versión.";

        return null;
    }

    private static void RevertVersion(string version)
    {
        PlayerSettings.bundleVersion = version;
        AssetDatabase.SaveAssets();

        // El build ya habia escrito el numero nuevo; que el instalador no quede
        // diciendo una version que no existe.
        CosmereIdleBuild.WriteVersionInclude();
    }

    private static void Fail(string title, string message)
    {
        Debug.LogError($"[Publicar] {title}: {message}");
        EditorUtility.DisplayDialog(title, message, "Cerrar");
    }

    // ------------------------------------------------------------------
    // git y gh
    // ------------------------------------------------------------------

    private bool Git(string args, string what, int timeoutMs, out string output) =>
        CosmereIdleBuild.RunTool(git, args, ProjectRoot, what, timeoutMs, out output) == 0;

    private bool Gh(string args, string what, int timeoutMs, out string output) =>
        CosmereIdleBuild.RunTool(gh, args, ProjectRoot, what, timeoutMs, out output) == 0;

    private List<string> PendingChanges()
    {
        CosmereIdleBuild.RunTool(git, "status --porcelain", ProjectRoot, "Revisando cambios",
                                 FetchTimeoutMs, out string output);

        return output.Split('\n')
                     .Select(l => l.TrimEnd('\r'))
                     .Where(l => l.Length > 3)
                     .Select(l => l.Substring(3).Trim('"'))
                     .ToList();
    }

    private static string FindGit() =>
        FindTool("git.exe", Path.Combine(ProgramFiles, @"Git\cmd\git.exe"));

    /// <summary>
    /// Ademas del PATH, donde lo deja winget: Unity no ve un PATH actualizado
    /// hasta que se reinicia, y gh se suele instalar con Unity abierto.
    /// </summary>
    private static string FindGh() =>
        FindTool("gh.exe",
                 Path.Combine(ProgramFiles, @"GitHub CLI\gh.exe"),
                 Path.Combine(LocalAppData, @"Programs\GitHub CLI\gh.exe"),
                 Path.Combine(LocalAppData, @"Microsoft\WinGet\Links\gh.exe"));

    private static string FindTool(string exe, params string[] knownLocations)
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";

        foreach (string dir in path.Split(Path.PathSeparator))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { /* entrada rota en el PATH */ }
        }

        return knownLocations.FirstOrDefault(File.Exists);
    }

    private static string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    /// <summary>"1.2" -> "1.3", "1.0.9" -> "1.0.10". Si no se puede, la misma.</summary>
    private static string NextVersion(string current)
    {
        string[] parts = (current ?? "").Split('.');
        if (parts.Length == 0 || !int.TryParse(parts[parts.Length - 1], out int last)) return current;

        parts[parts.Length - 1] = (last + 1).ToString();
        return string.Join(".", parts);
    }

    /// <summary>Igual que GitHubUpdater: 1.2 y 1.2.0 son la misma version.</summary>
    private static bool TryParseVersion(string text, out Version version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string s = text.Trim();
        if (!s.Contains(".")) s += ".0";
        if (!Version.TryParse(s, out Version parsed)) return false;

        version = new Version(parsed.Major, parsed.Minor,
                              Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }

    private static string RelativeToProject(string path)
    {
        string root = ProjectRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception) { }
    }
}
