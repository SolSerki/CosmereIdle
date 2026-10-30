using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fila del menu con un valor que se sube y se baja: al final tiene un "−" y
/// un "+" (hijos del prefab MenuStepperRow) y cada click mueve el valor un
/// salto. La rueda del mouse sobre la fila hace lo mismo. Las subclases solo
/// dicen que valor mueven y hasta donde.
///
/// No hay campo para escribir el numero: la ventana es transparente y casi
/// nunca tiene el foco del teclado.
/// </summary>
public abstract class StepperRow : MenuRow
{
    [SerializeField] private TMP_Text minus;
    [SerializeField] private TMP_Text plus;

    [SerializeField] private Color stepColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color stepHoverColor = new Color(0.45f, 0.75f, 1f, 1f);

    [Tooltip("Color de un boton que no puede ir mas alla, o de la fila sin poder usarse.")]
    [SerializeField] private Color stepDisabledColor = new Color(1f, 1f, 1f, 0.15f);

    private float wheelNotches;

    /// <summary>Se aprieta varias veces seguidas: el menu queda abierto.</summary>
    public override bool ClosesMenuOnClick => false;

    /// <summary>Mueve el valor un salto: +1 sube, -1 baja.</summary>
    protected abstract void Step(int direction);

    /// <summary>Si el valor todavia puede ir en esa direccion.</summary>
    protected abstract bool CanStep(int direction);

    /// <summary>El click solo cuenta sobre el "−" o el "+"; sobre el texto no hace nada.</summary>
    public override void OnClicked(Vector3 cursor)
    {
        int step = StepAt(cursor);
        if (step != 0) Step(step);
    }

    public override void Refresh(bool hovering, Vector3 cursor)
    {
        base.Refresh(hovering, cursor);

        int hovered = hovering ? StepAt(cursor) : 0;
        if (minus != null) minus.color = StepColor(-1, hovered);
        if (plus != null) plus.color = StepColor(+1, hovered);

        if (hovering) ReadWheel();
        else wheelNotches = 0f;
    }

    /// <summary>-1 si el punto cae sobre el "−", +1 sobre el "+", 0 si no.</summary>
    private int StepAt(Vector3 world)
    {
        if (!IsInteractable) return 0;
        if (minus != null && Contains(minus.rectTransform, world)) return -1;
        if (plus != null && Contains(plus.rectTransform, world)) return +1;
        return 0;
    }

    private Color StepColor(int direction, int hovered)
    {
        if (!IsInteractable || !CanStep(direction)) return stepDisabledColor;
        return hovered == direction ? stepHoverColor : stepColor;
    }

    /// <summary>
    /// La rueda sube o baja el valor. Segun la plataforma una muesca llega como
    /// 120 o como 1; los touchpads mandan valores chicos y continuos, que se van
    /// juntando hasta completar una muesca.
    /// </summary>
    private void ReadWheel()
    {
        Mouse mouse = Mouse.current;
        if (!IsInteractable || mouse == null) return;

        float wheel = mouse.scroll.ReadValue().y;
        if (wheel == 0f) return;

        wheelNotches += Mathf.Abs(wheel) >= 30f ? wheel / 120f : wheel;
        while (wheelNotches >= 1f) { Step(+1); wheelNotches -= 1f; }
        while (wheelNotches <= -1f) { Step(-1); wheelNotches += 1f; }
    }
}
