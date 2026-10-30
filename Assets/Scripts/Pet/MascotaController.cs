using System.Collections;
using UnityEngine;

/// <summary>
/// Que hace el personaje cuando nadie lo toca. Hay cuatro modos, que se eligen
/// por personaje con click derecho (ver PetModeMenu) y se guardan:
///
///   Todos     - alterna solo entre quedarse quieto, caminar y leer un rato.
///   Descanso  - se queda quieto.
///   Lectura   - se sienta a leer y se queda leyendo; pasa la pagina cada
///               tanto (lo hace la animacion "Read Loop").
///   Caminata  - camina de una punta a la otra de la franja (StripWalker lo
///               mueve y lo da vuelta en los bordes).
///
/// Se maneja con el parametro entero "Estado" del Animator. Al cambiar de modo
/// primero lo deja quieto (frena si venia caminando, guarda el libro si estaba
/// leyendo) y recien ahi arranca el nuevo: las transiciones del Animator salen
/// casi todas de "Idle 0".
///
/// Con el cursor encima (o con su menu de modos abierto) se queda quieto, para
/// que sea facil hacerle click: deja lo que estaba haciendo y pasa a idle (si
/// estaba leyendo, guarda el libro). Cuando el cursor se va, sigue con lo suyo.
/// </summary>
public class MascotaController : MonoBehaviour
{
    public enum Mode { Todos, Descanso, Lectura, Caminata }

    // Valores del parametro "Estado".
    private const int Idle = 0;
    private const int Walk = 1;
    private const int ReadOnce = 2;   // Saca el libro, lee y lo guarda, en loop (modo Todos).
    private const int Brake = 3;      // Frena despues de caminar.
    private const int ReadStay = 4;   // Saca el libro y se queda leyendo (modo Lectura).

    private const string IdleState = "Idle 0";
    private const string ModeKeyPrefix = "CosmereIdle_Mode_";

    [Tooltip("Modo Todos: cuanto dura cada cosa que hace, como minimo.")]
    public float tiempoMinimo = 2f;

    [Tooltip("Modo Todos: cuanto dura cada cosa que hace, como maximo.")]
    public float tiempoMaximo = 5f;

    [Tooltip("Cuanto frena (Idle Side) despues de caminar.")]
    [SerializeField] private float brakeSeconds = 2f;

    [Tooltip("Cuanto espera despues de que el cursor se va para seguir con lo suyo. " +
             "Evita que arranque y frene si el cursor apenas lo roza.")]
    [SerializeField] private float resumeDelay = 0.4f;

    public Mode CurrentMode { get; private set; } = Mode.Todos;

    /// <summary>Si esta quieto porque el cursor esta encima (o su menu abierto).</summary>
    public bool IsHeld { get; private set; }

    private Animator animator;
    private Collider2D body;
    private bool started;
    private float lastHoveredAt;

    /// <summary>Se guarda por personaje: cada uno recuerda su modo.</summary>
    private string ModeKey => ModeKeyPrefix + name.Replace("(Clone)", "").Trim();

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (!started || animator == null) return;

