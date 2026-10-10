using UnityEngine;

/// <summary>
/// Traslada al personaje a lo largo de la franja y lo da vuelta al llegar al
/// borde. Es la pieza que faltaba: MascotaController cicla animaciones pero
/// nunca movia nada, asi que "walk" se veia como caminar en el lugar.
///
/// Solo se mueve cuando el Animator esta en el estado de caminar. Es un
/// acople minimo y provisorio contra el parametro entero que ya existe: el
/// sistema de comportamientos del hito 1 lo va a reemplazar por algo donde el
/// comportamiento decide el movimiento y no al reves.
///
/// Si el objeto no tiene Animator (o su controller no tiene el parametro),
/// camina siempre. Eso es lo que hace andar al DebugWalker.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class StripWalker : MonoBehaviour
{
    [Tooltip("Unidades de mundo por segundo.")]
    [SerializeField] private float speed = 1.5f;

    [Tooltip("Cuanto antes del borde de la pantalla se da vuelta, en unidades de mundo.")]
    [SerializeField] private float edgePadding = 0.3f;

    [Header("Acople con el Animator")]
    [Tooltip("Parametro entero que dice que esta haciendo el personaje.")]
    [SerializeField] private string stateParameter = "Estado";

    [Tooltip("Valor de ese parametro que significa 'caminando'.")]
    [SerializeField] private int walkStateValue = 1;

    private SpriteRenderer sprite;
    private Animator animator;
    private Camera cam;
    private int direction = 1;
    private bool hasStateParameter;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        cam = Camera.main;

        if (cam == null)
            Debug.LogError("[StripWalker] No hay camara con tag MainCamera; no se puede " +
                           "calcular el borde de la franja.");
    }

    /// <summary>
    /// El parametro se busca en Start y NO en Awake. Animator.parameters devuelve
    /// un array vacio si se lo consulta en Awake, porque el Animator todavia no
    /// inicializo su controller: el chequeo daba false, el personaje caminaba
    /// siempre, y se movia tambien mientras leia.
    /// </summary>
    private void Start()
    {
        hasStateParameter = HasIntParameter(stateParameter);

        if (!hasStateParameter && animator != null)
            Debug.LogWarning($"[StripWalker] {name}: no encontre el parametro int " +
                             $"'{stateParameter}' (el Animator declara {animator.parameterCount}). " +
                             "Va a caminar todo el tiempo.");
    }

    public bool IsPaused { get; set; }
    private void Update()
    {
        if (cam == null) return;
        if (IsPaused) return;

        // La franja cambia de ancho en runtime (barra de tareas, o el jugador
        // la achica): los bordes se recalculan cada frame.
        GetWalkBounds(out float left, out float right);

        // Si la franja se achico y lo dejo afuera, vuelve al borde aunque este
        // quieto: si no, quedaria fuera de la ventana, invisible.
        Vector3 p = transform.position;
        if (p.x > right || p.x < left)
        {
            p.x = Mathf.Clamp(p.x, left, right);
            transform.position = p;
        }

        if (!ShouldWalk()) return;

        transform.position += Vector3.right * (direction * speed * Time.deltaTime);

        if (direction > 0 && transform.position.x >= right) Turn(-1, right);
        else if (direction < 0 && transform.position.x <= left) Turn(1, left);
    }

    /// <summary>
    /// Por donde puede caminar: el escenario de DesktopWindow, no lo que ve la
    /// camara. La ventana puede ser mas ancha que la franja mientras el menu de
    /// la esquina esta abierto.
    /// </summary>
    private void GetWalkBounds(out float left, out float right)
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window != null)
        {
            Rect stage = window.StageWorldRect;
            left = stage.xMin + edgePadding;
            right = stage.xMax - edgePadding;
            return;
        }

        float halfWidth = cam.orthographicSize * cam.aspect;
        left = cam.transform.position.x - halfWidth + edgePadding;
        right = cam.transform.position.x + halfWidth - edgePadding;
    }

    private bool ShouldWalk()
    {
        if (animator == null || !hasStateParameter) return true;
        return animator.GetInteger(stateParameter) == walkStateValue;
    }

    /// <summary>
    /// Preguntar una sola vez en Awake y no en cada frame: GetInteger contra un
    /// parametro que no existe llena la consola de warnings.
    /// </summary>
    private bool HasIntParameter(string name)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Int && p.name == name)
                return true;

        return false;
    }

    /// <summary>
    /// Cambia la dirección de marcha y actualiza flipX coherentemente.
    /// 1 = derecha, -1 = izquierda.
    /// </summary>
    public void SetDirection(int newDirection)
    {
        direction = newDirection >= 0 ? 1 : -1;
        if (sprite != null)
        {
            sprite.flipX = direction < 0;
        }
    }

    private void Turn(int newDirection, float clampX)
    {
        direction = newDirection;

        Vector3 p = transform.position;
        p.x = clampX;
        transform.position = p;

        // Se da vuelta con flipX y no con localScale.x negativo a proposito:
        // MascotaController.CambiarEscala() reescribe localScale en los tres ejes
        // y le borraria el flip.
        sprite.flipX = direction < 0;
    }
}
