using UnityEngine;

/// <summary>
/// Cambia entre "siempre visible" y "ventana normal". El icono (ojo abierto o
/// tachado), el color y el texto cuentan el estado actual, asi se sabe como
/// esta sin tener que apretar.
/// </summary>
public class TopmostRow : MenuRow
{
    [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f, 0.9f);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color hoverTint = Color.white;

    [Tooltip("Icono con la opcion prendida. Vacio = deja el que tenga la fila.")]
    [SerializeField] private Sprite onIcon;

    [Tooltip("Icono con la opcion apagada. Vacio = deja el que tenga la fila.")]
    [SerializeField] private Sprite offIcon;

    private static bool On => DesktopWindow.Instance == null || DesktopWindow.Instance.AlwaysOnTop;

    public override bool ClosesMenuOnClick => false;

    protected override string Text => On ? "Siempre visible" : "Detrás de las ventanas";

    protected override Color IconColor(bool hovering)
    {
        Color baseColor = On ? onColor : offColor;
        return hovering ? Color.Lerp(baseColor, hoverTint, 0.5f) : baseColor;
    }

    public override void Refresh(bool hovering, Vector3 cursor)
    {
        base.Refresh(hovering, cursor);

        Sprite stateIcon = On ? onIcon : offIcon;
        if (icon != null && stateIcon != null) icon.sprite = stateIcon;
    }

    public override void OnClicked(Vector3 cursor) => DesktopWindow.Instance?.ToggleAlwaysOnTop();
}