        if (WantsToHold())
        {
            lastHoveredAt = Time.time;
            if (!IsHeld) Hold();
        }
        else if (IsHeld && Time.time - lastHoveredAt >= resumeDelay)
        {
            IsHeld = false;
            Restart();
        }
    }

    /// <summary>Con el cursor encima, o con su menu de modos abierto.</summary>
    private bool WantsToHold()
    {
        if (PetModeMenu.IsOpenFor(this)) return true;

        DesktopWindow window = DesktopWindow.Instance;
        return window != null
               && window.Mode == DesktopWindow.WindowMode.Strip
               && !LayoutEditor.IsEditing
               && body != null
               && body.OverlapPoint(window.CursorWorldPosition);
    }

    /// <summary>
    /// Lo frena donde esta y lo pasa a idle, haga lo que haga: si estaba
    /// leyendo, la animacion guarda el libro antes de quedar quieto.
    /// </summary>
    private void Hold()
    {
        IsHeld = true;
        StopAllCoroutines();
        animator.SetInteger("Estado", Idle);
    }

    /// <summary>
    /// El modo se lee aca y no en Awake: el nombre del personaje (con el que se
    /// guarda) se pone despues de instanciarlo.
    /// </summary>
    private void Start()
    {
        int saved = PlayerPrefs.GetInt(ModeKey, (int)Mode.Todos);
        CurrentMode = System.Enum.IsDefined(typeof(Mode), saved) ? (Mode)saved : Mode.Todos;
        Restart();
    }

    /// <summary>
    /// Deshabilitar el componente no frena las corrutinas (solo desactivar el
    /// objeto lo hace): el selector de personajes lo deshabilita para dejarlo
    /// congelado, asi que hay que frenarlas a mano.
    /// </summary>
    private void OnDisable()
    {
        StopAllCoroutines();
        IsHeld = false;
    }

    private void OnEnable()
    {
        // Rehabilitado despues de Start: vuelve a su modo.
        if (started) Restart();
    }

    /// <summary>Cambia el modo de este personaje y lo guarda.</summary>
    public void SetMode(Mode mode)
    {
        if (mode == CurrentMode && started) return;

        CurrentMode = mode;
        PlayerPrefs.SetInt(ModeKey, (int)mode);
        PlayerPrefs.Save();
        Restart();
    }

    /// <summary>
    /// Arranca el modo actual de cero. Si esta quieto por el cursor no arranca
    /// nada: lo hace solo cuando el cursor se va (asi un modo elegido desde el
    /// menu empieza recien al soltarlo).
    /// </summary>
    private void Restart()
    {
        started = true;
        StopAllCoroutines();
        if (IsHeld) return;
        if (animator != null && isActiveAndEnabled) StartCoroutine(Run(CurrentMode));
    }

    private IEnumerator Run(Mode mode)
    {
        yield return Settle(mode);

        switch (mode)
        {
            case Mode.Descanso:
                animator.SetInteger("Estado", Idle);
                break;

            case Mode.Lectura:
                animator.SetInteger("Estado", ReadStay);
                break;

            case Mode.Caminata:
                animator.SetInteger("Estado", Walk);
                break;

            default:
                yield return RutinaMascota();
                break;
        }
    }

    /// <summary>
    /// Lo deja quieto en "Idle 0" antes de arrancar el modo nuevo: frena si
    /// venia caminando, y guarda el libro si estaba leyendo (salvo que el modo
    /// nuevo sea seguir leyendo).
    /// </summary>
    private IEnumerator Settle(Mode next)
    {
        int estado = animator.GetInteger("Estado");
        if (estado == ReadStay && next == Mode.Lectura) yield break;

        if (estado == Walk)
        {
            animator.SetInteger("Estado", Brake);
            yield return new WaitForSeconds(brakeSeconds * 0.5f);
        }

        animator.SetInteger("Estado", Idle);

        // Guardar el libro lleva un rato; el resto de las transiciones a Idle
        // son inmediatas. Con un tope, por si algun controller no tiene el estado.
        float giveUpAt = Time.time + 5f;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(IdleState) && Time.time < giveUpAt)
            yield return null;
    }

    /// <summary>Modo Todos: alterna solo entre quieto, caminar y leer un rato.</summary>
    private IEnumerator RutinaMascota()
    {
        while (true)
        {
            // 1. Siempre vuelve a Idle para resetear el Animator.
            animator.SetInteger("Estado", Idle);

            // Se queda quieto un momento.
            yield return new WaitForSeconds(1.5f);

            // 2. Elige que hacer: caminar o leer. Random.Range con enteros
            // excluye el maximo, asi que (1, 3) da 1 o 2.
            int estadoAleatorio = Random.Range(Walk, ReadOnce + 1);
            animator.SetInteger("Estado", estadoAleatorio);

            // 3. Lo hace un tiempo al azar.
            yield return new WaitForSeconds(Random.Range(tiempoMinimo, tiempoMaximo));

            // 4. Si estuvo caminando, frena antes de volver a quedarse quieto.
            if (estadoAleatorio == Walk)
            {
                animator.SetInteger("Estado", Brake);
                yield return new WaitForSeconds(brakeSeconds);
            }
        }
    }

    public void CambiarEscala(float multiplicador)
    {
        transform.localScale = new Vector3(multiplicador, multiplicador, multiplicador);
    }
}
