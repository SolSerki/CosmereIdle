using UnityEngine;

/// <summary>
/// Animación procedural para la mascota: Squash & Stretch y rebotes elásticos.
/// Controla la deformación elástica del sprite preservando su volumen visual
/// (cuando Y se achica, X se ensancha y viceversa).
///
/// Diseñado para funcionar con sprites cuyo pivot es Bottom Center (los pies
/// se mantienen fijos en el piso al aplastarse o estirarse).
/// </summary>
[DisallowMultipleComponent]
public class ProceduralPetAnimator : MonoBehaviour
{
    [Header("Escala Base")]
    [Tooltip("Escala original del personaje. En este proyecto los sprites están a 4x.")]
    [SerializeField] private Vector3 baseScale = new Vector3(4f, 4f, 4f);

    [Header("Resorte de Aterrizaje (Landing Spring)")]
    [Tooltip("Fuerza del resorte elástico. Más bajo = oscilación más lenta y apreciable.")]
    [SerializeField] private float stiffness = 120f;

    [Tooltip("Fricción del resorte. Más bajo = rebota más veces antes de detenerse.")]
    [SerializeField] private float damping = 7.5f;

    [Tooltip("Aplastamiento máximo al caer (0.45 = hasta 45% menos de alto).")]
    [SerializeField] private float maxLandingSquash = 0.45f;

    [Tooltip("Aplastamiento mínimo al tocar el piso para que siempre sea claramente visible.")]
    [SerializeField] private float minLandingSquash = 0.20f;

    [Tooltip("Tiempo en segundos que sostiene el aplastamiento antes de empezar el rebote (hit-pause de impacto).")]
    [SerializeField] private float squashHoldDuration = 0.07f;

    [Header("Estiramiento en el Aire (Fall Stretch)")]
    [Tooltip("Sensibilidad del estiramiento a la velocidad de caída.")]
    [SerializeField] private float fallStretchFactor = 0.025f;

    [Tooltip("Estiramiento vertical máximo en caída.")]
    [SerializeField] private float maxFallStretch = 0.4f;

    [Header("Arrastre con Mouse")]
    [Tooltip("Multiplicador vertical mientras se sostiene al personaje.")]
    [SerializeField] private float dragStretchY = 1.15f;

    private float currentScaleY = 1f;
    private float velocityY = 0f;
    private bool isSpringActive = false;
    private float squashHoldTimer = 0f;

    private void Awake()
    {
        // Si el objeto ya tiene una escala asignada en el prefab o escena, la usamos
        if (transform.localScale.sqrMagnitude > 0.001f)
        {
            baseScale = transform.localScale;
        }
    }

    private void Update()
    {
        if (isSpringActive)
        {
            // Pequeña pausa de impacto para que el ojo humano alcance a leer el aplastamiento
            if (squashHoldTimer > 0f)
            {
                squashHoldTimer -= Time.deltaTime;
                ApplyScale();
                return;
            }

            UpdateSpring(Time.deltaTime);
        }
    }

    /// <summary>
    /// Llamado cuando el jugador empieza a arrastrar al personaje.
    /// </summary>
    public void OnGrab()
    {
        isSpringActive = false;
        squashHoldTimer = 0f;
        velocityY = 0f;
        currentScaleY = dragStretchY;
        ApplyScale();
    }

    /// <summary>
    /// Llamado continuamente mientras el personaje cae por gravedad.
    /// </summary>
    public void OnFalling(float fallSpeed)
    {
        isSpringActive = false;
        squashHoldTimer = 0f;
        velocityY = 0f;

        float stretch = Mathf.Clamp(fallSpeed * fallStretchFactor, 0f, maxFallStretch);
        currentScaleY = 1f + stretch;
        ApplyScale();
    }

    /// <summary>
    /// Llamado en el instante del impacto contra el piso.
    /// </summary>
    public void OnLand(float impactSpeed)
    {
        float squash = Mathf.Clamp(impactSpeed * fallStretchFactor * 1.5f, minLandingSquash, maxLandingSquash);
        currentScaleY = 1f - squash; // Aplastamiento inmediato
        velocityY = 0f;
        squashHoldTimer = squashHoldDuration; // Pausa para congelar el impacto un instante
        isSpringActive = true;
        ApplyScale();
    }

    /// <summary>
    /// Restablece la escala base del personaje sin oscilación.
    /// </summary>
    public void ResetDeformation()
    {
        isSpringActive = false;
        squashHoldTimer = 0f;
        velocityY = 0f;
        currentScaleY = 1f;
        transform.localScale = baseScale;
    }

    private void UpdateSpring(float dt)
    {
        // Resorte armónico amortiguado hacia target = 1.0f (relativo a baseScale)
        float displacement = currentScaleY - 1f;
        float force = -stiffness * displacement - damping * velocityY;
        velocityY += force * dt;
        currentScaleY += velocityY * dt;

        // Corte cuando la oscilación es imperceptible
        if (Mathf.Abs(displacement) < 0.002f && Mathf.Abs(velocityY) < 0.02f)
        {
            currentScaleY = 1f;
            velocityY = 0f;
            isSpringActive = false;
        }

        ApplyScale();
    }

    private void ApplyScale()
    {
        float clampedY = Mathf.Max(0.2f, currentScaleY);
        // Conservación de volumen: ancho = 1 / alto
        float scaleMultiplierX = 1f / clampedY;

        transform.localScale = new Vector3(
            baseScale.x * scaleMultiplierX,
            baseScale.y * clampedY,
            baseScale.z
        );
    }
}
