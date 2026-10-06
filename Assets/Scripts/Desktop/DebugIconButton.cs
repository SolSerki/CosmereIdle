using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Base de los botones de la app. Se dibujan distinto segun la forma de la ventana:
///
///   Panel  - boton con icono grande y etiqueta debajo, en una fila abajo de todo.
///            Ahi hay lugar, y una pantalla de menu sin texto no se entiende.
///   Franja - iconito suelto en la esquina de arriba a la derecha, con el texto
///            escondido en un tooltip que sale al pasar el mouse. Es lo unico que
///            entra sin taparle el escritorio al usuario.
///
/// Un boton con menuSlot -1 no entra al menu del panel y vive solo en la esquina.
///
/// El collider no es decorativo: es lo que hace que DesktopWindow apague el
/// click-through cuando el cursor pasa por encima. Sin collider el boton se
/// dibuja pero el click se va al escritorio.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public abstract class DebugIconButton : MonoBehaviour
{
    /// <summary>
    /// Los que participan de la fila del menu. Sirve para repartir el ancho del
    /// panel entre todos sin que ninguno tenga que saber cuantos son ni que el
    /// numero quede escrito a mano en varios prefabs.
    /// </summary>
    private static readonly List<DebugIconButton> menuRow = new List<DebugIconButton>();

    [Header("Esquina (ventana en franja)")]
    [Tooltip("Posicion en la fila de la esquina: 0 es el mas pegado a la derecha.")]
    [SerializeField] private int slot = 0;

    [Tooltip("Margen contra el borde de la ventana, en unidades de mundo.")]
    [SerializeField] private float margin = 0.08f;

    [Tooltip("Separacion entre botones de la esquina, en unidades de mundo.")]
    [SerializeField] private float spacing = 0.26f;

    [Header("Menu (ventana en panel)")]
    [Tooltip("Orden en el menu del panel, de izquierda a derecha. " +
             "-1 = no aparece en el menu y vive solo en la esquina.")]
    [SerializeField] private int menuSlot = -1;

    [Tooltip("Texto del boton: etiqueta en el menu, y tooltip en la esquina.")]
    [SerializeField] private string menuLabel = "";

    [Tooltip("Cuanto se agranda el icono en el menu. Usar enteros: es pixel art, " +
             "y una escala fraccionaria lo deforma.")]
    [SerializeField] private float menuIconScale = 2f;

    [Tooltip("Altura del icono sobre el borde de abajo del panel.")]
    [SerializeField] private float menuIconY = 0.68f;

    [Tooltip("Altura del texto sobre el borde de abajo del panel.")]
    [SerializeField] private float menuLabelY = 0.2f;

    [Tooltip("Aire a los costados de la fila del menu.")]
    [SerializeField] private float menuSideMargin = 0.3f;

    [Header("Tooltip")]
    [Tooltip("Hijo con el texto. Se usa de etiqueta en el menu y de tooltip en la esquina.")]
    [SerializeField] private TextMeshPro label;

    [Tooltip("Fondo del tooltip. Es hermano del texto y solo se prende en la esquina: " +
             "en el menu el texto va sobre el panel y no necesita fondo.")]
    [SerializeField] private SpriteRenderer tooltipBackground;

    [Tooltip("Aire entre el texto y el borde del tooltip.")]
    [SerializeField] private Vector2 tooltipPadding = new Vector2(0.1f, 0.06f);

    [Tooltip("Separacion entre el icono y el tooltip que le cuelga abajo.")]
    [SerializeField] private float tooltipGap = 0.06f;

    protected SpriteRenderer Sprite { get; private set; }

    /// <summary>
    /// Que dice el tooltip. Por defecto lo mismo que la etiqueta del menu; los
    /// botones que son un switch lo pisan para contar en que estado estan.
    /// </summary>
    protected virtual string TooltipText => menuLabel;

    private BoxCollider2D box;
    private Camera cam;
    private string shownText;
    private Vector2 textSize;

    protected virtual void Awake()
    {
        Sprite = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
        cam = Camera.main;

        SetTextVisible(false, false);

        if (cam == null)
            Debug.LogError("[DebugIconButton] No hay camara con tag MainCamera.");
    }

    protected virtual void OnEnable()
    {
        if (menuSlot >= 0 && !menuRow.Contains(this)) menuRow.Add(this);
    }

    protected virtual void OnDisable()
    {
        menuRow.Remove(this);
    }

    protected virtual void Update()
    {
        if (cam == null) return;

        // Un boton no disponible se apaga entero: no se dibuja y no recibe clicks.
        // Tambien hay que apagarle el collider, o DesktopWindow lo sigue contando
        // como contenido y se come el click que deberia ir al escritorio.
        bool available = IsAvailable;
        Sprite.enabled = available;
        box.enabled = available;

        if (!available)
        {
            SetTextVisible(false, false);
            return;
        }

        bool inMenu = InMenu();

        if (inMenu) LayoutAsMenuButton();
        else LayoutAsCornerIcon();

        DesktopWindow window = DesktopWindow.Instance;
        bool hovering = window != null && box.OverlapPoint(window.CursorWorldPosition);

        if (!inMenu) LayoutTooltip(hovering);

        Color color = GetColor(hovering);
        Sprite.color = color;

        // En el menu la etiqueta acompaña el estado del icono (gris si no se puede
        // apretar). En el tooltip va siempre en blanco, sobre su propio fondo.
        if (inMenu && label != null) label.color = color;

        if (hovering && window.LeftPressedThisFrame)
            OnClicked();
    }

    /// <summary>Si el boton tiene sentido ahora mismo. Por defecto, siempre.</summary>
    protected virtual bool IsAvailable => true;

    /// <summary>Que hace el boton al ser apretado.</summary>
    protected abstract void OnClicked();

    /// <summary>Color segun el estado; el hover ya viene resuelto.</summary>
    protected abstract Color GetColor(bool hovering);

    private bool InMenu()
    {
        if (menuSlot < 0) return false;

        DesktopWindow window = DesktopWindow.Instance;
        return window != null && window.Mode == DesktopWindow.WindowMode.Panel;
    }

    /// <summary>
    /// Fila de abajo del panel: los botones se reparten el ancho en partes iguales,
    /// con el icono arriba y la etiqueta centrada debajo. Repartir en vez de medir
    /// el texto evita depender del largo de cada etiqueta, que ademas va a cambiar
    /// cuando se traduzca.
    /// </summary>
    private void LayoutAsMenuButton()
    {
        float scale = Mathf.Max(0.01f, menuIconScale);
        transform.localScale = Vector3.one * scale;

        DesktopWindow window = DesktopWindow.Instance;
        
        // En vez de usar el aspect de la cámara (que en el Editor abarca las barras negras),
        // usamos el ancho real del panel en unidades de mundo:
        float panelWorldWidth = (cam.orthographicSize * 2f); // Panel cuadrado: ancho = alto
        float halfWidth = panelWorldWidth * 0.5f;
        float bottom = cam.transform.position.y - cam.orthographicSize;

        int count = Mathf.Max(1, menuRow.Count);
        float slotWidth = (halfWidth * 2f - menuSideMargin * 2f) / count;
        float x = cam.transform.position.x - halfWidth + menuSideMargin
                  + (menuSlot + 0.5f) * slotWidth;

        transform.position = new Vector3(x, bottom + menuIconY, 0f);

        SetTextVisible(true, false);
        SetText(menuLabel);

        if (label != null)
        {
            Transform holder = label.transform.parent != null ? label.transform.parent : label.transform;
            holder.localScale = Vector3.one / scale;
            holder.position = new Vector3(x, bottom + menuLabelY, 0f);

            label.transform.localPosition = Vector3.zero;
            label.rectTransform.sizeDelta = new Vector2(slotWidth - 0.12f, 0.4f);
        }

        const float hitTop = 0.2f;
        const float hitBottom = 0.04f;

        box.size = new Vector2(slotWidth - 0.12f, menuIconY + hitTop - hitBottom) / scale;
        box.offset = new Vector2(0f, ((menuIconY + hitTop + hitBottom) * 0.5f - menuIconY) / scale);
    }

    /// <summary>
    /// Se reancla cada frame en vez de quedar en posicion fija: el ancho de la
    /// ventana depende de la resolucion de pantalla, la ventana se redimensiona
    /// en runtime cuando cambia la barra de tareas, y ademas cambia de forma.
    /// </summary>
    private void LayoutAsCornerIcon()
    {
        transform.localScale = Vector3.one;

        FitColliderToSprite();

        float halfWidth = cam.orthographicSize * cam.aspect;
        Vector3 half = Sprite.bounds.extents;

        transform.position = new Vector3(
            cam.transform.position.x + halfWidth - half.x - margin - slot * spacing,
            cam.transform.position.y + cam.orthographicSize - half.y - margin,
            0f);
    }

    /// <summary>
    /// Tooltip del icono de la esquina. Cuelga del icono y se pega al borde derecho
    /// de la ventana: ahi es donde viven estos botones, y asi el globito nunca se
    /// sale de la franja por mas largo que sea el texto.
    /// </summary>
    private void LayoutTooltip(bool hovering)
    {
        if (label == null || tooltipBackground == null) return;

        string text = TooltipText;

        if (!hovering || string.IsNullOrEmpty(text))
        {
            SetTextVisible(false, false);
            return;
        }

        SetTextVisible(true, true);
        SetText(text);

        label.color = Color.white;
        label.rectTransform.sizeDelta = textSize;
        label.transform.localPosition = Vector3.zero;

        Vector2 boxSize = textSize + tooltipPadding * 2f;
        tooltipBackground.size = boxSize;

        // Cuelga alineado al borde derecho de SU icono, no al de la ventana: asi se
        // ve de cual de los botones de la esquina esta hablando. El Min lo recorta
        // contra el borde de la ventana por si el boton quedara muy pegado.
        float windowRight = cam.transform.position.x + cam.orthographicSize * cam.aspect - margin;
        float right = Mathf.Min(transform.position.x + Sprite.bounds.extents.x, windowRight);
        float top = transform.position.y - Sprite.bounds.extents.y - tooltipGap;

        tooltipBackground.transform.localScale = Vector3.one;
        tooltipBackground.transform.position = new Vector3(
            right - boxSize.x * 0.5f,
            top - boxSize.y * 0.5f,
            0f);
    }

    /// <summary>Cambia el texto solo si hace falta: medirlo con TMP no es gratis.</summary>
    private void SetText(string text)
    {
        if (label == null || shownText == text) return;

        shownText = text;
        label.text = text;
        textSize = label.GetPreferredValues(text, 10f, 0f);
    }

    private void SetTextVisible(bool text, bool background)
    {
        if (label != null) label.enabled = text;
        if (tooltipBackground != null) tooltipBackground.enabled = background;
    }

    private void FitColliderToSprite()
    {
        if (Sprite.sprite == null) return;
        box.size = Sprite.sprite.bounds.size;
        box.offset = Vector2.zero;
    }
}
