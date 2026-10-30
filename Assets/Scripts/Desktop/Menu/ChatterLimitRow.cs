using UnityEngine;

/// <summary>
/// Fila que ajusta el minimo o el maximo de la espera de la charla automatica
/// (<see cref="Chatter"/>), de a saltos de <see cref="Chatter.Steps"/>. Va con
/// sangria debajo de "Frases automáticas"; la sangria esta armada en el prefab
/// (MenuSubStepperRow), no aca.
///
/// Con la charla apagada se sigue viendo, pero en gris, y no responde.
/// </summary>
public class ChatterLimitRow : StepperRow
{
    private enum Limit { Min, Max }

    [Tooltip("Que ajusta esta fila.")]
    [SerializeField] private Limit limit = Limit.Min;

    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1f, 1f);
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.2f);

    public override bool IsInteractable => Chatter.Enabled;

    protected override string Text => limit == Limit.Min
        ? "Mín. " + Chatter.Format(Chatter.MinSeconds)
        : "Máx. " + Chatter.Format(Chatter.MaxSeconds);

    protected override Color IconColor(bool hovering) =>
        !IsInteractable ? disabledColor : hovering ? hoverColor : idleColor;

    protected override void Step(int direction)
    {
        if (limit == Limit.Min) Chatter.StepMin(direction);
        else Chatter.StepMax(direction);
    }

    protected override bool CanStep(int direction) =>
        limit == Limit.Min ? Chatter.CanStepMin(direction) : Chatter.CanStepMax(direction);
}
