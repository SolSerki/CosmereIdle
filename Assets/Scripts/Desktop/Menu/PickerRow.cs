using UnityEngine;

/// <summary>
/// "Selección de spren": vuelve a abrir la pantalla de seleccion. Es la forma
/// de volver al panel una vez que las mascotas estan en el escritorio.
/// </summary>
public class PickerRow : MenuRow
{
    [SerializeField] private string text = "Selección de spren";
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.75f, 1f, 1f);

    public override bool IsAvailable => CharacterPicker.Instance != null;

    protected override string Text => text;

    protected override Color IconColor(bool hovering) => hovering ? hoverColor : idleColor;

    public override void OnClicked(Vector3 cursor) => CharacterPicker.Instance?.ShowPicker();
}
