using UnityEngine;

/// <summary>
/// Cierra la app. La ventana es un WS_POPUP con WS_EX_TOOLWINDOW: no sale en
/// alt-tab y Alt+F4 no la alcanza, asi que esta es la forma de salir.
/// </summary>
public class QuitRow : MenuRow
{
    [SerializeField] private string text = "Cerrar";
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.25f, 0.2f, 1f);

    protected override string Text => text;

    protected override Color IconColor(bool hovering) => hovering ? hoverColor : idleColor;

    public override void OnClicked(Vector3 cursor)
    {
        Debug.Log("[QuitRow] Cerrando.");
        Application.Quit();
    }
}
