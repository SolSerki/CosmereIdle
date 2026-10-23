using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana para preparar un spren con skinning por mapa UV.
///
///   1. Template inicial: convierte un frame del spritesheet en la pose de
///      reposo y deja listo el template para que Kato dibuje el resto.
///   2. Bakear skin: con el template terminado, saca la skin del spren desde su
///      spritesheet original y muestra frame por frame si se reproduce igual.
///
/// La logica esta en SprenSkinBaker.
/// </summary>
public class SprenSkinWindow : EditorWindow
{
    private const int PreviewScale = 6;

    private Texture2D original;
    private Texture2D template;
    private int referenceIndex;

    private SprenSkinBaker.BakeReport report;
    private int selectedFrame = -1;
    private Texture2D[] previews;
    private Vector2 scroll;

    [MenuItem("Tools/CosmereIdle/Spren UV Skin", false, 40)]
    private static void Open()
    {
        GetWindow<SprenSkinWindow>("Spren UV Skin");
    }

    private void OnDisable()
    {
        ClearPreviews();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUI.BeginChangeCheck();
        original = (Texture2D)EditorGUILayout.ObjectField("Spritesheet del spren", original, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck() && original != null)
        {
            template = AssetDatabase.LoadAssetAtPath<Texture2D>(SprenSkinBaker.TemplatePathFor(original)) ?? template;
            report = null;
            ClearPreviews();
        }

        if (original == null)
        {
            EditorGUILayout.HelpBox("Elegi el spritesheet original del spren (por ejemplo Bocaspren_Spritesheet).", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        DrawTemplateSection();
        EditorGUILayout.Space();
        DrawBakeSection();
        if (report != null)
        {
            EditorGUILayout.Space();
            DrawReport();
        }

        EditorGUILayout.EndScrollView();
    }

    // ---------------------------------------------------------------- 1. Template

    private void DrawTemplateSection()
    {
        EditorGUILayout.LabelField("1. Template inicial", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Convierte un frame en la pose de reposo (cada pixel pasa a guardar su coordenada). " +
            "Los frames que son copia de ese se completan solos; los demas quedan vacios para dibujar " +
            "en Aseprite moviendo pixeles de la pose de reposo. Tambien genera una paleta .gpl para modo indexado.",
            MessageType.None);

        referenceIndex = Mathf.Max(0, EditorGUILayout.IntField("Frame de referencia", referenceIndex));

        if (GUILayout.Button("Generar template"))
            GenerateTemplate();
    }

    private void GenerateTemplate()
    {
        string path = SprenSkinBaker.TemplatePathFor(original);
        if (File.Exists(path) && !EditorUtility.DisplayDialog(
                "Spren UV Skin",
                $"Ya existe {path}.\n\nSi lo regeneras se pierde todo lo que se haya dibujado en el template.",
                "Regenerar", "Cancelar"))
            return;

        var result = SprenSkinBaker.GenerateTemplate(original, referenceIndex);
        template = AssetDatabase.LoadAssetAtPath<Texture2D>(result.TemplatePath);
        EditorGUIUtility.PingObject(template);
        Debug.Log($"[SprenSkin] Template generado en {result.TemplatePath}. " +
                  $"Completados solos {result.AutoFilledFrames.Count} de {result.FrameCount} frames " +
                  $"({string.Join(", ", result.AutoFilledFrames)}). Paleta: {result.PalettePath}");
    }

    // ---------------------------------------------------------------- 2. Bake

    private void DrawBakeSection()
    {
        EditorGUILayout.LabelField("2. Bakear skin", EditorStyles.boldLabel);
        template = (Texture2D)EditorGUILayout.ObjectField("Template", template, typeof(Texture2D), false);
        EditorGUILayout.LabelField("Skin", SprenSkinBaker.SkinPathFor(original), EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(template == null))
        {
            if (GUILayout.Button("Bakear skin y comparar"))
                Bake();
            if (GUILayout.Button("Crear objeto de prueba en la escena"))
                CreateTestObject();
        }
    }

    private void Bake()
    {
        report = SprenSkinBaker.Bake(template, original);
        ClearPreviews();
        selectedFrame = report.Frames.FindIndex(f => f.Status == SprenSkinBaker.FrameStatus.Different);
        if (selectedFrame < 0)
            selectedFrame = report.Frames.FindIndex(f => f.Status == SprenSkinBaker.FrameStatus.Ok);

        Debug.Log($"[SprenSkin] {SprenSkinBaker.BaseName(original)}: " +
                  $"{report.Count(SprenSkinBaker.FrameStatus.Ok)} frames iguales, " +
                  $"{report.Count(SprenSkinBaker.FrameStatus.Different)} distintos, " +
                  $"{report.Count(SprenSkinBaker.FrameStatus.Pending)} sin dibujar. " +
                  $"Conflictos: {report.Conflicts.Count}. Huecos: {report.Holes.Count}. Skin: {report.SkinPath}");
    }

    private void CreateTestObject()
    {
        string templatePath = AssetDatabase.GetAssetPath(template);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(templatePath).OfType<Sprite>()
            .OrderBy(s => SprenSkinBaker.GetFrames(template).FindIndex(f => f.Name == s.name))
            .ToList();
        var skin = AssetDatabase.LoadAssetAtPath<Texture2D>(SprenSkinBaker.SkinPathFor(original));
        if (sprites.Count == 0 || skin == null)
        {
            EditorUtility.DisplayDialog("Spren UV Skin", "Primero hay que generar el template y bakear la skin.", "OK");
            return;
        }

        var go = new GameObject($"{SprenSkinBaker.BaseName(original)}_UVSkin_Test");
        Undo.RegisterCreatedObjectUndo(go, "Crear spren de prueba");
        var spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprites[Mathf.Clamp(referenceIndex, 0, sprites.Count - 1)];
        spriteRenderer.sharedMaterial = SprenSkinBaker.EnsureMaterial();
        go.AddComponent<SprenSkin>().SetSkin(skin);
        Selection.activeGameObject = go;
    }

    // ---------------------------------------------------------------- Reporte

    private void DrawReport()
    {
        EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"Skin {report.SkinSize.x}x{report.SkinSize.y}{(report.SkinExisted ? " (se conservo lo pintado a mano)" : "")}\n" +
            $"Iguales al original: {report.Count(SprenSkinBaker.FrameStatus.Ok)}   " +
            $"Distintos: {report.Count(SprenSkinBaker.FrameStatus.Different)}   " +
            $"Sin dibujar: {report.Count(SprenSkinBaker.FrameStatus.Pending)}\n" +
            $"Conflictos (una celda con varios colores): {report.Conflicts.Count}{CellList(report.Conflicts)}\n" +
            $"Huecos (celdas usadas sin color): {report.Holes.Count}{CellList(report.Holes)}",
            report.Count(SprenSkinBaker.FrameStatus.Different) > 0 || report.Holes.Count > 0
                ? MessageType.Warning
                : MessageType.Info);

        foreach (var frame in report.Frames)
        {
            string detail = frame.Status switch
            {
                SprenSkinBaker.FrameStatus.Pending => "sin dibujar",
                SprenSkinBaker.FrameStatus.Ok => "igual",
                _ => $"{frame.DifferentPixels} px distintos" + FrameProblems(frame),
            };
            var style = frame.Index == selectedFrame ? EditorStyles.boldLabel : EditorStyles.label;
            if (GUILayout.Button($"{frame.Index,2}  {frame.Name}  —  {detail}", style))
            {
                selectedFrame = frame.Index;
                ClearPreviews();
            }
        }

        if (selectedFrame >= 0 && report.Frames[selectedFrame].Status != SprenSkinBaker.FrameStatus.Pending)
            DrawPreview();
    }

    private static string CellList(System.Collections.Generic.List<Vector2Int> cells)
    {
        if (cells.Count == 0)
            return "";
        var shown = cells.Take(12).Select(c => $"({c.x},{c.y})");
        return " → " + string.Join(" ", shown) + (cells.Count > 12 ? " ..." : "");
    }

    private static string FrameProblems(SprenSkinBaker.FrameReport frame)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (frame.MissingInTemplate > 0) parts.Add($"{frame.MissingInTemplate} faltan en el template");
        if (frame.ExtraInTemplate > 0) parts.Add($"{frame.ExtraInTemplate} sobran en el template");
        if (frame.InvalidColors > 0) parts.Add($"{frame.InvalidColors} colores invalidos");
        if (frame.OutsideSkin > 0) parts.Add($"{frame.OutsideSkin} fuera de la skin");
        return parts.Count == 0 ? "" : " (" + string.Join(", ", parts) + ")";
    }

