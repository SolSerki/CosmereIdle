using UnityEngine;

/// <summary>
/// Vuelve a abrir la pantalla de seleccion de personaje. Vive solo en la franja:
/// es la forma de volver al panel una vez que las mascotas estan en el escritorio.
///
/// Es andamio como los otros botones: el "Cambiar personaje" del menu de bandeja
/// lo reemplaza en el hito 2.
/// </summary>
public class DebugPickerButton : DebugIconButton
{
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1f, 1f);

    /// <summary>
    /// Mientras el panel de seleccion esta abierto no tiene nada que hacer: ya
    /// estas ahi. Se apaga entero en vez de quedar como un boton que no responde.
    /// </summary>
    protected override bool IsAvailable
    {
        get
        {
            var picker = CharacterPicker.Instance;
            return picker == null || !picker.IsPicking;
        }
    }

    protected override Color GetColor(bool hovering) => hovering ? hoverColor : idleColor;

    protected override void OnClicked()
    {
        var picker = CharacterPicker.Instance;
        if (picker == null) return;

        picker.ShowPicker();
    }
}
