using UnityEngine;

/// <summary>
/// "Texto 100%": agranda o achica la letra de los globos de dialogo
/// (<see cref="BubbleTextSize"/>), aparte del tamaño de los personajes. Los
/// globos que esten en pantalla cambian en el momento.
/// </summary>
public class TextSizeRow : StepperRow
{
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1f, 1f);

    protected override string Text => "Texto " + BubbleTextSize.Format();

    protected override Color IconColor(bool hovering) => hovering ? hoverColor : idleColor;

    protected override void Step(int direction) => BubbleTextSize.Step(direction);

    protected override bool CanStep(int direction) => BubbleTextSize.CanStep(direction);
}
