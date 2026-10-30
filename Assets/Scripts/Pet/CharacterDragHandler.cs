using UnityEngine;

/// <summary>
/// Arrastrar al personaje con el mouse, en cualquier direccion dentro de la
/// franja. Si se lo suelta en el aire, cae hasta el piso y recien ahi vuelve
/// a caminar: StripWalker solo lo mueve de costado, asi que si no cayera se
/// quedaria caminando en el aire.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CharacterDragHandler : MonoBehaviour
{
    [Tooltip("Margen respecto a los bordes de la franja.")]
    [SerializeField] private float edgePadding = 0.3f;

    [Tooltip("Distancia mínima en unidades de mundo para considerar que es arrastre y no un clic.")]
    [SerializeField] private float dragDistanceThreshold = 0.05f;

    [Tooltip("¿Orientar el sprite hacia donde se arrastra el mouse?")]
    [SerializeField] private bool orientWithDrag = true;

    [Header("Caída")]
    [Tooltip("Si al soltarlo en el aire cae hasta el piso.")]
    [SerializeField] private bool fallToFloor = true;

    [Tooltip("Aceleración de la caída, en unidades de mundo por segundo al cuadrado.")]
    [SerializeField] private float gravity = 30f;

    private Camera cam;
    private SpriteRenderer sprite;
    private StripWalker walker;
    private Collider2D body;
    private ProceduralPetAnimator proceduralAnim;

    private bool isHolding;
    private Vector3 dragOffset;
    private Vector3 startCursorWorld;
    private float lastPosX;
    private float fallSpeed;

    public bool IsDragging { get; private set; }

    /// <summary>Si esta cayendo despues de que lo soltaran en el aire.</summary>
    public bool IsFalling { get; private set; }

    private void Awake()
    {
        cam = Camera.main;
        sprite = GetComponent<SpriteRenderer>();
        walker = GetComponent<StripWalker>();
        body = GetComponent<Collider2D>();
        proceduralAnim = GetComponent<ProceduralPetAnimator>();
        if (proceduralAnim == null) proceduralAnim = gameObject.AddComponent<ProceduralPetAnimator>();
    }

    private void Update()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null) return;
        if (cam == null) cam = Camera.main;

        // La caida sigue aunque se este acomodando la franja: si no, quedaria
        // colgado en el aire hasta que el jugador termine.
        if (IsFalling) Fall(window);

        Vector3 mouseWorld = window.CursorWorldPosition;

        // Mientras se acomoda la franja, arrastrar es mover la franja entera.
        if (LayoutEditor.IsEditing)
        {
            if (isHolding) EndDrag(window);
            return;
        }

        // Clic inicial sobre el personaje
        if (window.LeftPressedThisFrame && body.OverlapPoint(mouseWorld))
        {
            isHolding = true;
            IsDragging = false;
            window.IsDraggingContent = true;
            startCursorWorld = mouseWorld;
            dragOffset = transform.position - mouseWorld;
            lastPosX = transform.position.x;
            return;
        }

        if (!isHolding) return;

        // Si se mantiene presionado el botón
        if (window.IsLeftDown)
        {
            if (!IsDragging)
            {
                if (Vector2.Distance(startCursorWorld, mouseWorld) > dragDistanceThreshold)
                {
                    // Si lo agarran mientras cae, la caida se corta recien
                    // aca: un click sin arrastre lo deja seguir cayendo.
                    // Mientras se decidia, siguio cayendo: se lo agarra de
                    // donde esta ahora para que no pegue un salto.
                    if (IsFalling) dragOffset = transform.position - mouseWorld;

                    IsDragging = true;
                    IsFalling = false;
                    if (walker != null) walker.IsPaused = true;
                    if (proceduralAnim != null) proceduralAnim.OnGrab();
                }
            }

            // Movimiento y sincronización de dirección
            if (IsDragging)
            {
                Vector3 target = mouseWorld + dragOffset;
                Vector2 clamped = ClampToStage(window, target);

                // Orientar visualmente y sincronizar la dirección interna de StripWalker
                if (orientWithDrag)
                {
                    float deltaX = clamped.x - lastPosX;

                    if (deltaX > 0.01f)
                    {
                        // Se mueve a la derecha
                        if (walker != null) walker.SetDirection(1);
                        else if (sprite != null) sprite.flipX = false;
                    }
                    else if (deltaX < -0.01f)
                    {
                        // Se mueve a la izquierda
                        if (walker != null) walker.SetDirection(-1);
                        else if (sprite != null) sprite.flipX = true;
                    }
                }

                lastPosX = clamped.x;
                transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
            }
        }
        else
        {
            EndDrag(window);
        }
    }

    /// <summary>
    /// Donde puede estar mientras se lo arrastra: dentro de la franja (no de la
    /// camara: con el menu de la esquina abierto la ventana puede ser mas
    /// ancha), con los pies entre el piso y una altura en la que la cabeza
    /// todavia entra.
    /// </summary>
    private Vector2 ClampToStage(DesktopWindow window, Vector3 position)
    {
        Rect stage = window.StageWorldRect;
        float height = sprite != null ? sprite.bounds.size.y : 0f;

        float x = Mathf.Clamp(position.x, stage.xMin + edgePadding, stage.xMax - edgePadding);
        float y = Mathf.Clamp(position.y, window.FloorY, Mathf.Max(window.FloorY, stage.yMax - height));
        return new Vector2(x, y);
    }

    /// <summary>Cae con aceleracion hasta el piso y ahi vuelve a caminar.</summary>
    private void Fall(DesktopWindow window)
    {
        fallSpeed += gravity * Time.deltaTime;

        if (proceduralAnim != null)
        {
            proceduralAnim.OnFalling(fallSpeed);
        }

        Vector3 p = transform.position;
        p.y -= fallSpeed * Time.deltaTime;

        if (p.y <= window.FloorY)
        {
            p.y = window.FloorY;
            IsFalling = false;

            if (proceduralAnim != null)
            {
                proceduralAnim.OnLand(fallSpeed);
            }

            if (walker != null) walker.IsPaused = false;
        }

        transform.position = p;
    }

    private void EndDrag(DesktopWindow window)
    {
        isHolding = false;
        if (window != null) window.IsDraggingContent = false;

        if (!IsDragging) return;
        IsDragging = false;

        // Soltado en el aire: cae (y sigue en pausa hasta tocar el piso).
        bool inTheAir = window != null && transform.position.y > window.FloorY + 0.001f;
        if (fallToFloor && inTheAir)
        {
            IsFalling = true;
            fallSpeed = 0f;
            return;
        }

        if (proceduralAnim != null) proceduralAnim.ResetDeformation();
        if (walker != null) walker.IsPaused = false;
    }

    private void OnDisable()
    {
        if (DesktopWindow.Instance != null && isHolding)
        {
            EndDrag(DesktopWindow.Instance);
        }
    }
}
