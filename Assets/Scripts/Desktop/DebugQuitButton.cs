using UnityEngine;

/// <summary>
/// Cierra la app. Es la unica forma de salir hasta que exista el icono de
/// bandeja: la ventana es un WS_POPUP sin barra de titulo y con WS_EX_TOOLWINDOW,
/// asi que no sale en alt-tab y Alt+F4 no la alcanza.
/// </summary>
public class DebugQuitButton : DebugIconButton
{
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.25f, 0.2f, 1f);

    protected override Color GetColor(bool hovering) => hovering ? hoverColor : idleColor;

    protected override void OnClicked()
    {
        Debug.Log("[DebugQuitButton] Cerrando.");
        Application.Quit();
    }
}
