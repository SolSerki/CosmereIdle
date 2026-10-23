using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// La logica del skinning por mapa UV, separada de la ventana (SprenSkinWindow).
///
/// Todo se lee de los .png en disco y no de las texturas importadas, para que
/// los ajustes de importacion no puedan alterar ni un pixel. Las coordenadas son
/// las de Texture2D: (0, 0) es el pixel de abajo a la izquierda.
///
/// El template tiene que tener el mismo tamano y la misma grilla que el
/// spritesheet original: el frame N del template se compara con el frame N del
/// original.
/// </summary>
public static class SprenSkinBaker
{
    public const string MaterialPath = SprenSkinImport.Root + "/SprenSkin.mat";

    /// <summary>Cuanto se puede correr un frame respecto del de referencia y seguir contando como copia.</summary>
    private const int MaxShift = 3;

    // ---------------------------------------------------------------- Imagen

    public class Image
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;

        public Image(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        private Image(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public Color32 Get(int x, int y) => Pixels[y * Width + x];

        public void Set(int x, int y, Color32 c) => Pixels[y * Width + x] = c;

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public static Image Load(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(assetPath));
            var image = new Image(tex.width, tex.height, tex.GetPixels32());
            Object.DestroyImmediate(tex);
            return image;
        }

        public void Save(string assetPath)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(Pixels);
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }

    public static bool IsOpaque(Color32 c) => c.a >= 128;

    private static bool SameColor(Color32 a, Color32 b)
    {
        bool opaqueA = IsOpaque(a), opaqueB = IsOpaque(b);
        if (!opaqueA || !opaqueB)
            return opaqueA == opaqueB;
        return a.r == b.r && a.g == b.g && a.b == b.b;
    }

    // ---------------------------------------------------------------- Codificacion

    public static Color32 Encode(int x, int y)
    {
        return new Color32((byte)(x * SprenSkin.UVStep), (byte)(y * SprenSkin.UVStep), 0, 255);
    }

    /// <summary>
    /// Devuelve false si el color no es una coordenada exacta (por ejemplo, si se
    /// pinto con antialiasing). El shader igual redondea, pero puede caer en otra celda.
    /// </summary>
    public static bool TryDecode(Color32 c, out Vector2Int cell)
    {
        cell = new Vector2Int(
            Mathf.RoundToInt(c.r / (float)SprenSkin.UVStep),
            Mathf.RoundToInt(c.g / (float)SprenSkin.UVStep));
        return c.r % SprenSkin.UVStep == 0 && c.g % SprenSkin.UVStep == 0 && c.b == 0;
    }

    // ---------------------------------------------------------------- Frames

    public class Frame
    {
        public string Name;
        public RectInt Rect;
        public SpriteRect Source;
    }

    /// <summary>Los frames del spritesheet en orden de lectura: de arriba a abajo y de izquierda a derecha.</summary>
    public static List<Frame> GetFrames(Texture2D texture)
    {
        var provider = GetProvider(texture);
        return provider.GetSpriteRects()
            .OrderByDescending(r => r.rect.y)
            .ThenBy(r => r.rect.x)
            .Select(r => new Frame
            {
                Name = r.name,
                Rect = new RectInt((int)r.rect.x, (int)r.rect.y, (int)r.rect.width, (int)r.rect.height),
                Source = r,
            })
            .ToList();
    }

