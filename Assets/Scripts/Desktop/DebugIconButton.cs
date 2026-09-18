using UnityEngine;

/// <summary>
/// Base de los botoncitos de prueba que viven en la esquina de la franja.
///
/// Son andamio del hito 0: los reemplaza el menu del icono de bandeja en el
/// hito 2. Hasta entonces son la unica forma de cerrar la app y de cambiar
/// settings sin recompilar.
///
/// El collider no es decorativo: es lo que hace que DesktopWindow apague el
/// click-through cuando el cursor pasa por encima. Sin collider el boton se
/// dibuja pero el click se va al escritorio.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public abstract class DebugIconButton : MonoBehaviour
{
    [Tooltip("Posicion en la fila de botones: 0 es el mas pegado a la derecha.")]
    [SerializeField] private int slot = 0;

    [Tooltip("Margen contra el borde de la franja, en unidades de mundo.")]
    [SerializeField] private float margin = 0.08f;

    [Tooltip("Separacion entre botones, en unidades de mundo.")]
    [SerializeField] private float spacing = 0.26f;

    protected SpriteRenderer Sprite { get; private set; }

    private BoxCollider2D box;
    private Camera cam;

    protected virtual void Awake()
    {
        Sprite = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
        cam = Camera.main;

        FitColliderToSprite();

        if (cam == null)
            Debug.LogError("[DebugIconButton] No hay camara con tag MainCamera.");
    }

    protected virtual void Update()
    {
        if (cam == null) return;

        AnchorToTopRight();

        DesktopWindow window = DesktopWindow.Instance;
        if (window == null) return;

        bool hovering = box.OverlapPoint(window.CursorWorldPosition);
        Sprite.color = GetColor(hovering);

        if (hovering && window.LeftPressedThisFrame)
            OnClicked();
    }

    /// <summary>Que hace el boton al ser apretado.</summary>
    protected abstract void OnClicked();

    /// <summary>Color segun el estado; el hover ya viene resuelto.</summary>
    protected abstract Color GetColor(bool hovering);

    private void FitColliderToSprite()
    {
        if (Sprite.sprite == null) return;
        box.size = Sprite.sprite.bounds.size;
        box.offset = Vector2.zero;
    }

    /// <summary>
    /// Se reancla cada frame en vez de quedar en posicion fija: el ancho de la
    /// franja depende de la resolucion de pantalla, y la ventana se redimensiona
    /// en runtime cuando cambia la barra de tareas.
    /// </summary>
    private void AnchorToTopRight()
    {
        float halfWidth = cam.orthographicSize * cam.aspect;
        Vector3 half = Sprite.bounds.extents;

        transform.position = new Vector3(
            cam.transform.position.x + halfWidth - half.x - margin - slot * spacing,
            cam.transform.position.y + cam.orthographicSize - half.y - margin,
            0f);
    }
}
