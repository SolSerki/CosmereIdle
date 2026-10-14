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

    [SerializeField] private SpriteRenderer background;
    [SerializeField] private TextMeshPro label;

    [Tooltip("Colita que apunta al personaje. Sirve para saber quien hablo cuando " +
             "hay varias mascotas juntas.")]
    [SerializeField] private SpriteRenderer tail;

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
    private float offsetY;
    private float hideAt;
    private float shownAt;

    /// <summary>Muestra una frase encima de un personaje.</summary>
    private void OnEnable() => live.Add(this);
    private void OnDisable() => live.Remove(this);

    public void Show(string phrase, Transform follow, float aboveFeet, float seconds)
    {
        // El nuevo va arriba de todos los que estan en pantalla. Si no queda
        // ninguno, la pila vuelve a empezar desde abajo.
        if (live.Count <= 1) nextLayer = 0;
        var group = GetComponent<SortingGroup>();
        if (group == null) group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = baseSortingOrder + nextLayer % Mathf.Max(1, maxStack);
        nextLayer++;

        target = follow;
        offsetY = aboveFeet;
        shownAt = Time.time;
        hideAt = Time.time + seconds;

        label.text = phrase;

        // GetPreferredValues con ancho maximo devuelve el tamaño YA con el texto
        // cortado en lineas, que es lo que hay que medir para el fondo.
        Vector2 size = label.GetPreferredValues(phrase, maxTextWidth, 0f);
        size.x = Mathf.Min(size.x, maxTextWidth);

        label.rectTransform.sizeDelta = size;
        background.size = size + padding * 2f;

        if (tail != null)
            tail.transform.localPosition = new Vector3(0f, -background.size.y * 0.5f, 0f);

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
        Vector3 p = target.position;
        p.y += offsetY;
        p.z = 0f;

        // Que no se salga por los costados de la franja, ni por arriba si
        // levantaron al personaje arrastrandolo.
        var cam = Camera.main;
        DesktopWindow window = DesktopWindow.Instance;
        if (window != null)
        {
            Rect stage = window.StageWorldRect;
            float halfBubble = background.size.x * 0.5f;
            p.x = Mathf.Clamp(p.x, stage.xMin + halfBubble + 0.05f, stage.xMax - halfBubble - 0.05f);
            p.y = Mathf.Min(p.y, stage.yMax - background.size.y * 0.5f - 0.05f);
        }
        else if (cam != null)
        {
            float halfBubble = background.size.x * 0.5f;
            float limit = cam.orthographicSize * cam.aspect - halfBubble - 0.05f;
            p.x = Mathf.Clamp(p.x, cam.transform.position.x - limit, cam.transform.position.x + limit);
        }

        transform.position = p;
    }

    private void SetAlpha(float a)
    {
        Color c = background.color; c.a = a * baseAlpha; background.color = c;
        Color t = label.color;      t.a = a;             label.color = t;

        if (tail != null) { Color x = tail.color; x.a = a * baseAlpha; tail.color = x; }
    }
}
