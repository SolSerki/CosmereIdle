using UnityEngine;

/// <summary>
/// Cambia entre "siempre visible" y "ventana normal". El color y el texto
/// cuentan el estado actual, asi se sabe como esta sin tener que apretar.
/// </summary>
public class TopmostRow : MenuRow
{
    [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f, 0.9f);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color hoverTint = Color.white;

    private static bool On => DesktopWindow.Instance == null || DesktopWindow.Instance.AlwaysOnTop;

    public override bool ClosesMenuOnClick => false;

    protected override string Text => On ? "Siempre visible" : "Detrás de las ventanas";

    protected override Color IconColor(bool hovering)
    {
        Color baseColor = On ? onColor : offColor;
        return hovering ? Color.Lerp(baseColor, hoverTint, 0.5f) : baseColor;
    }

    public override void OnClicked(Vector3 cursor) => DesktopWindow.Instance?.ToggleAlwaysOnTop();
}
