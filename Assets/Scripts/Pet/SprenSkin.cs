using UnityEngine;

/// <summary>
/// Le pone la skin a un spren que se dibuja con el shader CosmereIdle/SprenSkin.
///
/// El SpriteRenderer muestra los sprites del template (el mapa UV) y este
/// componente le pasa al shader la textura de la skin. La skin va por
/// MaterialPropertyBlock, asi todos los spren comparten el mismo material.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SprenSkin : MonoBehaviour
{
    /// <summary>
    /// Cuanto vale un pixel de distancia en los canales R y G del template.
    /// Con 4 entran skins de hasta 64x64. Tiene que coincidir con UV_STEP del shader.
    /// </summary>
    public const int UVStep = 4;

    public const int MaxSkinSize = 256 / UVStep;

    private static readonly int SkinTexId = Shader.PropertyToID("_SkinTex");

    public Texture2D skin;

    private MaterialPropertyBlock block;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    public void SetSkin(Texture2D newSkin)
    {
        skin = newSkin;
        Apply();
    }

    private void Apply()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        block ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(block);
        if (skin != null)
            block.SetTexture(SkinTexId, skin);
        else
            block.Clear();
        spriteRenderer.SetPropertyBlock(block);
    }
}
