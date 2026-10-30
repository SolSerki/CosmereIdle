using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fantasma procedural de Halloween (Fantasmaspren).
/// 
/// Características procedurales inspiradas en PetSprenEmitter y ProceduralPetAnimator:
/// - Flotación espectral orgánica mediante superposición armónica de ondas senoidales.
/// - Squash & Stretch procedural con conservación de volumen visual (X = 1 / Y) para la "respiración".
/// - Inclinación inercial (Tilt) según la dirección y velocidad del desplazamiento.
/// - Ciclo de sprites animado a partir del sheet de 8 frames (24x24).
/// - Estela de partículas de spren espectral generadas proceduralmente en memoria.
/// - Reacción elástica con resorte armónico amortiguado si el jugador le hace clic con el cursor.
/// - Desvanecimiento suave (fade in / fade out) al emerger y disiparse en el éter.
/// - Tamaño fijo en pantalla correspondiente a la resolución 0.75x, sin importar el escalado de personajes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class ProceduralGhost : MonoBehaviour
{
    [Header("Sprites y Animación de Cuadros")]
    [Tooltip("Los 8 cuadros del spritesheet del fantasma.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Velocidad de reproducción de la animación en cuadros por segundo.")]
    [SerializeField] private float animationFps = 8f;

    [Header("Escala y Volumen")]
    /// <summary>
    /// Escala de resolución de referencia (0.75x) donde el fantasma toma su tamaño base deseado.
    /// </summary>
    public const float ReferenceStageScale = 0.75f;

    /// <summary>
    /// Multiplicador de escala de diseño de personajes (4x).
    /// En resolución 0.75x, la escala base del fantasma en el mundo resultaba en 4f * 0.75f = 3f.
    /// </summary>
    public const float DesignBaseScale = 4f;

    /// <summary>
    /// Producto constante de escala en pantalla (4f * 0.75f * 0.75f = 2.25f).
    /// Al dividir este valor por el StageScale actual de la cámara, el tamaño resultante en pantalla
    /// se mantiene 100% idéntico y fijo al de la resolución 0.75x.
    /// </summary>
    public const float FixedScreenScaleProduct = DesignBaseScale * ReferenceStageScale * ReferenceStageScale; // 2.25f

    [Tooltip("Escala base del fantasma en el mundo (se recalcula dinámicamente para ser fija en pantalla a resolución 0.75x).")]
    [SerializeField] private Vector3 baseScale = new Vector3(3f, 3f, 1f);

    [Tooltip("Intensidad de la respiración / oscilación de volumen (Squash & Stretch).")]
    [SerializeField] private float breathIntensity = 0.08f;

    [Tooltip("Frecuencia de la respiración.")]
    [SerializeField] private float breathSpeed = 3.2f;

    [Header("Física de Flotación Espectral")]
    [Tooltip("Velocidad de traslación horizontal al flotar por la pantalla.")]
    [SerializeField] private float driftSpeed = 0.55f;

    [Tooltip("Amplitud del balanceo vertical (Hover).")]
    [SerializeField] private float hoverAmplitude = 0.07f;

    [Tooltip("Frecuencia del balanceo vertical.")]
    [SerializeField] private float hoverFrequency = 2.4f;

    [Tooltip("Amplitud del vaivén horizontal complementario (Sway).")]
    [SerializeField] private float swayAmplitude = 0.06f;

    [Tooltip("Frecuencia del vaivén horizontal.")]
    [SerializeField] private float swayFrequency = 1.6f;

    [Tooltip("Inclinación angular máxima hacia la dirección del movimiento.")]
    [SerializeField] private float maxTiltAngle = 8.5f;

    [Header("Ciclo de Vida")]
    [Tooltip("Duración total en segundos antes de disiparse.")]
    [SerializeField] private float lifetime = 4.8f;

    [Tooltip("Tiempo de aparición suave (Fade In).")]
    [SerializeField] private float fadeInTime = 0.65f;

    [Tooltip("Tiempo de desvanecimiento (Fade Out).")]
    [SerializeField] private float fadeOutTime = 0.85f;

    [Tooltip("Transparencia máxima del fantasma (0.85 = semi-traslúcido etéreo).")]
    [SerializeField] private float maxAlpha = 0.88f;

    [Header("Partículas de Spren Espectral")]
    [Tooltip("Intervalo entre emisión de chispas espectrales.")]
    [SerializeField] private float sprenEmitInterval = 0.22f;

    [Tooltip("Colores de las estelas fantasmales de Halloween (solo naranja claro y amarillo claro).")]
    [SerializeField] private Color[] ghostSprenColors = new Color[]
    {
        new Color(1f, 0.72f, 0.25f, 0.85f),  // Naranja claro suave
        new Color(1f, 0.92f, 0.45f, 0.85f)   // Amarillo claro luminoso
    };

    [Header("Reacción al Clic (Susto / Spook)")]
    [Tooltip("Stiffness del resorte elástico cuando el jugador le hace clic.")]
    [SerializeField] private float springStiffness = 140f;

    [Tooltip("Amortiguación del resorte al ser asustado.")]
    [SerializeField] private float springDamping = 8f;

    // Componentes internos
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;

    // Estado del movimiento
    private Vector3 anchorPosition;
    private int driftDirection = 1; // 1 = derecha, -1 = izquierda
    private float timeOffset;
    private float age = 0f;
    private bool isDissipating = false;

    // Dinámica de Squash & Stretch y Resorte (Spring)
    private float springDisplacement = 0f;
    private float springVelocity = 0f;
    private float currentTilt = 0f;

    // Límites de la pantalla/franja
    private Rect movementBounds;
    private bool hasCustomBounds = false;

    // Temporizador de estela de partículas
    private float sprenTimer = 0f;

    // Partículas activas de este fantasma
    private class SpectralSpren
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector3 position;
        public Vector2 velocity;
        public float lifeTime;
        public float maxLifeTime;
        public Color baseColor;
    }
    private readonly List<SpectralSpren> activeSpren = new List<SpectralSpren>();
    private static Transform sprenRoot;

    /// <summary>
    /// Calcula la escala en unidades de mundo para que el fantasma tenga SIEMPRE el mismo
    /// tamaño en pantalla que tiene en resolución 0.75x, sin importar qué escalado elija el usuario.
    /// </summary>
    public static float GetFixedWorldScale(float stageScale)
    {
        float safeScale = Mathf.Max(0.01f, stageScale);
        return FixedScreenScaleProduct / safeScale;
    }

    /// <summary>
    /// Factor de escala visual respecto a la referencia (0.75x / stageScale).
    /// Se usa para normalizar velocidades, amplitudes y offsets en espacio de mundo.
    /// </summary>
    public static float GetScaleFactor(float stageScale)
    {
        float safeScale = Mathf.Max(0.01f, stageScale);
        return ReferenceStageScale / safeScale;
    }

    private void UpdateBaseScale(float stageScale)
    {
        float s = GetFixedWorldScale(stageScale);
        baseScale = new Vector3(s, s, 1f);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Configurar collider para interacción con DesktopWindow y detección del mouse
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector2(0.24f, 0.24f); // 24x24 px a 100 PPU
        }

        timeOffset = Random.Range(0f, 100f);
        anchorPosition = transform.position;

        EnsureSprenRoot();
        EnsureHalloweenSprenColors();

        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(stageScale);
    }

    private void EnsureHalloweenSprenColors()
    {
        // Forzar exclusivamente la paleta suave de naranja claro y amarillo claro (sin rojos ni naranjas oscuros)
        bool needsReset = ghostSprenColors == null || ghostSprenColors.Length != 2;
        if (!needsReset)
        {
            for (int i = 0; i < ghostSprenColors.Length; i++)
            {
                if (ghostSprenColors[i].g < 0.65f) // Tono rojizo o naranja oscuro intrusivo
                {
                    needsReset = true;
                    break;
                }
            }
        }

        if (needsReset)
        {
            ghostSprenColors = new Color[]
            {
                new Color(1f, 0.72f, 0.25f, 0.85f),  // Naranja claro suave
                new Color(1f, 0.92f, 0.45f, 0.85f)   // Amarillo claro luminoso
            };
        }
    }

    private void Start()
    {
        // Si no se asignaron frames manualmente, cargarlos a través del fallback
        if (frames == null || frames.Length == 0)
        {
            frames = HalloweenGhostSpawner.GetGhostSprites();
        }

        // Color inicial completamente transparente
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
            spriteRenderer.sortingOrder = 16; // Flota por encima de las mascotas y fondos
        }

        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(stageScale);
    }

    private void OnDestroy()
    {
        // Limpiar partículas espectrales asociadas
        foreach (var s in activeSpren)
        {
            if (s.go != null) Destroy(s.go);
        }
        activeSpren.Clear();
    }

    /// <summary>
    /// Configura el fantasma al ser instanciado por el Spawner.
    /// </summary>
    public void Initialize(Vector3 spawnPosition, int initialDirection, float customLifetime, Rect bounds)
    {
        transform.position = spawnPosition;
        anchorPosition = spawnPosition;
        driftDirection = initialDirection != 0 ? initialDirection : (Random.value > 0.5f ? 1 : -1);
        lifetime = Mathf.Max(2.5f, customLifetime);
        movementBounds = bounds;
        hasCustomBounds = true;
        age = 0f;
        isDissipating = false;

        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(stageScale);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        // Mantener el tamaño fijo en pantalla sincronizado con el escalado actual de la franja
        float currentStageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(currentStageScale);

        // 1. Chequeo de interacción del mouse (clic para asustar al fantasma)
        CheckMouseInteraction();

        // 2. Animación de cuadros (Frame Cycling de los 8 frames del sheet)
        UpdateSpriteFrame();

        // 3. Movimiento y física espectral
        UpdateDrift(dt);
        UpdateSpringPhysics(dt);
        UpdateTransform();

        // 4. Alpha y desvanecimiento
        UpdateAlpha(dt);

        // 5. Emisión de estela de spren espectrales
        UpdateSpectralSpren(dt);

        // 6. Fin de ciclo de vida
        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void CheckMouseInteraction()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null)
        {
            // Fallback si no está DesktopWindow: clic estándar de Unity
            if (Input.GetMouseButtonDown(0) && Camera.main != null)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if (boxCollider != null && boxCollider.OverlapPoint(mousePos))
                {
                    OnSpook();
                }
            }
            return;
        }

        // Con DesktopWindow: interacción directa respetando la ventana transparente de Windows
        if (window.LeftPressedThisFrame && boxCollider != null && boxCollider.OverlapPoint(window.CursorWorldPosition))
        {
            OnSpook();
        }
    }

    /// <summary>
    /// Reacción de susto cuando el usuario hace clic sobre el fantasma:
    /// Da un salto elástico procedural, estalla en chispas de spren y acelera su flotación.
    /// </summary>
    public void OnSpook()
    {
        // Impulso elástico instantáneo en el resorte de escala Y
        springVelocity = 6.5f;

        // Estallido de partículas de spren espectrales
        BurstSpookySpren(6);

        // Ligera sorpresa: invierte la dirección o se eleva sutilmente sin superar el tope del 25%
        Rect bounds = GetCurrentBounds();
        float maxY = bounds.yMin + bounds.height * 0.25f;
        anchorPosition.y = Mathf.Min(anchorPosition.y + 0.03f, maxY);
        driftDirection = -driftDirection;
    }

    private void UpdateSpriteFrame()
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null) return;

        int frameIndex = Mathf.FloorToInt((Time.time * animationFps) % frames.Length);
        if (frameIndex >= 0 && frameIndex < frames.Length && frames[frameIndex] != null)
        {
            spriteRenderer.sprite = frames[frameIndex];
        }
    }

    private void UpdateDrift(float dt)
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        // 1. Traslación del ancla horizontal (normalizada a la velocidad visual a 0.75x)
        anchorPosition.x += driftDirection * (driftSpeed * scaleFactor) * dt;

        // Si está en fase de disipación, ascender suavemente como niebla pero contenido en el 25%
        isDissipating = age >= (lifetime - fadeOutTime);
        if (isDissipating)
        {
            Rect b = GetCurrentBounds();
            float maxY = b.yMin + b.height * 0.26f;
            anchorPosition.y = Mathf.Min(anchorPosition.y + (0.10f * scaleFactor) * dt, maxY);
        }

        // 2. Rebotar suavemente al acercarse a los bordes de la franja / pantalla
        Rect bounds = GetCurrentBounds();
        float halfWidth = (24f / 100f) * baseScale.x * 0.5f;
        float padding = halfWidth + 0.09f * scaleFactor;

        if (driftDirection > 0 && anchorPosition.x >= bounds.xMax - padding)
        {
            driftDirection = -1;
            anchorPosition.x = bounds.xMax - padding;
        }
        else if (driftDirection < 0 && anchorPosition.x <= bounds.xMin + padding)
        {
            driftDirection = 1;
            anchorPosition.x = bounds.xMin + padding;
        }
    }

    private void UpdateSpringPhysics(float dt)
    {
        // Resorte armónico amortiguado en la escala (Damped Harmonic Oscillator)
        float force = -springStiffness * springDisplacement - springDamping * springVelocity;
        springVelocity += force * dt;
        springDisplacement += springVelocity * dt;

        // Amortiguación de corte
        if (Mathf.Abs(springDisplacement) < 0.002f && Mathf.Abs(springVelocity) < 0.02f)
        {
            springDisplacement = 0f;
            springVelocity = 0f;
        }
    }

    private void UpdateTransform()
    {
        float t = Time.time + timeOffset;
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        // 1. Desplazamiento oscilatorio (Hover & Sway normalizado en pantalla a 0.75x)
        // Superposición de dos frecuencias senoidales para movimiento biológico y fluido
        float hoverY = (Mathf.Sin(t * hoverFrequency) * hoverAmplitude 
                     + Mathf.Sin(t * (hoverFrequency * 1.5f)) * (hoverAmplitude * 0.35f)) * scaleFactor;

        float swayX = Mathf.Cos(t * swayFrequency) * (swayAmplitude * scaleFactor);

        // Mantener la base del fantasma suavemente por encima de la barra de tareas y con tope en el 25%
        Rect bounds = GetCurrentBounds();
        float visibleHalfHeight = baseScale.y * (9.5f / 100f);
        float minSafeY = bounds.yMin + visibleHalfHeight + 0.02f * scaleFactor;
        float maxAllowedY = bounds.yMin + bounds.height * 0.25f;

        float finalY = Mathf.Clamp(anchorPosition.y + hoverY, minSafeY, maxAllowedY);
        transform.position = new Vector3(anchorPosition.x + swayX, finalY, anchorPosition.z);

        // 2. Inclinación inercial (Tilt) según la dirección de traslación
        float targetTilt = -driftDirection * maxTiltAngle;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * 4.5f);
        transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);

        // 3. Orientación del sprite (flipX) hacia donde se desplaza
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = driftDirection < 0;
        }

        // 4. Respiración procedural con conservación de volumen:
        // Ancho = 1 / Alto para preservar la masa visual orgánica
        float breath = Mathf.Sin(t * breathSpeed) * breathIntensity;
        float dynamicScaleY = 1f + breath + springDisplacement;
        dynamicScaleY = Mathf.Max(0.2f, dynamicScaleY);
        float dynamicScaleX = 1f / dynamicScaleY;

        // Multiplicar por escala de entrada si está apareciendo
        float enterFactor = 1f;
        if (age < fadeInTime)
        {
            float progress = age / fadeInTime;
            // Curva elástica de entrada
            enterFactor = Mathf.Lerp(0.35f, 1f, Mathf.Sin(progress * Mathf.PI * 0.5f));
        }

        transform.localScale = new Vector3(
            baseScale.x * dynamicScaleX * enterFactor,
            baseScale.y * dynamicScaleY * enterFactor,
            baseScale.z
        );
    }

    private void UpdateAlpha(float dt)
    {
        if (spriteRenderer == null) return;

        float alpha = maxAlpha;

        if (age < fadeInTime)
        {
            // Aparición gradual (Fade In)
            float t = Mathf.Clamp01(age / fadeInTime);
            alpha = Mathf.SmoothStep(0f, maxAlpha, t);
        }
        else if (age >= (lifetime - fadeOutTime))
        {
            // Desvanecimiento suave al disiparse (Fade Out)
            float t = Mathf.Clamp01((age - (lifetime - fadeOutTime)) / fadeOutTime);
            alpha = Mathf.SmoothStep(maxAlpha, 0f, t);
        }

        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    // ------------------------------------------------------------------
    // Sistema de Spren Espectral (Partículas Procedurales)
    // ------------------------------------------------------------------

    private void UpdateSpectralSpren(float dt)
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        sprenTimer += dt;
        if (sprenTimer >= sprenEmitInterval && age < (lifetime - fadeOutTime * 0.5f))
        {
            sprenTimer = 0f;
            EmitSpectralParticle();
        }

        // Actualizar partículas activas
        for (int i = activeSpren.Count - 1; i >= 0; i--)
        {
            var p = activeSpren[i];
            p.lifeTime += dt;

            if (p.lifeTime >= p.maxLifeTime)
            {
                if (p.go != null) Destroy(p.go);
                activeSpren.RemoveAt(i);
                continue;
            }

            float progress = p.lifeTime / p.maxLifeTime;

            // Ascenso suave con resistencia
            p.position += (Vector3)(p.velocity * dt);
            p.velocity.y += (0.2f * scaleFactor) * dt; // Flota hacia arriba
            p.go.transform.position = p.position;

            // Escala y desvanecimiento
            float scale = (2.4f * scaleFactor) * (1f - progress * 0.7f);
            p.go.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = p.baseColor;
            c.a = p.baseColor.a * (1f - progress);
            p.sr.color = c;
        }
    }

    private void EmitSpectralParticle()
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        // Posición en la cola o estela del fantasma
        Vector3 spawnPos = transform.position + new Vector3(
            Random.Range(-0.08f, 0.08f) * scaleFactor,
            Random.Range(-0.10f, 0.02f) * scaleFactor,
            0f
        );

        var p = CreateSprenParticle(spawnPos);
        p.velocity = new Vector2(
            (-driftDirection * Random.Range(0.1f, 0.35f) + Random.Range(-0.1f, 0.1f)) * scaleFactor,
            Random.Range(0.2f, 0.5f) * scaleFactor
        );
        p.maxLifeTime = Random.Range(0.55f, 0.85f);
        activeSpren.Add(p);
    }

    private void BurstSpookySpren(int count)
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + new Vector3(
                Random.Range(-0.12f, 0.12f) * scaleFactor, 
                Random.Range(-0.08f, 0.12f) * scaleFactor, 
                0f
            );
            var p = CreateSprenParticle(spawnPos);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(0.7f, 1.4f) * scaleFactor;
            p.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            p.maxLifeTime = Random.Range(0.5f, 0.8f);

            activeSpren.Add(p);
        }
    }

    private SpectralSpren CreateSprenParticle(Vector3 pos)
    {
        var go = new GameObject("SpectralSpren");
        go.transform.SetParent(sprenRoot);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        // Utiliza el generador de estrellas pixel art en memoria de PetSprenEmitter
        sr.sprite = PetSprenEmitter.GetOrCreateSprenSprite();
        sr.sortingOrder = 18; // Delante del fantasma

        Color col = ghostSprenColors[Random.Range(0, ghostSprenColors.Length)];
        sr.color = col;

        return new SpectralSpren
        {
            go = go,
            sr = sr,
            position = pos,
            lifeTime = 0f,
            baseColor = col
        };
    }

    private Rect GetCurrentBounds()
    {
        if (DesktopWindow.Instance != null)
        {
            return DesktopWindow.Instance.StageWorldRect;
        }

        if (hasCustomBounds) return movementBounds;

        if (Camera.main != null)
        {
            var cam = Camera.main;
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            return new Rect(-w * 0.5f, -h * 0.5f, w, h);
        }

        return new Rect(-5f, -1f, 10f, 2f);
    }

    private static void EnsureSprenRoot()
    {
        if (sprenRoot != null) return;
        var go = new GameObject("[HalloweenGhostSpren]");
        DontDestroyOnLoad(go);
        sprenRoot = go.transform;
    }
}