using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Arma la actualizacion parcial de la version que hay en Build/:
///
///   - files.json: todos los archivos que instala el juego, con su SHA-256.
///   - CosmereIdle-X-patch.exe: un instalador con solo los archivos que
///     cambiaron respecto de CUALQUIERA de las ultimas versiones publicadas.
///
/// Las versiones anteriores se leen de git y no de los releases: cada commit
/// "Version X" tiene su Build/ adentro, y git ya sabe que archivos cambiaron
/// (git hash-object da el mismo id que esta commiteado, incluso con LFS). Asi
/// no hay que bajar nada ni guardar listas aparte.
///
/// Tomar el cambio contra varias versiones y no solo la anterior es lo que
/// permite que alguien que se salteo un par de actualizaciones tambien reciba
/// el parche. En la practica siempre cambian los mismos ~10 archivos de datos,
/// asi que el parche casi no crece. Si cambio el motor (actualizar Unity,
/// sumar un paquete), el parche no conviene y se publica solo el completo.
///
/// Lo usa Publicar version. Sin dialogos.
/// </summary>
public static class CosmereIdlePatch
{
    /// <summary>Contra cuantas versiones publicadas se compara.</summary>
    private const int BaseReleases = 10;

    /// <summary>Si el parche pesa mas que esta fraccion del juego, no vale la pena.</summary>
    private const double MaxPatchFraction = 0.6;

    private const int GitTimeoutMs = 3 * 60 * 1000;

    public const string ManifestName = "files.json";

    public class Result
    {
        /// <summary>files.json. Null solo si fallo todo.</summary>
        public string ManifestPath;

        /// <summary>El parche. Null si esta version no tiene (ver SkippedReason).</summary>
        public string PatchPath;

        /// <summary>Por que no hay parche, para contarlo. Null si hay.</summary>
        public string SkippedReason;

        public int PatchFiles;
        public long PatchBytes;
        public long FullBytes;
    }

    /// <summary>
    /// Genera files.json y, si conviene, el parche. <paramref name="fullInstaller"/>
    /// es el instalador completo de esta misma version, que va nombrado en
    /// files.json. Null con el motivo en <paramref name="error"/> si fallo algo
    /// que impide incluso escribir files.json.
    /// </summary>
    public static Result Build(string git, string version, string fullInstaller, out string error)
    {
        var result = new Result();
        string root = Directory.GetCurrentDirectory();
        string buildDir = Path.Combine(root, CosmereIdleBuild.OutputFolder);

        List<string> files = ShippedFiles(buildDir);
        if (files.Count == 0)
        {
            error = $"No hay archivos en {CosmereIdleBuild.OutputFolder}/.";
            return null;
        }

        // ---- Que cambio respecto de las ultimas versiones ----
        Dictionary<string, string> current = CurrentBlobIds(git, root, files, out error);
        if (current == null) return null;

        List<string> tags = RecentTags(git, root, "v" + version);
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string tag in tags)
        {
            Dictionary<string, string> old = BlobIdsAt(git, root, tag);
            if (old == null) continue;

            foreach (string file in files)
            {
                // Nuevo, o distinto en esa version: va en el parche.
                if (!old.TryGetValue(file, out string id) || id != current[file])
                    changed.Add(file);
            }

            // Estaba en esa version y ya no: un paquete que se saco, por
            // ejemplo. El parche lo borra para que no quede ocupando lugar.
            foreach (string file in old.Keys)
                if (!current.ContainsKey(file) && IsShipped(file)) stale.Add(file);
        }

        // ---- files.json ----
        var manifest = new UpdateManifest
        {
            version = version,
            full = Path.GetFileName(fullInstaller),
            patch = "",
            files = new UpdateManifestFile[files.Count]
        };

        for (int i = 0; i < files.Count; i++)
        {
            string full = Path.Combine(buildDir, files[i]);
            manifest.files[i] = new UpdateManifestFile
            {
                path = files[i],
                sha256 = Sha256(full),
                size = new FileInfo(full).Length,
                inPatch = changed.Contains(files[i])
            };
        }

