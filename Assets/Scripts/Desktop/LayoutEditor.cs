using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Modo "Mover y escalar ventana" de la franja. Mientras esta prendido se
/// dibuja un marco sobre la franja (la ventana es transparente: sin marco no
/// hay nada que agarrar) y:
///
///   - arrastrar en cualquier lado del marco mueve la franja,
///   - las manijas de las cuatro esquinas cambian el tamaño de la ventana, sin
///     tocar a los personajes,
///   - "Restablecer" devuelve la franja a la de siempre, "Listo" o un click
///     afuera terminan.
///
/// El tamaño de los personajes no se cambia aca sino en el menu de la esquina
/// (PetScaleRow).
///
/// Se calcula todo en pixeles de pantalla y no en coordenadas de mundo: el
/// mundo se mueve justamente mientras se arrastra.
///
/// Lo que hace a la ventana lo hace DesktopWindow; aca solo esta la interaccion.
/// </summary>
[DefaultExecutionOrder(-60)] // Antes que los botones: el click que lo abre no tiene que cerrarlo.
public class LayoutEditor : MonoBehaviour
{
    /// <summary>Boton del modo edicion: fondo, icono opcional y texto, con su collider.</summary>
    [Serializable]
    private class Pill
    {
        public SpriteRenderer background;
        public SpriteRenderer icon;
        public TextMeshPro label;

        [NonSerialized] public BoxCollider2D box;

        public void Init()
        {
            if (background != null) box = background.GetComponent<BoxCollider2D>();
        }

        public void SetVisible(bool visible)
        {
            if (background != null) background.enabled = visible;
            if (box != null) box.enabled = visible;
            if (icon != null) icon.enabled = visible;
            if (label != null) label.enabled = visible;
        }

        public bool Contains(Vector3 point) => box != null && box.enabled && box.OverlapPoint(point);
    }

    public static LayoutEditor Instance { get; private set; }

    /// <summary>Si se esta acomodando la franja. El resto del juego se corre del medio.</summary>
    public static bool IsEditing => Instance != null && Instance.editing;

    [Header("Piezas")]
    [Tooltip("Marco sobre la franja. Necesita BoxCollider2D: es de donde se agarra para moverla.")]
    [SerializeField] private SpriteRenderer frame;

    [Tooltip("Manijas de las esquinas, en este orden: arriba-izquierda, arriba-derecha, " +
             "abajo-izquierda, abajo-derecha. Cada una necesita BoxCollider2D. El icono se " +
             "dibuja para la de abajo a la derecha y se espeja para las otras.")]
    [SerializeField] private SpriteRenderer[] grips = new SpriteRenderer[4];

    [Tooltip("Texto que explica que hacer.")]
    [SerializeField] private TextMeshPro hint;

    [SerializeField] private Pill doneButton;
    [SerializeField] private Pill resetButton;

    [Header("Medidas (interfaz, 1 = 100 px)")]
    [SerializeField] private float margin = 0.04f;
    [SerializeField] private float gripSize = 0.26f;
    [SerializeField] private float buttonHeight = 0.26f;
    [SerializeField] private float buttonIconSlot = 0.24f;
    [SerializeField] private float buttonPadding = 0.08f;
    [SerializeField] private float buttonGap = 0.06f;

