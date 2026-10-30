using TMPro;
using UnityEngine;

/// <summary>
/// Uno de los globos del menu de modos (<see cref="PetModeMenu"/>): el prefab
/// ModeBubble, con el fondo de globo de dialogo, el texto y un collider. El
/// collider es lo que hace que DesktopWindow no sea click-through encima.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class PetModeOption : MonoBehaviour
{
    [Tooltip("Modo que elige este globo.")]
    [SerializeField] private MascotaController.Mode mode;

    [SerializeField] private BubbleFrame frame;
    [SerializeField] private TextMeshPro label;

    [Tooltip("Aire entre el texto y el borde del globo, con el texto al 100%.")]
    [SerializeField] private Vector2 padding = new Vector2(0.14f, 0.08f);

    private BoxCollider2D box;
    private float baseFontSize;

    public MascotaController.Mode Mode => mode;

    /// <summary>Tamaño del globo sin la colita, en sus unidades (sin la escala del menu).</summary>
    public Vector2 Size => frame.Size;

    /// <summary>Cuanto sobresale la colita por debajo, en sus unidades.</summary>
    public float TailDrop => frame.TailDrop;

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        baseFontSize = label.fontSize;
    }

    /// <summary>
    /// Mide el texto con el tamaño elegido y arma el globo a su medida. Se mide
    /// en negrita: el modo actual va en negrita, y asi no cambia de tamaño (ni
    /// se corta en dos lineas) cuando pasa a ser el actual.
    /// </summary>
    public void Layout(float textScale)
    {
        label.fontSize = baseFontSize * textScale;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        FontStyles style = label.fontStyle;
        label.fontStyle = FontStyles.Bold;
        Vector2 text = label.GetPreferredValues(label.text);
        label.fontStyle = style;
        label.rectTransform.sizeDelta = text;

        frame.Layout(text + padding * 2f * textScale);
        box.size = frame.Size;
        box.offset = Vector2.zero;
    }

    /// <summary>Corre la colita para que apunte al personaje (en unidades del globo).</summary>
    public void SetTailX(float localX) => frame.SetTailX(localX);

    /// <summary>El modo actual va resaltado; el que esta bajo el cursor, teñido.</summary>
    public void SetLook(bool current, bool hovering, Color text, Color currentText, Color hoverTint)
    {
        label.color = current ? currentText : text;
        label.fontStyle = current ? FontStyles.Bold : FontStyles.Normal;
        frame.SetTint(hovering ? hoverTint : Color.white);
    }

    public bool Contains(Vector3 world) => box != null && box.enabled && box.OverlapPoint(world);
}
