using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Menu de la esquina de abajo a la derecha de la franja, al estilo Taskbar
/// Hero: un boton chico pegado al borde que despliega hacia arriba una lista
/// con los botones de la app (DebugIconButton). Los avisos (actualizaciones)
/// no se esconden en la lista: van sueltos al lado del boton.
///
/// Este componente solo decide donde va cada cosa. Cada boton se dibuja y se
/// aprieta solo; le pregunta aca en que lugar le toca (<see cref="PlacementOf"/>).
/// Por eso corre antes que ellos.
///
/// Las medidas son de interfaz: unidades a escala 1, donde 1 = 100 pixeles. La
/// escala de la franja no las achica (DesktopWindow.UiScale lo compensa): los
/// personajes pueden quedar chiquitos, pero un menu ilegible no sirve.
///
/// Si la franja es muy baja para el menu abierto, le pide lugar a la ventana
/// con SetOverlay. La ventana crece hacia arriba solo mientras el menu esta
/// abierto; como es transparente, no se nota.
/// </summary>
[DefaultExecutionOrder(-50)]
public class CornerMenu : MonoBehaviour
{
    public enum Placement
    {
        /// <summary>No se dibuja: menu cerrado, otra forma de ventana, o acomodando la franja.</summary>
        Hidden,

        /// <summary>Fila del menu desplegado.</summary>
        Row,

        /// <summary>Suelto al lado del boton del menu.</summary>
        Pinned
    }

    public static CornerMenu Instance { get; private set; }

    [Header("Piezas")]
    [Tooltip("Boton que abre y cierra el menu. Necesita BoxCollider2D. Se dibuja en modo Sliced.")]
    [SerializeField] private SpriteRenderer launcher;

    [Tooltip("Icono dentro del boton.")]
    [SerializeField] private SpriteRenderer launcherIcon;

    [Tooltip("Fondo del menu desplegado. Se dibuja en modo Sliced.")]
    [SerializeField] private SpriteRenderer panel;

    [Tooltip("Resaltado de la fila que esta bajo el cursor. Se dibuja en modo Sliced.")]
    [SerializeField] private SpriteRenderer highlight;

    [Header("Medidas (interfaz, 1 = 100 px)")]
    [Tooltip("Distancia del boton a los bordes de la franja.")]
    [SerializeField] private float margin = 0.06f;
    [SerializeField] private float launcherSize = 0.26f;

    [Tooltip("Lugar de cada aviso suelto a la izquierda del boton.")]
    [SerializeField] private float pinnedSlot = 0.24f;

    [SerializeField] private float rowWidth = 1.7f;
    [SerializeField] private float rowHeight = 0.26f;

    [Tooltip("Ancho de la columna del icono dentro de cada fila.")]
    [SerializeField] private float rowIconSlot = 0.3f;

    [SerializeField] private float panelPadding = 0.08f;

    [Tooltip("Aire entre el boton y el menu desplegado.")]
    [SerializeField] private float panelGap = 0.06f;

