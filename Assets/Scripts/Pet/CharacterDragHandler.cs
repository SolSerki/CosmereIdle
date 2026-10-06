using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CharacterDragHandler : MonoBehaviour
{
    [Tooltip("Margen respecto a los bordes de la franja.")]
    [SerializeField] private float edgePadding = 0.3f;

    [Tooltip("Distancia mínima en unidades de mundo para considerar que es arrastre y no un clic.")]
    [SerializeField] private float dragDistanceThreshold = 0.05f;

    [Tooltip("¿Orientar el sprite hacia donde se arrastra el mouse?")]
    [SerializeField] private bool orientWithDrag = true;

    private Camera cam;
    private SpriteRenderer sprite;
    private StripWalker walker;
    private Collider2D body;

    private bool isHolding;
    private Vector3 dragOffset;
    private Vector3 startCursorWorld;
    private float fixedY;
    private float lastPosX;

    public bool IsDragging { get; private set; }

    private void Awake()
    {
        cam = Camera.main;
        sprite = GetComponent<SpriteRenderer>();
        walker = GetComponent<StripWalker>();
        body = GetComponent<Collider2D>();
    }

    private void Start()
    {
        fixedY = transform.position.y;
    }

    private void Update()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null) return;
        if (cam == null) cam = Camera.main;

        Vector3 mouseWorld = window.CursorWorldPosition;

        // Clic inicial sobre el personaje
        if (window.LeftPressedThisFrame && body.OverlapPoint(mouseWorld))
        {
            isHolding = true;
            IsDragging = false;
            window.IsDraggingContent = true;
            startCursorWorld = mouseWorld;
            dragOffset = transform.position - mouseWorld;
            fixedY = transform.position.y;
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
                    IsDragging = true;
                    if (walker != null) walker.IsPaused = true;
                }
            }

            // Movimiento y sincronización de dirección
            if (IsDragging)
            {
                float targetX = mouseWorld.x + dragOffset.x;

                // Límites de la cámara en la franja
                float halfWidth = cam.orthographicSize * cam.aspect;
                float left = cam.transform.position.x - halfWidth + edgePadding;
                float right = cam.transform.position.x + halfWidth - edgePadding;

                float clampedX = Mathf.Clamp(targetX, left, right);

                // Orientar visualmente y sincronizar la dirección interna de StripWalker
                if (orientWithDrag)
                {
                    float deltaX = clampedX - lastPosX;

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

                lastPosX = clampedX;
                transform.position = new Vector3(clampedX, fixedY, transform.position.z);
            }
        }
        else
        {
            EndDrag(window);
        }
    }

    private void EndDrag(DesktopWindow window)
    {
        isHolding = false;
        if (window != null) window.IsDraggingContent = false;

        if (IsDragging)
        {
            IsDragging = false;
            if (walker != null) walker.IsPaused = false;
        }
    }

    private void OnDisable()
    {
        if (DesktopWindow.Instance != null && isHolding)
        {
            EndDrag(DesktopWindow.Instance);
        }
    }
}