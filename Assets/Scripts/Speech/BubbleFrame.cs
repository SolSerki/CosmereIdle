using UnityEngine;

/// <summary>
/// Fondo del globo de dialogo, armado con las piezas de Art/Dialog.png.
///
/// La imagen viene dibujada en 15 piezas separadas por una linea transparente,
/// asi que no sirve como un solo sprite 9-slice: cada pieza es un hijo del
/// prefab con su propio SpriteRenderer (y su sprite y orden de dibujo puestos
/// ahi). Este componente no crea nada; solo las estira y las ubica. Las
/// esquinas no se estiran; los bordes se estiran a lo largo y el centro en las
/// dos direcciones (son lisos en esa direccion, asi que no se deforman). La
/// colita va abajo, tapando el borde, y sobresale por debajo.
///
/// Las medidas salen de los sprites: si se cambia el PPU de la imagen, el globo
/// cambia de tamaño de pixel sin tocar el codigo.
/// </summary>
public class BubbleFrame : MonoBehaviour
{
    [Header("Piezas (hijos del prefab)")]
    [SerializeField] private SpriteRenderer topLeft;
    [SerializeField] private SpriteRenderer top;
    [SerializeField] private SpriteRenderer topRight;
    [SerializeField] private SpriteRenderer left;
    [SerializeField] private SpriteRenderer center;
    [SerializeField] private SpriteRenderer right;
    [SerializeField] private SpriteRenderer bottomLeft;

    [Tooltip("Tramo del borde de abajo a la izquierda de la colita.")]
    [SerializeField] private SpriteRenderer bottomBeforeTail;

    [Tooltip("Colita. Su borde de arriba coincide con el de la fila de abajo del globo; " +
             "lo que tenga de mas alto sobresale por debajo. Se dibuja arriba del borde.")]
    [SerializeField] private SpriteRenderer tail;

    [Tooltip("Tramo del borde de abajo a la derecha de la colita.")]
    [SerializeField] private SpriteRenderer bottomAfterTail;

    [SerializeField] private SpriteRenderer bottomRight;

    /// <summary>Tamaño del globo sin la colita, en el mundo. Ya redondeado a pixeles.</summary>
    public Vector2 Size { get; private set; }

    /// <summary>Cuanto sobresale la colita por debajo del globo, en el mundo.</summary>
    public float TailDrop => Mathf.Max(0f, H(tail) - H(bottomBeforeTail));

    private float tailX;

    private float Pixel => center != null && center.sprite != null ? 1f / center.sprite.pixelsPerUnit : 0.01f;

    /// <summary>Lo minimo que mide el globo: las esquinas y la colita entera.</summary>
    private Vector2 MinSize => new Vector2(
        W(topLeft) + W(tail) + W(topRight),
        H(topLeft) + H(bottomLeft));

    /// <summary>
    /// Arma el globo para que mida al menos ese tamaño (sin la colita). Se
    /// redondea hacia arriba a pixeles enteros de la imagen, para que el borde
    /// no quede con un pixel mas grueso que otro.
    /// </summary>
    public void Layout(Vector2 wanted)
    {
        float pixel = Pixel;
        Vector2 min = MinSize;

        Size = new Vector2(
            Mathf.Max(Mathf.Ceil(wanted.x / pixel - 0.001f) * pixel, min.x),
            Mathf.Max(Mathf.Ceil(wanted.y / pixel - 0.001f) * pixel, min.y));

        float l = -Size.x * 0.5f, r = Size.x * 0.5f;
        float b = -Size.y * 0.5f, t = Size.y * 0.5f;
        float lw = W(topLeft), rw = W(topRight), th = H(topLeft), bh = H(bottomLeft);
        float middle = Size.y - th - bh;

        Place(topLeft, new Rect(l, t - th, lw, th));
        Place(top, new Rect(l + lw, t - th, Size.x - lw - rw, th));
        Place(topRight, new Rect(r - rw, t - th, rw, th));
        Place(left, new Rect(l, b + bh, lw, middle));
        Place(center, new Rect(l + lw, b + bh, Size.x - lw - rw, middle));
        Place(right, new Rect(r - rw, b + bh, rw, middle));
        Place(bottomLeft, new Rect(l, b, W(bottomLeft), bh));
        Place(bottomRight, new Rect(r - W(bottomRight), b, W(bottomRight), bh));

        LayoutBottom();
    }

    /// <summary>
    /// Mueve la colita a lo largo del borde de abajo, en coordenadas locales
    /// del globo (0 es el centro). Sirve para que siga apuntando al personaje
    /// cuando el globo se corre para no salirse de la franja.
    /// </summary>
    public void SetTailX(float localX)
    {
        if (Mathf.Approximately(localX, tailX)) return;
        tailX = localX;
        LayoutBottom();
    }

    private void LayoutBottom()
    {
        float pixel = Pixel;
        float l = -Size.x * 0.5f + W(bottomLeft);
        float r = Size.x * 0.5f - W(bottomRight);
        float b = -Size.y * 0.5f;
        float bh = H(bottomBeforeTail);
        float tw = W(tail);

        // El borde izquierdo de la colita cae en un pixel entero desde la
        // esquina, asi la colita no queda medio pixel corrida del borde.
        float tailLeft = Mathf.Clamp(tailX - tw * 0.5f, l, Mathf.Max(l, r - tw));
        tailLeft = l + Mathf.Round((tailLeft - l) / pixel) * pixel;

        Place(bottomBeforeTail, new Rect(l, b, tailLeft - l, bh));
        Place(tail, new Rect(tailLeft, b + bh - H(tail), tw, H(tail)));
        Place(bottomAfterTail, new Rect(tailLeft + tw, b, r - tailLeft - tw, bh));
    }

    public void SetAlpha(float a)
    {
        foreach (SpriteRenderer sr in Pieces)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = a;
            sr.color = c;
        }
    }

    /// <summary>Tiñe el globo entero (por ejemplo, con el cursor encima).</summary>
    public void SetTint(Color color)
    {
        foreach (SpriteRenderer sr in Pieces)
            if (sr != null) sr.color = color;
    }

    // Se arma una vez: SetAlpha corre en cada frame de cada globo.
    private SpriteRenderer[] pieces;

    private SpriteRenderer[] Pieces => pieces ??= new[]
    {
        topLeft, top, topRight, left, center, right,
        bottomLeft, bottomBeforeTail, tail, bottomAfterTail, bottomRight,
    };

    /// <summary>
    /// Estira una pieza para que ocupe ese rectangulo. Sin sprite o sin lugar
    /// (un globo angosto no tiene tramo de borde al lado de la colita) se apaga.
    /// </summary>
    private static void Place(SpriteRenderer sr, Rect rect)
    {
        if (sr == null) return;

        bool show = sr.sprite != null && rect.width > 0.0001f && rect.height > 0.0001f;
        sr.enabled = show;
        if (!show) return;

        Bounds native = sr.sprite.bounds;
        var scale = new Vector3(rect.width / native.size.x, rect.height / native.size.y, 1f);
        sr.transform.localScale = scale;

        // El pivot del sprite puede no estar en el centro: se corrige con el
        // centro de sus bounds, escalado.
        sr.transform.localPosition = new Vector3(
            rect.center.x - native.center.x * scale.x,
            rect.center.y - native.center.y * scale.y,
            0f);
    }

    private static float W(SpriteRenderer sr) => sr != null && sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
    private static float H(SpriteRenderer sr) => sr != null && sr.sprite != null ? sr.sprite.bounds.size.y : 0f;
}
