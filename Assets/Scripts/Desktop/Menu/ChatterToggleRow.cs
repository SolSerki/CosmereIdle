using UnityEngine;

/// <summary>
/// "Frases automáticas": prende y apaga la charla automatica
/// (<see cref="Chatter"/>). Prendido (verde): los personajes dicen frases solos
/// cada tanto. Apagado (gris, "Frases con click"): solo cuando les hacen click.
/// Las filas de minimo y maximo van debajo y se ponen en gris con esta.
/// </summary>
public class ChatterToggleRow : MenuRow
{
    [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f, 0.9f);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color hoverTint = Color.white;

    public override bool ClosesMenuOnClick => false;

    protected override string Text => Chatter.Enabled ? "Frases automáticas" : "Frases con click";

    protected override Color IconColor(bool hovering)
    {
        Color baseColor = Chatter.Enabled ? onColor : offColor;
        return hovering ? Color.Lerp(baseColor, hoverTint, 0.5f) : baseColor;
    }

    public override void OnClicked(Vector3 cursor) => Chatter.Enabled = !Chatter.Enabled;
}