    [Header("Colores")]
    [SerializeField] private Color frameColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color gripColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color gripActiveColor = Color.white;
    [SerializeField] private Color buttonColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color buttonHoverColor = Color.white;
    [SerializeField] private Color buttonDisabledColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.8f);

    private enum Drag { None, Move, Resize }

    private bool editing;
    private Drag drag;
    private int dragCorner;
    private Vector2Int grabOffset;
    private Vector2Int startCursor;
    private RectInt startStage;

    private BoxCollider2D frameBox;
    private BoxCollider2D[] gripBoxes;

    private void Awake()
    {
        Instance = this;
        if (frame != null) frameBox = frame.GetComponent<BoxCollider2D>();

        gripBoxes = new BoxCollider2D[grips.Length];
        for (int i = 0; i < grips.Length; i++)
            if (grips[i] != null) gripBoxes[i] = grips[i].GetComponent<BoxCollider2D>();

        doneButton?.Init();
        resetButton?.Init();
        SetVisible(false);
    }

    /// <summary>Prende el modo edicion. Lo llama la fila "Mover y escalar ventana" del menu.</summary>
    public void Begin()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null || window.Mode != DesktopWindow.WindowMode.Strip) return;

        editing = true;
        drag = Drag.None;
    }

    public void End()
    {
        EndDrag();
        editing = false;
        SetVisible(false);
    }

    private void Update()
    {
        if (!editing) return;

        DesktopWindow window = DesktopWindow.Instance;
        if (window == null || window.Mode != DesktopWindow.WindowMode.Strip)
        {
            End();
            return;
        }

        Vector3 cursor = window.CursorWorldPosition;

        if (window.LeftPressedThisFrame && drag == Drag.None)
        {
            int corner = GripUnder(cursor);

            if (doneButton.Contains(cursor)) { End(); return; }
            if (resetButton.Contains(cursor)) window.ResetStage();
            else if (corner >= 0) BeginResize(window, corner);
            else if (frameBox != null && frameBox.enabled && frameBox.OverlapPoint(cursor)) BeginDrag(window, Drag.Move);
            else
            {
                // Click afuera de la franja: se termino de acomodar.
                End();
                return;
            }
        }

        if (drag != Drag.None)
        {
            if (window.IsLeftDown) ApplyDrag(window);
            else EndDrag();
        }

        Layout(window, cursor);
    }

    private int GripUnder(Vector3 cursor)
    {
        for (int i = 0; i < gripBoxes.Length; i++)
            if (gripBoxes[i] != null && gripBoxes[i].enabled && gripBoxes[i].OverlapPoint(cursor))
                return i;
        return -1;
    }

    private void BeginResize(DesktopWindow window, int corner)
    {
        dragCorner = corner;
        BeginDrag(window, Drag.Resize);
    }

    private void BeginDrag(DesktopWindow window, Drag kind)
    {
        drag = kind;
        startCursor = window.CursorScreenPixel;
        startStage = window.Stage;
        grabOffset = startCursor - startStage.position;

        // La ventana tiene que seguir capturando el mouse aunque el cursor se
        // escape del marco en un tiron rapido.
        window.IsDraggingContent = true;
    }

    private void ApplyDrag(DesktopWindow window)
    {
        Vector2Int cursor = window.CursorScreenPixel;

        if (drag == Drag.Move)
        {
            window.MoveStage(cursor - grabOffset);
            return;
        }

        // La esquina agarrada sigue al cursor y la opuesta queda quieta.
        // Pixeles de pantalla: la Y crece hacia abajo.
        bool right = IsRight(dragCorner);
        bool bottom = IsBottom(dragCorner);
        Vector2Int delta = cursor - startCursor;

        int xMin = startStage.xMin, xMax = startStage.xMax;
        int yMin = startStage.yMin, yMax = startStage.yMax;

        if (right) xMax += delta.x; else xMin += delta.x;
        if (bottom) yMax += delta.y; else yMin += delta.y;

        var anchor = new Vector2Int(right ? 0 : 1, bottom ? 0 : 1);
        window.ResizeStage(new RectInt(xMin, yMin, xMax - xMin, yMax - yMin), anchor);
    }

    private static bool IsRight(int corner) => corner == 1 || corner == 3;
    private static bool IsBottom(int corner) => corner >= 2;

    private void EndDrag()
    {
        if (drag == Drag.None) return;
        drag = Drag.None;

        DesktopWindow window = DesktopWindow.Instance;
        if (window == null) return;

        window.IsDraggingContent = false;
        window.SaveStage();
    }

    private void Layout(DesktopWindow window, Vector3 cursor)
    {
        SetVisible(true);

        float k = window.UiScale;
        Rect stage = window.StageWorldRect;

        if (frame != null)
        {
            frame.drawMode = SpriteDrawMode.Sliced;
            frame.color = frameColor;
            frame.transform.localScale = Vector3.one * k;
            frame.transform.position = stage.center;
            frame.size = stage.size / k;

            if (frameBox != null)
            {
                frameBox.size = frame.size;
                frameBox.offset = Vector2.zero;
            }
        }

        int hotGrip = drag == Drag.Resize ? dragCorner : GripUnder(cursor);
        float inset = (margin + gripSize * 0.5f) * k;

        for (int i = 0; i < grips.Length; i++)
        {
            SpriteRenderer grip = grips[i];
            if (grip == null) continue;

            bool atRight = IsRight(i);
            bool atBottom = IsBottom(i);

            grip.transform.localScale = Vector3.one * k;
            grip.transform.position = new Vector3(
                atRight ? stage.xMax - inset : stage.xMin + inset,
                atBottom ? stage.yMin + inset : stage.yMax - inset,
                0f);

            // El icono apunta a la esquina de abajo a la derecha: se espeja.
            grip.flipX = !atRight;
            grip.flipY = !atBottom;
            grip.color = i == hotGrip ? gripActiveColor : gripColor;

            if (gripBoxes[i] != null)
            {
                gripBoxes[i].size = Vector2.one * gripSize;
                gripBoxes[i].offset = Vector2.zero;
            }
        }

        // Fila de arriba, entre las manijas: Restablecer y Listo a la derecha.
        float topRow = stage.yMax - inset;
        float right = stage.xMax - (margin + gripSize + buttonGap) * k;

        right = LayoutPill(doneButton, right, topRow, k, cursor, true) - buttonGap * k;
        LayoutPill(resetButton, right, topRow, k, cursor, true);

        if (hint != null)
        {
            hint.transform.localScale = Vector3.one * k;
            hint.transform.position = stage.center;
            hint.color = textColor;

            const string text = "Arrastrá la ventana y redimensioná desde las puntas";
            if (hint.text != text) hint.text = text;

            // En una franja chica no entra: se esconde antes que taparle los
            // botones.
            Vector2 needed = hint.GetPreferredValues(text) * k;
            float free = stage.height - 2f * (margin + gripSize + buttonGap) * k;
            hint.enabled = needed.x < stage.width - 2f * margin * k && needed.y < free;
        }
    }

    /// <summary>Acomoda un boton con su borde derecho en x. Devuelve su borde izquierdo.</summary>
    private float LayoutPill(Pill pill, float right, float centerY, float k, Vector3 cursor, bool enabled)
    {
        if (pill == null || pill.background == null) return right;

        float labelWidth = pill.label != null ? pill.label.GetPreferredValues(pill.label.text).x : 0f;
        float iconWidth = pill.icon != null ? buttonIconSlot : buttonPadding;
        float width = iconWidth + labelWidth + buttonPadding;
        float left = right - width * k;

        Transform t = pill.background.transform;
        t.localScale = Vector3.one * k;
        t.position = new Vector3((left + right) * 0.5f, centerY, 0f);

        pill.background.drawMode = SpriteDrawMode.Sliced;
        pill.background.size = new Vector2(width, buttonHeight);
        pill.background.color = !enabled ? buttonDisabledColor
            : pill.Contains(cursor) ? buttonHoverColor
            : buttonColor;

        // Uno que no se puede apretar (ya en el tamaño minimo o maximo) se ve
        // gris y no recibe el click.
        if (pill.box != null)
        {
            pill.box.enabled = enabled;
            pill.box.size = pill.background.size;
            pill.box.offset = Vector2.zero;
        }

        // Icono y texto son hijos del fondo: se ubican en sus unidades locales.
        if (pill.icon != null)
            pill.icon.transform.localPosition = new Vector3(-width * 0.5f + buttonIconSlot * 0.5f, 0f, 0f);

        if (pill.label != null)
        {
            pill.label.alignment = TextAlignmentOptions.Center;
            pill.label.rectTransform.sizeDelta = new Vector2(labelWidth, buttonHeight);
            pill.label.transform.localPosition = new Vector3(-width * 0.5f + iconWidth + labelWidth * 0.5f, 0f, 0f);
            pill.label.color = enabled ? Color.white : buttonDisabledColor;
        }

        return left;
    }

    private void SetVisible(bool visible)
    {
        if (frame != null) frame.enabled = visible;
        if (frameBox != null) frameBox.enabled = visible;

        for (int i = 0; i < grips.Length; i++)
        {
            if (grips[i] != null) grips[i].enabled = visible;
            if (gripBoxes != null && gripBoxes[i] != null) gripBoxes[i].enabled = visible;
        }

        if (hint != null && !visible) hint.enabled = false;
        doneButton?.SetVisible(visible);
        resetButton?.SetVisible(visible);
    }
}
