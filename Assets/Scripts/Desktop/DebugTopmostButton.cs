using UnityEngine;

/// <summary>
/// Cambia entre "siempre visible" y "ventana normal".
///
/// Prendido (verde): la franja queda por encima de todas las ventanas.
/// Apagado (gris): las demas apps la tapan, util cuando estas laburando en
/// pantalla completa y no querés la mascota encima.
///
/// El color muestra el estado, no solo el hover, asi se sabe como esta sin
/// tener que apretar.
/// </summary>
public class DebugTopmostButton : DebugIconButton
{
    [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f, 0.9f);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color hoverTint = new Color(1f, 1f, 1f, 1f);

    /// <summary>
    /// El tooltip cuenta el estado actual, igual que el color. Un texto fijo
    /// ("siempre visible") no diria si esta prendido o apagado.
    /// </summary>
    protected override string TooltipText
    {
        get
        {
            DesktopWindow window = DesktopWindow.Instance;
            bool on = window == null || window.AlwaysOnTop;

            return on ? "Siempre visible" : "Detrás de las ventanas";
        }
    }

    protected override Color GetColor(bool hovering)
    {
        DesktopWindow window = DesktopWindow.Instance;
        bool on = window == null || window.AlwaysOnTop;

        Color baseColor = on ? onColor : offColor;
        return hovering ? Color.Lerp(baseColor, hoverTint, 0.5f) : baseColor;
    }

    protected override void OnClicked()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null) return;

        window.ToggleAlwaysOnTop();
        Debug.Log($"[DebugTopmostButton] Siempre visible: {window.AlwaysOnTop}");
    }
}