    [Header("Colores")]
    [SerializeField] private Color launcherColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color launcherHoverColor = Color.white;
    [SerializeField] private Color iconColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color iconActiveColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.12f);

    public bool IsOpen { get; private set; }

    /// <summary>localScale de las piezas de interfaz (ver DesktopWindow.UiScale).</summary>
    public float UiScale { get; private set; } = 1f;

    public float RowWidth => rowWidth;
    public float RowHeight => rowHeight;
    public float RowIconSlot => rowIconSlot;

    private readonly List<DebugIconButton> rows = new List<DebugIconButton>();
    private readonly List<DebugIconButton> pinned = new List<DebugIconButton>();

    private BoxCollider2D launcherBox;
    private BoxCollider2D panelBox;
    private Vector3 launcherCenter;
    private Rect panelRect;
    private bool active;

    private void Awake()
    {
        Instance = this;
        if (launcher != null) launcherBox = launcher.GetComponent<BoxCollider2D>();
        if (panel != null) panelBox = panel.GetComponent<BoxCollider2D>();
    }

    public void Close() => IsOpen = false;

    /// <summary>Donde va un boton. index es su fila o su lugar al lado del boton del menu.</summary>
    public Placement PlacementOf(DebugIconButton button, out int index)
    {
        index = 0;
        if (!active) return Placement.Hidden;

        index = pinned.IndexOf(button);
        if (index >= 0) return Placement.Pinned;

        index = rows.IndexOf(button);
        if (index >= 0 && IsOpen) return Placement.Row;

        index = 0;
        return Placement.Hidden;
    }

    /// <summary>Centro del icono de la fila, en el mundo. La fila 0 es la de arriba.</summary>
    public Vector3 RowIconPosition(int row)
    {
        float x = panelRect.xMin + (panelPadding + rowIconSlot * 0.5f) * UiScale;
        float y = panelRect.yMax - (panelPadding + rowHeight * (row + 0.5f)) * UiScale;
        return new Vector3(x, y, 0f);
    }

    /// <summary>Centro de un aviso suelto. El 0 es el mas pegado al boton.</summary>
    public Vector3 PinnedPosition(int slot)
    {
        float x = launcherCenter.x - (launcherSize * 0.5f + pinnedSlot * (slot + 0.5f)) * UiScale;
        return new Vector3(x, launcherCenter.y, 0f);
    }

    private void Update()
    {
        DesktopWindow window = DesktopWindow.Instance;

        active = window != null
                 && window.Mode == DesktopWindow.WindowMode.Strip
                 && !LayoutEditor.IsEditing;

        if (!active)
        {
            IsOpen = false;
            SetVisible(false, false);
            if (window != null) window.SetOverlay(Vector2Int.zero);
            return;
        }

        CollectButtons();

        UiScale = window.UiScale;
        Rect stage = window.StageWorldRect;

        float half = launcherSize * 0.5f;
        launcherCenter = new Vector3(
            stage.xMax - (margin + half) * UiScale,
            stage.yMin + (margin + half) * UiScale,
            0f);

        float panelWidth = rowWidth + panelPadding * 2f;
        float panelHeight = rows.Count * rowHeight + panelPadding * 2f;
        float panelBottom = launcherCenter.y + (half + panelGap) * UiScale;
        float panelRight = stage.xMax - margin * UiScale;

        panelRect = new Rect(
            panelRight - panelWidth * UiScale,
            panelBottom,
            panelWidth * UiScale,
            panelHeight * UiScale);

        Vector3 cursor = window.CursorWorldPosition;
        bool overLauncher = launcherBox != null && launcherBox.OverlapPoint(cursor);

        if (window.LeftPressedThisFrame)
        {
            if (overLauncher) IsOpen = !IsOpen;
            else if (IsOpen && !panelRect.Contains(cursor)) IsOpen = false;
        }

        if (rows.Count == 0) IsOpen = false;

        RequestRoom(window, panelWidth, panelHeight);
        LayoutLauncher(overLauncher);
        LayoutPanel(cursor);
    }

    private void CollectButtons()
    {
        rows.Clear();
        pinned.Clear();

        foreach (var button in DebugIconButton.All)
        {
            if (button == null || !button.IsAvailableNow) continue;
            (button.PinnedNextToLauncher ? pinned : rows).Add(button);
        }

        // El de slot mas alto arriba de todo; Cerrar (slot 0) queda abajo,
        // pegado al boton del menu.
        rows.Sort((a, b) => b.Slot.CompareTo(a.Slot));
        pinned.Sort((a, b) => a.Slot.CompareTo(b.Slot));
    }

    /// <summary>
    /// Cuanto lugar hace falta, en pixeles, desde la esquina de abajo a la
    /// derecha del escenario. Con el menu cerrado el boton siempre entra.
    /// </summary>
    private void RequestRoom(DesktopWindow window, float panelWidth, float panelHeight)
    {
        if (!IsOpen)
        {
            window.SetOverlay(Vector2Int.zero);
            return;
        }

        float pixelsPerUiUnit = 100f * window.UiPixelScale;
        float width = margin + panelWidth;
        float height = margin + launcherSize + panelGap + panelHeight;

        window.SetOverlay(new Vector2Int(
            Mathf.CeilToInt(width * pixelsPerUiUnit),
            Mathf.CeilToInt(height * pixelsPerUiUnit)));
    }

    private void LayoutLauncher(bool hovering)
    {
        if (launcher == null) return;

        launcher.enabled = true;
        if (launcherBox != null)
        {
            launcherBox.enabled = true;
            launcherBox.size = Vector2.one * launcherSize;
            launcherBox.offset = Vector2.zero;
        }

        launcher.transform.localScale = Vector3.one * UiScale;
        launcher.transform.position = launcherCenter;
        launcher.drawMode = SpriteDrawMode.Sliced;
        launcher.size = Vector2.one * launcherSize;
        launcher.color = hovering ? launcherHoverColor : launcherColor;

        if (launcherIcon != null)
        {
            launcherIcon.enabled = true;
            launcherIcon.color = hovering || IsOpen ? iconActiveColor : iconColor;
        }
    }

    private void LayoutPanel(Vector3 cursor)
    {
        if (panel != null)
        {
            panel.enabled = IsOpen;
            if (panelBox != null) panelBox.enabled = IsOpen;

            if (IsOpen)
            {
                panel.drawMode = SpriteDrawMode.Sliced;
                panel.transform.localScale = Vector3.one * UiScale;
                panel.transform.position = panelRect.center;
                panel.size = panelRect.size / UiScale;

                // Sin collider el aire entre filas seria click-through y el
                // click se iria al escritorio de atras.
                if (panelBox != null)
                {
                    panelBox.size = panel.size;
                    panelBox.offset = Vector2.zero;
                }
            }
        }

        if (highlight == null) return;

        int hovered = -1;
        if (IsOpen && panelRect.Contains(cursor))
        {
            float fromTop = (panelRect.yMax - cursor.y) / UiScale - panelPadding;
            int row = Mathf.FloorToInt(fromTop / rowHeight);
            if (fromTop >= 0f && row < rows.Count) hovered = row;
        }

        highlight.enabled = hovered >= 0;
        if (hovered < 0) return;

        highlight.drawMode = SpriteDrawMode.Sliced;
        highlight.color = highlightColor;
        highlight.transform.localScale = Vector3.one * UiScale;
        highlight.size = new Vector2(rowWidth, rowHeight);

        Vector3 icon = RowIconPosition(hovered);
        highlight.transform.position = new Vector3(
            icon.x + (rowWidth * 0.5f - rowIconSlot * 0.5f) * UiScale,
            icon.y,
            0f);
    }

    private void SetVisible(bool launcherVisible, bool panelVisible)
    {
        if (launcher != null) launcher.enabled = launcherVisible;
        if (launcherBox != null) launcherBox.enabled = launcherVisible;
        if (launcherIcon != null) launcherIcon.enabled = launcherVisible;
        if (panel != null) panel.enabled = panelVisible;
        if (panelBox != null) panelBox.enabled = panelVisible;
        if (highlight != null) highlight.enabled = false;
    }
}
