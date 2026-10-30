using UnityEngine;

/// <summary>
/// Animación procedural para la mascota:
/// - Squash & Stretch con conservación de volumen visual (X = 1 / Y).
/// - Rebote elástico amortiguado (Damped Harmonic Oscillator) al caer o soltar.
/// - Inercia, compresión horizontal e inclinación contra los bordes de la pantalla (Wall Bonk).
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

    [Header("Choque contra Borde (Wall Bonk)")]
    [Tooltip("Intensidad de compresión horizontal al chocar contra la pared del escenario.")]
    [SerializeField] private float wallSquashIntensity = 0.38f;

    [Tooltip("Inclinación angular hacia atrás al golpear la pared.")]
    [SerializeField] private float wallTiltAngle = 8f;

    private float currentScaleY = 1f;
    private float velocityY = 0f;
    private float currentTiltZ = 0f;
    private float tiltVelocity = 0f;
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
        currentTiltZ = 0f;
        tiltVelocity = 0f;
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
        currentTiltZ = 0f;
        tiltVelocity = 0f;
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
        currentTiltZ = 0f;
        tiltVelocity = 0f;
        ApplyScale();
    }

    /// <summary>
    /// Llamado cuando el personaje se choca contra el borde de la pantalla mientras camina.
    /// Aplasta horizontalmente e inclina el cuerpo hacia atrás.
    /// </summary>
    public void OnWallBonk(int hitDirection)
    {
        currentScaleY = 1f + wallSquashIntensity;
        velocityY = 0f;
        squashHoldTimer = squashHoldDuration;
        isSpringActive = true;

        currentTiltZ = -hitDirection * wallTiltAngle;
        tiltVelocity = 0f;

        ApplyScale();
    }

    /// <summary>
    /// Restablece la escala y rotación base del personaje sin oscilación.
    /// </summary>
    public void ResetDeformation()
    {
        isSpringActive = false;
        squashHoldTimer = 0f;
        velocityY = 0f;
        currentScaleY = 1f;
        currentTiltZ = 0f;
        tiltVelocity = 0f;
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.identity;
    }

    private void UpdateSpring(float dt)
    {
        // 1. Resorte armónico amortiguado en escala Y hacia target = 1.0f
        float displacement = currentScaleY - 1f;
        float force = -stiffness * displacement - damping * velocityY;
        velocityY += force * dt;
        currentScaleY += velocityY * dt;

        // 2. Resorte armónico amortiguado en rotación Z hacia target = 0f
        if (Mathf.Abs(currentTiltZ) > 0.01f || Mathf.Abs(tiltVelocity) > 0.01f)
        {
            float tiltForce = -stiffness * currentTiltZ - damping * tiltVelocity;
            tiltVelocity += tiltForce * dt;
            currentTiltZ += tiltVelocity * dt;
        }
        else
        {
            currentTiltZ = 0f;
            tiltVelocity = 0f;
        }

        // Corte cuando la oscilación y la inclinación son imperceptibles
        if (Mathf.Abs(displacement) < 0.002f && Mathf.Abs(velocityY) < 0.02f && Mathf.Abs(currentTiltZ) < 0.01f)
        {
            currentScaleY = 1f;
            velocityY = 0f;
            currentTiltZ = 0f;
            tiltVelocity = 0f;
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

        transform.localRotation = Quaternion.Euler(0f, 0f, currentTiltZ);
    }
}