    // ---------------------------------------------------------------- Preview

    private void DrawPreview()
    {
        if (previews == null)
            BuildPreviews();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Frame {selectedFrame}: original / template + skin / diferencias", EditorStyles.miniBoldLabel);
        var size = new Vector2(previews[0].width, previews[0].height) * PreviewScale;
        var row = GUILayoutUtility.GetRect(size.x * 3 + 16, size.y);
        for (int i = 0; i < previews.Length; i++)
        {
            var rect = new Rect(row.x + i * (size.x + 8), row.y, size.x, size.y);
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            GUI.DrawTexture(rect, previews[i], ScaleMode.ScaleToFit, true);
        }
    }

    private void BuildPreviews()
    {
        var frames = SprenSkinBaker.GetFrames(original);
        var rect = frames[selectedFrame].Rect;
        var source = SprenSkinBaker.Image.Load(AssetDatabase.GetAssetPath(original));
        var map = SprenSkinBaker.Image.Load(AssetDatabase.GetAssetPath(template));
        var skin = SprenSkinBaker.Image.Load(report.SkinPath);

        var originalPixels = new Color32[rect.width * rect.height];
        var renderedPixels = new Color32[originalPixels.Length];
        var diffPixels = new Color32[originalPixels.Length];
        for (int y = 0; y < rect.height; y++)
        for (int x = 0; x < rect.width; x++)
        {
            int i = y * rect.width + x;
            var o = source.Get(rect.x + x, rect.y + y);
            var r = SprenSkinBaker.RenderPixel(map.Get(rect.x + x, rect.y + y), skin);
            originalPixels[i] = o;
            renderedPixels[i] = r;

            bool opaqueO = SprenSkinBaker.IsOpaque(o), opaqueR = SprenSkinBaker.IsOpaque(r);
            bool same = opaqueO == opaqueR && (!opaqueO || (o.r == r.r && o.g == r.g && o.b == r.b));
            diffPixels[i] = !same ? new Color32(255, 40, 40, 255)
                : opaqueO ? new Color32(90, 90, 90, 255)
                : default;
        }

        previews = new[]
        {
            MakeTexture(rect, originalPixels),
            MakeTexture(rect, renderedPixels),
            MakeTexture(rect, diffPixels),
        };
    }

    private static Texture2D MakeTexture(RectInt rect, Color32[] pixels)
    {
        var tex = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private void ClearPreviews()
    {
        if (previews == null)
            return;
        foreach (var tex in previews)
            DestroyImmediate(tex);
        previews = null;
    }
}
