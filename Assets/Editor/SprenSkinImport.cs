using UnityEditor;

/// <summary>
/// Ajustes de importacion para el skinning por mapa UV de los spren.
///
/// Se fuerzan en cada import porque si alguno queda mal no hay error: el spren
/// simplemente se dibuja con los colores corridos y cuesta mucho darse cuenta.
///
///   - Templates (Assets/Characters/UVSkin/Templates): cada pixel guarda una
///     coordenada, no un color. Sin sRGB (si no Unity "corrige" los valores en
///     Linear), sin compresion y sin mipmaps, para que lleguen exactos al shader.
///
///   - Skins (Assets/Characters/UVSkin/Skins): son colores normales, pero el
///     shader las lee pixel por pixel. Lo critico es npotScale en None: una skin
///     de 24x24 escalada a 32x32 rompe todas las coordenadas.
/// </summary>
public class SprenSkinImport : AssetPostprocessor
{
    public const string Root = "Assets/Characters/UVSkin";
    public const string TemplatesFolder = Root + "/Templates";
    public const string SkinsFolder = Root + "/Skins";

    private void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;

        if (assetPath.StartsWith(TemplatesFolder + "/"))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.sRGBTexture = false;
            ApplyPixelExact(importer);
        }
        else if (assetPath.StartsWith(SkinsFolder + "/"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ApplyPixelExact(importer);
        }
    }

    private static void ApplyPixelExact(TextureImporter importer)
    {
        importer.filterMode = UnityEngine.FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = false;
        importer.isReadable = true;
    }
}
