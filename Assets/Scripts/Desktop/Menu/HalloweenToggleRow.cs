using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila del menú de la franja para activar o desactivar la decoración y eventos de Halloween
/// (fantasmas y bruja). Guarda la elección en PlayerPrefs mediante <see cref="HalloweenEvent"/>.
/// </summary>
public class HalloweenToggleRow : MenuRow
{
    [SerializeField] private Color onColor = new Color(1f, 0.62f, 0.22f, 0.95f); // Naranja festivo de Halloween
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color hoverTint = Color.white;

    public override bool ClosesMenuOnClick => false;

    protected override string Text => HalloweenEvent.Enabled ? "Halloween: Activado" : "Halloween: Desactivado";

    protected override Color IconColor(bool hovering)
    {
        Color baseColor = HalloweenEvent.Enabled ? onColor : offColor;
        return hovering ? Color.Lerp(baseColor, hoverTint, 0.5f) : baseColor;
    }

    public override void OnClicked(Vector3 cursor)
    {
        HalloweenEvent.Enabled = !HalloweenEvent.Enabled;
    }

    /// <summary>
    /// Asigna las piezas de UI al ser clonada desde otra fila en CornerMenu.
    /// </summary>
    public void Setup(Image rowIcon, TMP_Text rowLabel, Graphic rowHighlight)
    {
        icon = rowIcon;
        label = rowLabel;

        var highlightField = typeof(MenuRow).GetField("highlight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (highlightField != null)
        {
            highlightField.SetValue(this, rowHighlight);
        }

        // Asignar el sprite del fantasmita como icono temático de la fila
        var ghostSprites = HalloweenGhostSpawner.GetGhostSprites();
        if (ghostSprites != null && ghostSprites.Length > 0 && ghostSprites[0] != null && icon != null)
        {
            icon.sprite = ghostSprites[0];
        }
    }
}
