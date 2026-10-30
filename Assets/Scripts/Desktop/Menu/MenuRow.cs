using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila del menu de la esquina (ver <see cref="CornerMenu"/>). El menu es un
/// Canvas armado en la escena: cada fila es un prefab con su icono, su texto y
/// su resaltado, y el orden es el de la jerarquia. Esta clase no crea ni
/// acomoda nada; solo dice que mostrar y que hacer.
///
/// Los clicks no pasan por el EventSystem: los reparte el CornerMenu con el
/// cursor de DesktopWindow, que se lee por Win32 y funciona aunque la ventana
/// no tenga el foco.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class MenuRow : MonoBehaviour
{
    [SerializeField] protected Image icon;
    [SerializeField] protected TMP_Text label;

    [Tooltip("Fondo que se prende con el cursor encima.")]
    [SerializeField] private Graphic highlight;

    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color textHoverColor = Color.white;
    [SerializeField] private Color textDisabledColor = new Color(1f, 1f, 1f, 0.3f);

    private RectTransform rect;
    private string shownText;

    public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;

    /// <summary>Si la fila tiene sentido ahora. Una que no, ni se muestra.</summary>
    public virtual bool IsAvailable => true;

    /// <summary>
    /// Si se puede apretar. Una que no, igual se ve, en gris: sirve para
    /// opciones que dependen de otra.
    /// </summary>
    public virtual bool IsInteractable => true;

    /// <summary>Si el menu se cierra despues de apretarla. Los switches lo dejan abierto.</summary>
    public virtual bool ClosesMenuOnClick => true;

    /// <summary>Texto de la fila. Puede cambiar con el estado.</summary>
    protected abstract string Text { get; }

    /// <summary>Color del icono segun el estado; el hover ya viene resuelto.</summary>
    protected abstract Color IconColor(bool hovering);

    /// <summary>Que hace la fila al apretarla. cursor esta en el mundo.</summary>
    public abstract void OnClicked(Vector3 cursor);

    public bool ContainsPoint(Vector3 world) => Contains(Rect, world);

    /// <summary>Si un punto del mundo cae dentro de un RectTransform de un Canvas en el mundo.</summary>
    public static bool Contains(RectTransform rt, Vector3 world)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;
        Vector3 local = rt.InverseTransformPoint(world);
        return rt.rect.Contains(new Vector2(local.x, local.y));
    }

    /// <summary>
    /// La llama el CornerMenu en cada frame con el menu abierto: actualiza texto
    /// y colores. hovering ya tiene en cuenta si se puede apretar.
    /// </summary>
    public virtual void Refresh(bool hovering, Vector3 cursor)
    {
        string text = Text;
        if (label != null && text != shownText)
        {
            shownText = text;
            label.text = text;
        }

        bool interactable = IsInteractable;
        if (label != null)
            label.color = !interactable ? textDisabledColor : hovering ? textHoverColor : textColor;

        if (icon != null) icon.color = IconColor(hovering);
        if (highlight != null) highlight.enabled = hovering;
    }
}
