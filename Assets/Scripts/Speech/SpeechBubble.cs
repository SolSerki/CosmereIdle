using TMPro;
using UnityEngine;

/// <summary>
/// Globo de dialogo. Se estira segun el texto, sigue al personaje y se va solo.
///
/// No tiene collider a proposito: es decorativo, y con uno DesktopWindow lo
/// contaria como contenido clickeable y se comeria clicks del escritorio.
/// </summary>
public class SpeechBubble : MonoBehaviour
{
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

    [Tooltip("Opacidad del fondo cuando esta del todo visible. El texto siempre va opaco.")]
    [SerializeField] private float baseAlpha = 0.88f;

    private Transform target;
    private float offsetY;
    private float hideAt;
    private float shownAt;

    /// <summary>Muestra una frase encima de un personaje.</summary>
    public void Show(string phrase, Transform follow, float aboveFeet, float seconds)
    {
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

        // Que no se salga por los costados de la franja.
        var cam = Camera.main;
        if (cam != null)
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
