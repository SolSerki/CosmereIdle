using UnityEngine;

/// <summary>
/// Entrada "Mover y escalar" del menu de la franja: prende el LayoutEditor.
/// En el panel de seleccion no tiene sentido (ahi la ventana es otra) y se apaga.
/// </summary>
public class LayoutEditButton : DebugIconButton
{
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1f, 1f);

    protected override bool IsAvailable
    {
        get
        {
            DesktopWindow window = DesktopWindow.Instance;
            return LayoutEditor.Instance != null
                   && window != null
                   && window.Mode == DesktopWindow.WindowMode.Strip;
        }
    }

    protected override Color GetColor(bool hovering) => hovering ? hoverColor : idleColor;

    protected override void OnClicked() => LayoutEditor.Instance?.Begin();
}
