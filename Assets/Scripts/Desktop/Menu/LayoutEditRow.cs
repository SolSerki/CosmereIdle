using UnityEngine;

/// <summary>"Mover y escalar ventana": prende el LayoutEditor para acomodar la franja.</summary>
public class LayoutEditRow : MenuRow
{
    [SerializeField] private string text = "Mover y escalar ventana";
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1f, 1f);

    public override bool IsAvailable => LayoutEditor.Instance != null;

    protected override string Text => text;

    protected override Color IconColor(bool hovering) => hovering ? hoverColor : idleColor;

    public override void OnClicked(Vector3 cursor) => LayoutEditor.Instance?.Begin();
}
