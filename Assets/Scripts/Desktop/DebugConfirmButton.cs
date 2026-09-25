using UnityEngine;

/// <summary>
/// Confirma la seleccion de personajes. Solo existe mientras la fila de
/// seleccion esta abierta: el resto del tiempo se apaga entero, asi no ocupa
/// lugar ni se come clicks que van al escritorio.
///
/// Se pone gris mientras no haya nadie marcado, para que se vea que falta
/// elegir antes de que alguien lo apriete y no pase nada.
/// </summary>
public class DebugConfirmButton : DebugIconButton
{
    [SerializeField] private Color readyColor = new Color(0.35f, 0.9f, 0.45f, 0.9f);
    [SerializeField] private Color hoverColor = new Color(0.55f, 1f, 0.6f, 1f);
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.2f);

    protected override bool IsAvailable
    {
        get
        {
            var picker = CharacterPicker.Instance;
            return picker != null && picker.IsPicking;
        }
    }

    protected override Color GetColor(bool hovering)
    {
        var picker = CharacterPicker.Instance;
        if (picker == null || picker.SelectedCount == 0) return emptyColor;

        return hovering ? hoverColor : readyColor;
    }

    protected override void OnClicked()
    {
        var picker = CharacterPicker.Instance;
        if (picker == null) return;

        picker.Confirm();
    }
}
