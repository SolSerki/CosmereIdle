using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Globo de dialogo. Se estira segun el texto, sigue al personaje y se va solo.
///
/// No tiene collider a proposito: es decorativo, y con uno DesktopWindow lo
/// contaria como contenido clickeable y se comeria clicks del escritorio.
///
/// Cuando hay varios globos encimados (personajes superpuestos), cada uno se
/// dibuja entero arriba del anterior: el fondo es opaco y el globo es un
/// SortingGroup, asi el texto de uno de atras no se mezcla con el de adelante.
/// El ultimo que aparecio queda arriba.
///
/// La punta de la colita va apoyada arriba de la cabeza y el globo crece hacia
/// arriba desde ahi. Si en la franja no entra, le pide lugar a la ventana
/// (SetOverlay) en vez de bajar a tapar la cara.
///
/// El tamaño no sigue al de los personajes: el globo lleva la escala inversa a
/// la de la franja, asi mide siempre lo mismo en pantalla. El texto tiene su
/// propio tamaño (<see cref="BubbleTextSize"/>); si cambia con el globo en
/// pantalla, el globo se vuelve a medir en el momento.
///
/// Las medidas del Inspector (padding, ancho maximo) son a escala 1 de los
/// personajes y con el texto al 100%.
/// </summary>
[RequireComponent(typeof(SortingGroup))]
public class SpeechBubble : MonoBehaviour
{
    /// <summary>Globos en pantalla, para apilarlos.</summary>
    private static readonly List<SpeechBubble> live = new List<SpeechBubble>();
    private static int nextLayer;

    [Tooltip("Orden de dibujo del primer globo. Los siguientes van un escalon arriba cada uno. " +
             "Tiene que quedar debajo del menu de la esquina (88).")]
    [SerializeField] private int baseSortingOrder = 60;

    [Tooltip("Cuantos globos se apilan antes de volver a empezar desde abajo.")]
    [SerializeField] private int maxStack = 25;

    [Tooltip("Fondo armado con las piezas de Art/Dialog.png. Su colita apunta al personaje: " +
             "sirve para saber quien hablo cuando hay varias mascotas juntas.")]
    [SerializeField] private BubbleFrame frame;

    [SerializeField] private TextMeshPro label;

    [Tooltip("Aire entre el texto y el borde del globo, en unidades de mundo.")]
    [SerializeField] private Vector2 padding = new Vector2(0.18f, 0.12f);

    [Tooltip("Ancho maximo antes de cortar en varias lineas.")]
    [SerializeField] private float maxTextWidth = 2.6f;

    [Tooltip("Cuanto tarda en aparecer y en desvanecerse.")]
    [SerializeField] private float fadeSeconds = 0.12f;

    [Tooltip("Opacidad del fondo cuando esta del todo visible. El texto siempre va opaco. " +
             "En 1 a proposito: con globos encimados, uno translucido deja ver el de atras.")]
    [SerializeField] private float baseAlpha = 1f;

    private Transform target;
    private float tipAboveFeet;
    private float hideAt;
    private float shownAt;
    private string phrase;
    private float baseFontSize;

    private void Awake() => baseFontSize = label.fontSize;

    private void OnEnable()
    {
        live.Add(this);
        BubbleTextSize.Changed += Relayout;
    }

    private void OnDisable()
    {
        live.Remove(this);
        BubbleTextSize.Changed -= Relayout;

        DesktopWindow window = DesktopWindow.Instance;
        if (window != null) window.SetOverlay(this, new RectInt());
    }

    /// <summary>
    /// Escala del globo en el mundo: la inversa de la de la franja, para que
    /// los personajes puedan crecer sin que crezca el globo.
    /// </summary>
    private static float WorldScale
    {
        get
        {
            DesktopWindow window = DesktopWindow.Instance;
            return window != null ? 1f / Mathf.Max(0.01f, window.StageScale) : 1f;
        }
    }

    /// <summary>
    /// Muestra una frase encima de un personaje. tipAboveFeet es a que altura
    /// sobre los pies va la punta de la colita.
    /// </summary>
    public void Show(string phrase, Transform follow, float tipAboveFeet, float seconds)
    {
        // El nuevo va arriba de todos los que estan en pantalla. Si no queda
        // ninguno, la pila vuelve a empezar desde abajo.
        if (live.Count <= 1) nextLayer = 0;
        var group = GetComponent<SortingGroup>();
        if (group == null) group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = baseSortingOrder + nextLayer % Mathf.Max(1, maxStack);
        nextLayer++;

        target = follow;
        this.tipAboveFeet = tipAboveFeet;
        this.phrase = phrase;
        shownAt = Time.time;
        hideAt = Time.time + seconds;

        Relayout();
    }

