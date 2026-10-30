using UnityEngine;

/// <summary>
/// "Spren x1": agranda o achica a los personajes, de a los saltos de
/// DesktopWindow (que cuidan que el pixel art no se deforme). No toca la
/// interfaz ni el texto de los globos; la franja crece sola si ya no entran.
/// </summary>
public class PetScaleRow : StepperRow
{
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1f, 1f);

    private static DesktopWindow Window => DesktopWindow.Instance;

    public override bool IsAvailable => Window != null;

    protected override string Text => $"Spren x{Window.PetScale:0.##}";

    protected override Color IconColor(bool hovering) => hovering ? hoverColor : idleColor;

    protected override void Step(int direction) =>
        Window.SetPetScale(Window.PetScale + direction * Window.PetScaleStep);

    protected override bool CanStep(int direction) => direction > 0
        ? Window.PetScale < Window.MaxPetScale - 0.001f
        : Window.PetScale > Window.MinPetScale + 0.001f;
}
