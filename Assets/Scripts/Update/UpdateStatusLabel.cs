using TMPro;
using UnityEngine;

/// <summary>
/// Cartel de actualizaciones arriba de la pantalla de seleccion.
///
/// Ahi las mascotas estan congeladas y no pueden avisar con un globo, y es
/// justo donde se ve la actualizacion del arranque. Muestra en que anda el
/// updater: buscando, descargando con porcentaje, instalando, o "al dia" con
/// la version actual. En la franja no se muestra: ahi avisan el icono de la
/// esquina y las mascotas.
///
/// Es decorativo y no tiene collider: no se come clicks del escritorio.
/// </summary>
public class UpdateStatusLabel : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    [Tooltip("Distancia desde el borde de arriba del panel, en unidades de mundo.")]
    [SerializeField] private float topMargin = 0.22f;

    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color errorColor = new Color(1f, 0.5f, 0.45f, 1f);

    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        if (label == null) label = GetComponentInChildren<TextMeshPro>();
    }

    private void LateUpdate()
    {
        if (label == null) return;

        DesktopWindow window = DesktopWindow.Instance;
        bool inPanel = window != null && window.Mode == DesktopWindow.WindowMode.Panel;

        label.enabled = inPanel;
        if (!inPanel || cam == null) return;

        transform.position = new Vector3(
            cam.transform.position.x,
            cam.transform.position.y + cam.orthographicSize - topMargin,
            0f);

        Describe(out string text, out Color color);
        if (label.text != text) label.text = text;
        label.color = color;
    }

    private void Describe(out string text, out Color color)
    {
        string current = $"v{Application.version}";
        GitHubUpdater u = GitHubUpdater.Instance;

        color = normalColor;

        if (u == null || !u.IsEnabled)
        {
            text = current;
            return;
        }

        switch (u.State)
        {
            case GitHubUpdater.UpdateState.Installing:
                text = $"Instalando la versión {u.LatestVersion}... ya vuelvo";
                color = highlightColor;
                return;

            case GitHubUpdater.UpdateState.Downloading:
                text = $"Descargando la versión {u.LatestVersion}... {Mathf.RoundToInt(u.DownloadProgress * 100f)}%";
                color = highlightColor;
                return;

            case GitHubUpdater.UpdateState.Available:
                if (u.LastError != null)
                {
                    text = "No se pudo actualizar. Tocá el icono amarillo para reintentar";
                    color = errorColor;
                }
                else
                {
                    text = $"Versión {u.LatestVersion} disponible. Tocá el icono amarillo";
                    color = highlightColor;
                }
                return;
        }

        if (u.IsChecking)
        {
            // Puntos que se mueven: sin animacion, un texto fijo no dice si
            // sigue buscando o se colgo.
            int dots = 1 + (int)(Time.unscaledTime * 2f) % 3;
            text = $"{current} · Buscando actualizaciones{new string('.', dots)}";
        }
        else if (u.HasChecked) text = $"{current} · Al día";
        else if (u.LastCheckFailed) text = $"{current} · No se pudo buscar actualizaciones";
        else text = current;
    }
}
