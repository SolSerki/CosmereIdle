using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu de la esquina de abajo a la derecha de la franja, al estilo Taskbar
/// Hero: un boton chico pegado al borde que despliega una lista de opciones.
///
/// Es un Canvas en el mundo armado en la escena: el boton, el panel y cada fila
/// (<see cref="MenuRow"/>) estan en la jerarquia, y el orden de las filas es el
/// orden de la jerarquia (lo resuelve el VerticalLayoutGroup del panel). Este
/// componente no crea ni acomoda filas; solo:
///   - pone el Canvas en la esquina de la franja, con la escala de interfaz,
///   - abre el panel hacia arriba o, si arriba no entra, hacia abajo, y lo
///     mantiene dentro del monitor,
///   - reparte los clicks, con el cursor de DesktopWindow (Win32, no depende
///     del foco) y no con el EventSystem,
///   - le pide lugar a la ventana (SetOverlay) mientras esta abierto.
///
/// Las medidas del Canvas son pixeles de pantalla: 1 unidad del Canvas mide 1
/// pixel, sea cual sea el tamaño de los personajes.
///
/// Los avisos (actualizaciones) no son filas: son DebugIconButton que van
/// sueltos al lado del boton (<see cref="PinnedPosition"/>).
/// </summary>
[DefaultExecutionOrder(-50)]
public class CornerMenu : MonoBehaviour, DesktopWindow.IPointerTarget
{
    public enum Placement
    {
        /// <summary>No se dibuja: otra forma de ventana, o acomodando la franja.</summary>
        Hidden,

        /// <summary>Suelto al lado del boton del menu.</summary>
        Pinned
    }

    public static CornerMenu Instance { get; private set; }

    [Header("Piezas (Canvas en el mundo, en la escena)")]
    [SerializeField] private Canvas canvas;

    [Tooltip("Raiz del menu. Se pone en la esquina de abajo a la derecha de la franja; " +
             "sus hijos se anclan a esa esquina.")]
    [SerializeField] private RectTransform root;

    [Tooltip("Boton que abre y cierra el menu.")]
    [SerializeField] private Image launcher;

    [Tooltip("Panel con las filas. Lo ordena su VerticalLayoutGroup y lo mide su ContentSizeFitter.")]
    [SerializeField] private RectTransform panel;

    [SerializeField] private VerticalLayoutGroup panelLayout;

    [Header("Medidas (pixeles de pantalla)")]
    [Tooltip("Distancia del boton y del panel a los bordes de la franja.")]
    [SerializeField] private float margin = 6f;

    [Tooltip("Aire entre el boton y el menu desplegado.")]
    [SerializeField] private float panelGap = 6f;

    [Tooltip("Lugar de cada aviso suelto a la izquierda del boton.")]
    [SerializeField] private float pinnedSlot = 24f;

    [Header("Colores")]
    [SerializeField] private Color launcherColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color launcherHoverColor = Color.white;

    public bool IsOpen { get; private set; }

    /// <summary>
    /// localScale de las piezas de interfaz que no son del Canvas (los avisos
    /// sueltos). Ver DesktopWindow.UiScale.
    /// </summary>
    public float UiScale { get; private set; } = 1f;

    private readonly List<DebugIconButton> pinned = new List<DebugIconButton>();
    private MenuRow[] rows = new MenuRow[0];
    private readonly Vector3[] corners = new Vector3[4];
    private bool active;

        private void Awake()
    {
        Instance = this;
        EnsureHalloweenRow();
        if (panel != null) rows = panel.GetComponentsInChildren<MenuRow>(true);
    }