        result.FullBytes = manifest.files.Sum(f => f.size);
        result.PatchBytes = manifest.files.Where(f => f.inPatch).Sum(f => f.size);
        result.PatchFiles = changed.Count;

        // ---- El parche, si conviene ----
        if (tags.Count == 0)
            result.SkippedReason = "no hay versiones anteriores contra las que comparar";
        else if (changed.Count == 0)
            result.SkippedReason = "no cambió ningún archivo del juego";
        else if (result.PatchBytes > result.FullBytes * MaxPatchFraction)
            result.SkippedReason = $"cambió casi todo ({result.PatchBytes / 1048576.0:0.0} de " +
                                   $"{result.FullBytes / 1048576.0:0.0} MB), probablemente el motor";
        else
        {
            result.PatchPath = CompilePatch(buildDir, manifest, stale, out string patchError);
            if (result.PatchPath == null) result.SkippedReason = "falló el parche: " + patchError;
            else manifest.patch = Path.GetFileName(result.PatchPath);
        }

        if (result.PatchPath == null)
            foreach (UpdateManifestFile f in manifest.files) f.inPatch = false;

        result.ManifestPath = Path.GetFullPath(Path.Combine(CosmereIdleBuild.InstallerOutput, ManifestName));
        File.WriteAllText(result.ManifestPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));

        Debug.Log(result.PatchPath != null
            ? $"[Parche] {result.PatchFiles} archivos, {result.PatchBytes / 1048576.0:0.0} MB sin comprimir " +
              $"(el juego entero: {result.FullBytes / 1048576.0:0.0} MB). Comparado contra: {string.Join(", ", tags)}"
            : $"[Parche] Sin parche: {result.SkippedReason}.");

        error = null;
        return result;
    }

    /// <summary>
    /// Los archivos que instala el instalador completo, relativos a Build/ y
    /// con "/". Tiene que coincidir con los Excludes de [Files] en CosmereIdle.iss.
    /// </summary>
    private static List<string> ShippedFiles(string buildDir)
    {
        if (!Directory.Exists(buildDir)) return new List<string>();

        return Directory.GetFiles(buildDir, "*", SearchOption.AllDirectories)
            .Select(f => f.Substring(buildDir.Length + 1).Replace('\\', '/'))
            .Where(IsShipped)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsShipped(string relativePath) =>
        !relativePath.Contains("_BurstDebugInformation_DoNotShip/") &&
        !relativePath.Contains("_BackUpThisFolder_ButDontShipItWithYourGame/") &&
        !relativePath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------
    // git
    // ------------------------------------------------------------------

    /// <summary>
    /// El id que git le daria a cada archivo del build actual, aunque no este
    /// commiteado. Pasa por los filtros (LFS), asi que es comparable con lo
    /// que hay en los commits.
    /// </summary>
    private static Dictionary<string, string> CurrentBlobIds(string git, string root, List<string> files, out string error)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Las rutas van como argumentos y en tandas: Windows corta la linea de
        // comandos en ~32 mil caracteres. (Por la entrada estandar no se puede:
        // el Mono de Unity cierra el pipe antes de que git termine de leer.)
        const int batch = 100;
        for (int start = 0; start < files.Count; start += batch)
        {
            List<string> chunk = files.Skip(start).Take(batch).ToList();
            string args = "hash-object -- " +
                          string.Join(" ", chunk.Select(f => $"\"{CosmereIdleBuild.OutputFolder}/{f}\""));

            int code = CosmereIdleBuild.RunTool(git, args, root, "Comparando con las versiones anteriores",
                                                GitTimeoutMs, out string output);

            string[] ids = output.Split('\n').Select(l => l.Trim()).Where(l => l.Length == 40).ToArray();
            if (code != 0 || ids.Length != chunk.Count)
            {
                error = $"git hash-object falló ({code}):\n{output}";
                return null;
            }

            for (int i = 0; i < chunk.Count; i++) map[chunk[i]] = ids[i];
        }

        error = null;
        return map;
    }

    /// <summary>Los ultimos tags de version, sin el que se esta por publicar.</summary>
    private static List<string> RecentTags(string git, string root, string excludeTag)
    {
        CosmereIdleBuild.RunTool(git, "tag --list \"v*\" --sort=-v:refname", root,
                                 "Buscando versiones anteriores", GitTimeoutMs, out string output);

        return output.Split('\n')
                     .Select(l => l.Trim())
                     .Where(t => t.Length > 0 && t != excludeTag)
                     .Take(BaseReleases)
                     .ToList();
    }

    /// <summary>Archivo de Build/ (sin el prefijo) -> id de git, en ese tag. Null si no se pudo leer.</summary>
    private static Dictionary<string, string> BlobIdsAt(string git, string root, string tag)
    {
        int code = CosmereIdleBuild.RunTool(git, $"-c core.quotePath=false ls-tree -r {tag} -- {CosmereIdleBuild.OutputFolder}",
                                            root, $"Leyendo {tag}", GitTimeoutMs, out string output);
        if (code != 0)
        {
            Debug.LogWarning($"[Parche] No pude leer {tag}, lo salteo:\n{output}");
            return null;
        }

        string prefix = CosmereIdleBuild.OutputFolder + "/";
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // "<modo> blob <id>\t<ruta>"
        foreach (string line in output.Split('\n'))
        {
            int tab = line.IndexOf('\t');
            if (tab < 0) continue;

            string[] meta = line.Substring(0, tab).Split(' ');
            string path = line.Substring(tab + 1).TrimEnd('\r');
            if (meta.Length < 3 || meta[1] != "blob" || !path.StartsWith(prefix)) continue;

            map[path.Substring(prefix.Length)] = meta[2];
        }

        return map;
    }

    // ------------------------------------------------------------------
    // El instalador del parche
    // ------------------------------------------------------------------

    private static string CompilePatch(string buildDir, UpdateManifest manifest, ICollection<string> stale, out string error)
    {
        string work = Path.Combine(Path.GetTempPath(), "CosmereIdlePatch");
        string staging = Path.Combine(work, "files");
        string check = Path.Combine(work, "PatchCheck.iss");
        string delete = Path.Combine(work, "PatchDelete.iss");

        try
        {
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(staging);

            foreach (UpdateManifestFile f in manifest.files.Where(f => f.inPatch))
            {
                string target = Path.Combine(staging, f.path.Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(buildDir, f.path.Replace('/', '\\')), target);
            }

            File.WriteAllText(check, PatchCheckScript(manifest), new UTF8Encoding(true));

            // Una linea de [InstallDelete] por archivo que ya no existe.
            File.WriteAllText(delete,
                "; Generado por Publicar version. No editar.\r\n" +
                string.Concat(stale.OrderBy(f => f, StringComparer.Ordinal)
                                   .Select(f => $"Type: files; Name: \"{{app}}\\{f.Replace('/', '\\')}\"\r\n")),
                new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }

        if (stale.Count > 0)
            Debug.Log($"[Parche] Borra {stale.Count} archivos que ya no son parte del juego: " +
                      string.Join(", ", stale.OrderBy(f => f).Take(8)) + (stale.Count > 8 ? ", ..." : ""));

        return CosmereIdleBuild.CompileInstaller(
            $"/DPatch=1 \"/DPatchDir={staging}\" \"/DPatchCheck={check}\" \"/DPatchDelete={delete}\"",
            "*-patch.exe", "Armando la actualización parcial", out error);
    }

    /// <summary>
    /// El codigo Pascal que verifica, antes de instalar el parche, que todo lo
    /// que el parche NO trae este instalado exactamente asi. Lo incluye
    /// CosmereIdle.iss con #include PatchCheck.
    /// </summary>
    private static string PatchCheckScript(UpdateManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Generado por Publicar version para " + manifest.version + ". No editar.");
        sb.AppendLine("function PatchBaseMatches(Dir: String): Boolean;");
        sb.AppendLine("begin");
        sb.AppendLine("  Result := True;");

        foreach (UpdateManifestFile f in manifest.files.Where(f => !f.inPatch))
        {
            string path = f.path.Replace('/', '\\').Replace("'", "''");
            sb.AppendLine($"  if not FileMatches(Dir, '{path}', '{f.sha256}') then Result := False;");
        }

        sb.AppendLine("end;");
        return sb.ToString();
    }

    private static string Sha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
