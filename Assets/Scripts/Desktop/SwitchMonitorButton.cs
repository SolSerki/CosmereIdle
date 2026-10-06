using UnityEngine;

public class SwitchMonitorButton : DebugIconButton
{
    [Header("Colores")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.8f, 0.9f, 1f, 1f);
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.35f);

    // Solo vive en el panel y si hay al menos 2 pantallas
    protected override bool IsAvailable
    {
        get
        {
            var monitors = Win32.GetMonitors();
            bool hasMultipleMonitors = monitors != null && monitors.Count > 1;

            // Se activa únicamente cuando la ventana está en modo Panel
            bool inPanel = DesktopWindow.Instance != null && DesktopWindow.Instance.Mode == DesktopWindow.WindowMode.Panel;

            return hasMultipleMonitors && inPanel;
        }
    }

    protected override string TooltipText => "Cambiar Monitor";

    protected override void OnClicked()
    {
        var monitors = Win32.GetMonitors();
        if (monitors == null || monitors.Count <= 1) return;

        int current = PlayerPrefs.GetInt("CosmereIdle_MonitorIndex", 0);
        int next = (current + 1) % monitors.Count;

        if (DesktopWindow.Instance != null)
        {
            DesktopWindow.Instance.SwitchToMonitor(next);
        }
    }

    protected override Color GetColor(bool hovering)
    {
        if (!IsAvailable) return disabledColor;
        return hovering ? hoverColor : normalColor;
    }
}