    private void EnsureHalloweenRow()
    {
        if (panel == null) return;
        if (panel.GetComponentInChildren<HalloweenToggleRow>(true) != null) return;

        var template = panel.GetComponentInChildren<TopmostRow>(true);
        if (template == null) return;

        GameObject rowGo = Instantiate(template.gameObject, panel);
        rowGo.name = "HalloweenToggleRow";

        var oldScript = rowGo.GetComponent<TopmostRow>();
        if (oldScript != null) DestroyImmediate(oldScript);

        var halloweenRow = rowGo.AddComponent<HalloweenToggleRow>();
        halloweenRow.Setup(
            rowGo.transform.Find("Icon")?.GetComponent<UnityEngine.UI.Image>(),
            rowGo.transform.Find("Label")?.GetComponent<TMPro.TMP_Text>(),
            rowGo.transform.Find("Highlight")?.GetComponent<UnityEngine.UI.Graphic>()
        );

        var quit = panel.GetComponentInChildren<QuitRow>(true);
        if (quit != null)
        {
            rowGo.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());
        }
    }

    private void OnEnable() => DesktopWindow.AddPointerTarget(this);
    private void OnDisable() => DesktopWindow.RemovePointerTarget(this);

    public void Close() => IsOpen = false;

    /// <summary>Donde va un aviso. index es su lugar al lado del boton del menu.</summary>
    public Placement PlacementOf(DebugIconButton button, out int index)
    {
        index = active ? pinned.IndexOf(button) : -1;
        if (index >= 0) return Placement.Pinned;

        index = 0;
        return Placement.Hidden;
    }

    /// <summary>Centro de un aviso suelto, en el mundo. El 0 es el mas pegado al boton.</summary>
    public Vector3 PinnedPosition(int slot)
    {
        Rect l = WorldRect(launcher.rectTransform);
        float x = l.xMin - pinnedSlot * (slot + 0.5f) * root.lossyScale.x;
        return new Vector3(x, l.center.y, 0f);
    }

    /// <summary>Para DesktopWindow: con el cursor encima del menu, la ventana no es click-through.</summary>
    public bool ContainsPoint(Vector3 world)
    {
        if (!active || canvas == null || !canvas.enabled) return false;
        return MenuRow.Contains(launcher.rectTransform, world)
               || (IsOpen && MenuRow.Contains(panel, world));
    }

    private void Update()
    {
        DesktopWindow window = DesktopWindow.Instance;

        active = window != null
                 && window.Mode == DesktopWindow.WindowMode.Strip
                 && !LayoutEditor.IsEditing;

        if (canvas != null) canvas.enabled = active;

        if (!active)
        {
            IsOpen = false;
            if (window != null) window.SetOverlay(this, new RectInt());
            return;
        }

        CollectPinned();
        UiScale = window.UiScale;

        // El Canvas va a la esquina de abajo a la derecha de la franja, y una
        // unidad del Canvas mide un pixel de pantalla.
        Rect stage = window.StageWorldRect;
        float scale = window.WorldUnitsPerPixel;
        root.position = new Vector3(stage.xMax, stage.yMin, 0f);
        root.localScale = new Vector3(scale, scale, 1f);

        Vector3 cursor = window.CursorWorldPosition;
        bool overLauncher = MenuRow.Contains(launcher.rectTransform, cursor);
        MenuRow hovered = IsOpen ? RowAt(cursor) : null;

        if (window.LeftPressedThisFrame)
        {
            if (overLauncher)
            {
                IsOpen = !IsOpen;
            }
            else if (hovered != null)
            {
                hovered.OnClicked(cursor);
                if (hovered.ClosesMenuOnClick) IsOpen = false;
            }
            else if (IsOpen && !MenuRow.Contains(panel, cursor))
            {
                IsOpen = false;
            }
        }

        launcher.color = overLauncher ? launcherHoverColor : launcherColor;

        foreach (MenuRow row in rows)
            if (row.gameObject.activeSelf != row.IsAvailable) row.gameObject.SetActive(row.IsAvailable);

        if (panel.gameObject.activeSelf != IsOpen) panel.gameObject.SetActive(IsOpen);

        if (IsOpen)
        {
            foreach (MenuRow row in rows)
                if (row.gameObject.activeSelf) row.Refresh(row == hovered, cursor);

            PlacePanel(window);
        }

        window.SetOverlay(this, IsOpen ? window.WorldToDesktopRect(WorldRect(panel)) : new RectInt());
    }

    /// <summary>La fila que se puede apretar bajo el cursor, o null.</summary>
    private MenuRow RowAt(Vector3 cursor)
    {
        foreach (MenuRow row in rows)
            if (row.gameObject.activeSelf && row.IsInteractable && row.ContainsPoint(cursor)) return row;
        return null;
    }

    private void CollectPinned()
    {
        pinned.Clear();

        foreach (var button in DebugIconButton.All)
            if (button != null && button.IsAvailableNow && button.PinnedNextToLauncher)
                pinned.Add(button);

        pinned.Sort((a, b) => a.Slot.CompareTo(b.Slot));
    }

    /// <summary>
    /// Pega el panel al borde derecho de la franja, arriba del boton; si arriba
    /// no entra en el monitor y abajo hay mas lugar, abajo. Despues lo corre lo
    /// que haga falta para que quede entero dentro del monitor: un menu fuera de
    /// la pantalla no se puede cerrar ni usar para volver a acomodar la franja.
    /// </summary>
    private void PlacePanel(DesktopWindow window)
    {
        // Primero el tamaño: depende de que filas estan prendidas.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        float scale = root.lossyScale.y;
        Rect screen = window.DesktopToWorldRect(window.Limits);
        Rect l = WorldRect(launcher.rectTransform);
        float height = panel.rect.height * scale;
        float gap = panelGap * scale;

        float roomUp = screen.yMax - (l.yMax + gap);
        float roomDown = (l.yMin - gap) - screen.yMin;
        bool opensDown = roomUp < height && roomDown > roomUp;

        // Cerrar va siempre pegado al boton: abajo de todo si se abre hacia
        // arriba, arriba de todo si se abre hacia abajo.
        if (panelLayout != null && panelLayout.reverseArrangement != opensDown)
        {
            panelLayout.reverseArrangement = opensDown;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        float launcherTop = launcher.rectTransform.anchoredPosition.y + launcher.rectTransform.rect.height;
        float launcherBottom = launcher.rectTransform.anchoredPosition.y;

        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, opensDown ? 1f : 0f);
        panel.anchoredPosition = new Vector2(-margin, opensDown ? launcherBottom - panelGap : launcherTop + panelGap);

        // Dentro del monitor por los cuatro lados.
        Rect p = WorldRect(panel);
        float dx = Mathf.Max(0f, screen.xMin - p.xMin) - Mathf.Max(0f, p.xMax - screen.xMax);
        float dy = Mathf.Max(0f, screen.yMin - p.yMin) - Mathf.Max(0f, p.yMax - screen.yMax);
        if (dx != 0f || dy != 0f)
            panel.anchoredPosition += new Vector2(dx / root.lossyScale.x, dy / scale);
    }

    private Rect WorldRect(RectTransform rt)
    {
        rt.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}