    private static ISpriteEditorDataProvider GetProvider(Object asset)
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset));
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        return provider;
    }

    /// <summary>"Bocaspren_Spritesheet" -> "Bocaspren".</summary>
    public static string BaseName(Object asset)
    {
        return asset.name.Replace("_Spritesheet", "").Trim();
    }

    public static string TemplatePathFor(Texture2D original) =>
        $"{SprenSkinImport.TemplatesFolder}/{BaseName(original)}_Template.png";

    public static string SkinPathFor(Texture2D original) =>
        $"{SprenSkinImport.SkinsFolder}/{BaseName(original)}_Skin.png";

    // ---------------------------------------------------------------- Paso 2: template inicial

    public class TemplateResult
    {
        public string TemplatePath;
        public string PalettePath;
        public List<int> AutoFilledFrames = new List<int>();
        public int FrameCount;
    }

    /// <summary>
    /// Genera el template inicial a partir de un frame del spritesheet original.
    ///
    /// El frame de referencia se convierte en la pose de reposo: cada pixel pasa a
    /// ser su propia coordenada, asi que la skin de ese frame es identica al frame.
    /// Los frames que son copia exacta del de referencia (aunque esten corridos
    /// unos pixeles) se completan solos. El resto quedan vacios para que Kato los
    /// dibuje moviendo pixeles de la pose de reposo.
    ///
    /// Tambien escribe una paleta .gpl con los colores de la pose de reposo, para
    /// usar en Aseprite en modo indexado y que no se pueda pintar un color invalido.
    /// </summary>
    public static TemplateResult GenerateTemplate(Texture2D original, int referenceIndex)
    {
        var frames = GetFrames(original);
        var source = Image.Load(AssetDatabase.GetAssetPath(original));
        var reference = frames[referenceIndex].Rect;

        if (reference.width > SprenSkin.MaxSkinSize || reference.height > SprenSkin.MaxSkinSize)
            throw new System.InvalidOperationException(
                $"El frame mide {reference.width}x{reference.height} y la skin admite hasta {SprenSkin.MaxSkinSize}x{SprenSkin.MaxSkinSize}.");

        var result = new TemplateResult
        {
            TemplatePath = TemplatePathFor(original),
            PalettePath = Path.ChangeExtension(TemplatePathFor(original), ".gpl"),
            FrameCount = frames.Count,
        };

        var template = new Image(source.Width, source.Height);
        for (int i = 0; i < frames.Count; i++)
        {
            if (!TryFindShift(source, reference, frames[i].Rect, out var shift))
                continue;

            for (int y = 0; y < reference.height; y++)
            for (int x = 0; x < reference.width; x++)
            {
                if (!IsOpaque(source.Get(reference.x + x, reference.y + y)))
                    continue;
                template.Set(frames[i].Rect.x + x + shift.x, frames[i].Rect.y + y + shift.y, Encode(x, y));
            }
            result.AutoFilledFrames.Add(i);
        }

        Directory.CreateDirectory(SprenSkinImport.TemplatesFolder);
        template.Save(result.TemplatePath);
        WritePalette(result.PalettePath, BaseName(original), source, reference);

        AssetDatabase.ImportAsset(result.TemplatePath, ImportAssetOptions.ForceUpdate);
        CopySlicing(original, AssetDatabase.LoadAssetAtPath<Texture2D>(result.TemplatePath), frames);
        AssetDatabase.ImportAsset(result.PalettePath);
        EnsureMaterial();
        return result;
    }

    /// <summary>Busca si el frame es el de referencia corrido como mucho MaxShift pixeles.</summary>
    private static bool TryFindShift(Image source, RectInt reference, RectInt frame, out Vector2Int shift)
    {
        for (int dy = -MaxShift; dy <= MaxShift; dy++)
        for (int dx = -MaxShift; dx <= MaxShift; dx++)
        {
            if (MatchesShifted(source, reference, frame, dx, dy))
            {
                shift = new Vector2Int(dx, dy);
                return true;
            }
        }
        shift = default;
        return false;
    }

    private static bool MatchesShifted(Image source, RectInt reference, RectInt frame, int dx, int dy)
    {
        // Ningun pixel de la referencia puede quedar afuera del frame al correrlo.
        for (int y = 0; y < reference.height; y++)
        for (int x = 0; x < reference.width; x++)
        {
            bool inside = x + dx >= 0 && y + dy >= 0 && x + dx < frame.width && y + dy < frame.height;
            if (!inside && IsOpaque(source.Get(reference.x + x, reference.y + y)))
                return false;
        }

        for (int y = 0; y < frame.height; y++)
        for (int x = 0; x < frame.width; x++)
        {
            int rx = x - dx, ry = y - dy;
            var expected = rx >= 0 && ry >= 0 && rx < reference.width && ry < reference.height
                ? source.Get(reference.x + rx, reference.y + ry)
                : default;
            if (!SameColor(expected, source.Get(frame.x + x, frame.y + y)))
                return false;
        }
        return true;
    }

    private static void WritePalette(string path, string name, Image source, RectInt reference)
    {
        var lines = new List<string> { "GIMP Palette", $"Name: {name} UV", "Columns: 16", "#" };
        // De arriba a abajo, como se ve en Aseprite.
        for (int y = reference.height - 1; y >= 0; y--)
        for (int x = 0; x < reference.width; x++)
        {
            if (!IsOpaque(source.Get(reference.x + x, reference.y + y)))
                continue;
            var c = Encode(x, y);
            lines.Add($"{c.r,3} {c.g,3} {c.b,3}\t{x},{y}");
        }
        File.WriteAllLines(path, lines);
    }

    /// <summary>Corta el template con la misma grilla, pivots y bordes que el original.</summary>
    private static void CopySlicing(Texture2D original, Texture2D template, List<Frame> frames)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(template));
        var originalImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(original));
        importer.spritePixelsPerUnit = originalImporter.spritePixelsPerUnit;

        var provider = GetProvider(template);
        string baseName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(template));
        var rects = frames.Select((f, i) => new SpriteRect
        {
            name = $"{baseName}_{i}",
            rect = f.Source.rect,
            alignment = f.Source.alignment,
            pivot = f.Source.pivot,
            border = f.Source.border,
        }).ToArray();

        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    public static Material EnsureMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            return material;

        material = new Material(Shader.Find("CosmereIdle/SprenSkin"));
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    // ---------------------------------------------------------------- Paso 6: bakear la skin

    public enum FrameStatus { Pending, Ok, Different }

    public class FrameReport
    {
        public int Index;
        public string Name;
        public FrameStatus Status;
        public int DifferentPixels;
        /// <summary>El original tiene pixel y el template no.</summary>
        public int MissingInTemplate;
        /// <summary>El template tiene pixel y el original no.</summary>
        public int ExtraInTemplate;
        public int InvalidColors;
        public int OutsideSkin;
    }

    public class BakeReport
    {
        public string SkinPath;
        public Vector2Int SkinSize;
        public bool SkinExisted;
        public List<FrameReport> Frames = new List<FrameReport>();
        /// <summary>Celdas de la skin que en distintos frames piden colores distintos. Gana el mas votado.</summary>
        public List<Vector2Int> Conflicts = new List<Vector2Int>();
        /// <summary>Celdas que el template usa pero que quedaron sin color en la skin.</summary>
        public List<Vector2Int> Holes = new List<Vector2Int>();

        public int Count(FrameStatus status) => Frames.Count(f => f.Status == status);
    }

    /// <summary>
    /// Arma la skin del spren a partir de su spritesheet original y el template.
    ///
    /// Para cada pixel del template, la coordenada que guarda recibe el color que
    /// tiene el original en ese mismo lugar. Como usa todos los frames, tambien
    /// se llenan las partes que no se ven en la pose de reposo.
    ///
    /// Si la skin ya existia, las celdas que no aparecen en ningun frame conservan
    /// lo que tenian: lo que Kato pinte a mano en la skin no se pisa.
    /// </summary>
    public static BakeReport Bake(Texture2D template, Texture2D original)
    {
        var report = new BakeReport { SkinPath = SkinPathFor(original) };
        var frames = GetFrames(original);
        var source = Image.Load(AssetDatabase.GetAssetPath(original));
        var map = Image.Load(AssetDatabase.GetAssetPath(template));

        if (map.Width != source.Width || map.Height != source.Height)
            throw new System.InvalidOperationException(
                $"El template mide {map.Width}x{map.Height} y el original {source.Width}x{source.Height}. Tienen que tener la misma grilla.");

        report.SkinExisted = File.Exists(report.SkinPath);
        var skin = report.SkinExisted
            ? Image.Load(report.SkinPath)
            : new Image(frames[0].Rect.width, frames[0].Rect.height);
        report.SkinSize = new Vector2Int(skin.Width, skin.Height);

        var votes = new Dictionary<Vector2Int, Dictionary<Color32, int>>();
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            var frameReport = new FrameReport { Index = i, Name = frame.Name };
            report.Frames.Add(frameReport);

            if (IsEmpty(map, frame.Rect))
            {
                frameReport.Status = FrameStatus.Pending;
                continue;
            }
            frameReport.Status = FrameStatus.Ok;

            for (int y = frame.Rect.yMin; y < frame.Rect.yMax; y++)
            for (int x = frame.Rect.xMin; x < frame.Rect.xMax; x++)
            {
                var m = map.Get(x, y);
                var o = source.Get(x, y);
                if (!IsOpaque(m))
                {
                    if (IsOpaque(o))
                        frameReport.MissingInTemplate++;
                    continue;
                }
                if (!TryDecode(m, out var cell))
                    frameReport.InvalidColors++;
                if (!skin.Contains(cell.x, cell.y))
                {
                    frameReport.OutsideSkin++;
                    continue;
                }
                if (!IsOpaque(o))
                {
                    frameReport.ExtraInTemplate++;
                    continue;
                }

                if (!votes.TryGetValue(cell, out var colors))
                    votes[cell] = colors = new Dictionary<Color32, int>();
                o.a = 255;
                colors[o] = colors.TryGetValue(o, out int n) ? n + 1 : 1;
            }
        }

        foreach (var pair in votes)
        {
            var best = pair.Value.OrderByDescending(v => v.Value).First().Key;
            skin.Set(pair.Key.x, pair.Key.y, best);
            if (pair.Value.Count > 1)
                report.Conflicts.Add(pair.Key);
        }

        Directory.CreateDirectory(SprenSkinImport.SkinsFolder);
        skin.Save(report.SkinPath);
        AssetDatabase.ImportAsset(report.SkinPath, ImportAssetOptions.ForceUpdate);

        // Diff: se dibuja cada frame con la skin nueva y se compara con el original.
        var holes = new HashSet<Vector2Int>();
        foreach (var frameReport in report.Frames)
        {
            if (frameReport.Status == FrameStatus.Pending)
                continue;
            var rect = frames[frameReport.Index].Rect;
            for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                var m = map.Get(x, y);
                if (IsOpaque(m))
                {
                    TryDecode(m, out var cell);
                    if (skin.Contains(cell.x, cell.y) && !IsOpaque(skin.Get(cell.x, cell.y)))
                        holes.Add(cell);
                }
                if (!SameColor(RenderPixel(m, skin), source.Get(x, y)))
                    frameReport.DifferentPixels++;
            }
            frameReport.Status = frameReport.DifferentPixels == 0 ? FrameStatus.Ok : FrameStatus.Different;
        }
        report.Holes = holes.ToList();
        return report;
    }

    private static bool IsEmpty(Image image, RectInt rect)
    {
        for (int y = rect.yMin; y < rect.yMax; y++)
        for (int x = rect.xMin; x < rect.xMax; x++)
            if (IsOpaque(image.Get(x, y)))
                return false;
        return true;
    }

    /// <summary>Lo mismo que hace el shader, pero en CPU.</summary>
    public static Color32 RenderPixel(Color32 mapPixel, Image skin)
    {
        if (!IsOpaque(mapPixel))
            return default;
        TryDecode(mapPixel, out var cell);
        return skin.Contains(cell.x, cell.y) ? skin.Get(cell.x, cell.y) : default;
    }
}