    /// <summary>
    /// Mide el texto con el tamaño elegido y arma el fondo a su medida. Lo
    /// llama Show, y tambien BubbleTextSize cuando cambia el tamaño con el
    /// globo en pantalla.
    /// </summary>
    private void Relayout()
    {
        if (phrase == null || target == null) return;

        // El ancho maximo y el aire crecen con la letra: asi una frase corta
        // en las mismas palabras, sea cual sea el tamaño.
        float k = BubbleTextSize.Scale;
        float maxWidth = maxTextWidth * k;
        label.fontSize = baseFontSize * k;
        label.text = phrase;

        // GetPreferredValues con ancho maximo devuelve el tamaño YA con el texto
        // cortado en lineas, que es lo que hay que medir para el fondo.
        Vector2 size = label.GetPreferredValues(phrase, maxWidth, 0f);
        size.x = Mathf.Min(size.x, maxWidth);

        label.rectTransform.sizeDelta = size;
        frame.Layout(size + padding * 2f * k);

        Reposition();
    }

    private void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

        Reposition();

        float alpha = Mathf.Min(
            Mathf.InverseLerp(0f, fadeSeconds, Time.time - shownAt),
            Mathf.InverseLerp(0f, fadeSeconds, hideAt - Time.time));

        SetAlpha(Mathf.Clamp01(alpha));

        if (Time.time >= hideAt) Destroy(gameObject);
    }

    private void Reposition()
    {
        // El globo se mide en sus unidades; en el mundo lleva la escala que lo
        // separa del tamaño de los personajes.
        float s = WorldScale;
        transform.localScale = new Vector3(s, s, 1f);

        // El centro del globo queda medio globo y lo que sobresale la colita
        // arriba de la punta.
        Vector2 bubbleSize = frame.Size * s;
        float halfHeight = bubbleSize.y * 0.5f;
        float edge = 0.05f * s;

        Vector3 p = target.position;
        p.y += tipAboveFeet + frame.TailDrop * s + halfHeight;
        p.z = 0f;

        // Que no se salga por los costados de la franja. Por arriba, si no
        // entra, la ventana crece hasta el borde del monitor; recien ahi se
        // recorta, para que al menos se vea entero.
        var cam = Camera.main;
        DesktopWindow window = DesktopWindow.Instance;
        if (window != null)
        {
            Rect stage = window.StageWorldRect;
            float halfBubble = bubbleSize.x * 0.5f;
            p.x = Mathf.Clamp(p.x, stage.xMin + halfBubble + edge, stage.xMax - halfBubble - edge);

            float top = p.y + halfHeight + edge;
            bool fits = window.Mode != DesktopWindow.WindowMode.Strip || top <= stage.yMax;
            window.SetOverlay(this, fits
                ? new RectInt()
                : window.WorldToDesktopRect(new Rect(stage.xMin, stage.yMax, stage.width, top - stage.yMax)));

            float screenTop = window.Mode == DesktopWindow.WindowMode.Strip
                ? window.DesktopToWorldRect(window.Limits).yMax
                : stage.yMax;
            p.y = Mathf.Min(p.y, screenTop - halfHeight - edge);

            // La esquina del globo, justo sobre un pixel de pantalla: el fondo
            // son varias piezas de pixel art, y si caen entre dos pixeles se
            // ven cortes entre ellas (pasa cuando el personaje camina o lo
            // arrastran y el globo lo sigue).
            Vector2 half = bubbleSize * 0.5f;
            Vector2 corner = window.SnapToPixel(new Vector2(p.x - half.x, p.y - half.y));
            p = new Vector3(corner.x + half.x, corner.y + half.y, 0f);
        }
        else if (cam != null)
        {
            float halfBubble = bubbleSize.x * 0.5f;
            float limit = cam.orthographicSize * cam.aspect - halfBubble - edge;
            p.x = Mathf.Clamp(p.x, cam.transform.position.x - limit, cam.transform.position.x + limit);
        }

        transform.position = p;

        // Si el globo se corrio para no salirse, la colita igual apunta al
        // personaje. SetTailX va en unidades del globo, sin la escala.
        frame.SetTailX((target.position.x - p.x) / s);
    }

    private void SetAlpha(float a)
    {
        frame.SetAlpha(a * baseAlpha);
        Color t = label.color; t.a = a; label.color = t;
    }
}
