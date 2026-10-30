using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bruja procedural de Halloween:
/// Vuela sobre su escoba a lo largo de la franja/pantalla en una altitud media-alta,
/// con ondulación armónica, estela mágica de chispas en la cola de la escoba,
/// animación del ciclo de vuelo a partir del spritesheet (9 frames de 32x32),
/// respuesta interactiva al clic del cursor y control de escala constante en pantalla.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class ProceduralWitch : MonoBehaviour
{
    public static event Action<ProceduralWitch> OnWitchDestroyed;

    [Header("Sprites y Animación de Vuelo")]
    [Tooltip("Los 9 cuadros del spritesheet de vuelo de la bruja.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Velocidad de reproducción de la animación (cuadros por segundo).")]
    [SerializeField] private float animationFps = 9f;

    [Header("Escala y Volumen")]
    public const float ReferenceStageScale = 0.75f;
    public const float DesignBaseScale = 4f;
    public const float FixedScreenScaleProduct = DesignBaseScale * ReferenceStageScale * ReferenceStageScale; // 2.25f

    [Tooltip("Escala base calculada para permanecer idéntica en pantalla a resolución 0.75x.")]
    [SerializeField] private Vector3 baseScale = new Vector3(3f, 3f, 1f);

    [Tooltip("Intensidad de oscilación sutil de respiración/vuelo.")]
    [SerializeField] private float breathIntensity = 0.05f;

    [Tooltip("Frecuencia de oscilación sutil.")]
    [SerializeField] private float breathSpeed = 3.0f;

    [Header("Física de Vuelo en Escoba")]
    [Tooltip("Velocidad de crucero horizontal de la bruja.")]
    [SerializeField] private float flySpeed = 1.15f;

    [Tooltip("Amplitud de la ondulación vertical al surcar el aire.")]
    [SerializeField] private float hoverAmplitude = 0.045f;

    [Tooltip("Frecuencia de la ondulación vertical.")]
    [SerializeField] private float hoverFrequency = 1.9f;

    [Tooltip("Amplitud del vaivén horizontal secundario.")]
    [SerializeField] private float swayAmplitude = 0.04f;

    [Tooltip("Frecuencia del vaivén horizontal secundario.")]
    [SerializeField] private float swayFrequency = 1.4f;

    [Tooltip("Inclinación angular máxima al volar.")]
    [SerializeField] private float maxTiltAngle = 7.5f;

    [Header("Ciclo de Vida")]
    [Tooltip("Duración en pantalla antes de disiparse.")]
    [SerializeField] private float lifetime = 7.5f;

    [Tooltip("Tiempo de aparición suave (Fade In).")]
    [SerializeField] private float fadeInTime = 0.7f;

    [Tooltip("Tiempo de desvanecimiento (Fade Out).")]
    [SerializeField] private float fadeOutTime = 0.9f;

    [Tooltip("Opacidad máxima de la bruja.")]
    [SerializeField] private float maxAlpha = 1.0f;

    [Header("Estela Mágica de Escoba")]
    [Tooltip("Intervalo entre emisión de partículas de la estela.")]
    [SerializeField] private float trailEmitInterval = 0.16f;

    [Tooltip("Colores mágicos de la estela de bruja (violeta brillante, verde espectral y ámbar luminoso).")]
    [SerializeField] private Color[] witchTrailColors = new Color[]
    {
        new Color(0.78f, 0.38f, 1f, 0.9f),     // Violeta mágico
        new Color(0.35f, 0.95f, 0.55f, 0.88f), // Verde hechicero
        new Color(1f, 0.85f, 0.32f, 0.9f)      // Amarillo/ámbar estelar
    };

    [Header("Reacción al Clic")]
    [Tooltip("Fuerza del resorte elástico al hacer clic.")]
    [SerializeField] private float springStiffness = 150f;

    [Tooltip("Amortiguación del resorte.")]
    [SerializeField] private float springDamping = 8.5f;

    // Componentes internos
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;

    // Estado de movimiento
    private Vector3 anchorPosition;
    private int flyDirection = 1; // 1 = derecha, -1 = izquierda
    private float timeOffset;
    private float age = 0f;
    private bool isDissipating = false;

    // Dinámica elástica
    private float springDisplacement = 0f;
    private float springVelocity = 0f;
    private float currentTilt = 0f;

    // Límites de vuelo
    private Rect flightBounds;
    private bool hasCustomBounds = false;

    // Partículas de estela
    private float trailTimer = 0f;
    private class MagicalTrailSpren
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector3 position;
        public Vector2 velocity;
        public float lifeTime;
        public float maxLifeTime;
        public Color baseColor;
    }
    private readonly List<MagicalTrailSpren> activeParticles = new List<MagicalTrailSpren>();
    private static Transform trailRoot;

    public static float GetFixedWorldScale(float stageScale)
    {
        float safeScale = Mathf.Max(0.01f, stageScale);
        return FixedScreenScaleProduct / safeScale;
    }

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

        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector2(0.32f, 0.32f); // 32x32 px a 100 PPU
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 17;
            spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            spriteRenderer.enabled = true;
        }

        timeOffset = UnityEngine.Random.Range(0f, 100f);
        anchorPosition = transform.position;

        EnsureTrailRoot();
        EnsureFrames();

        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(stageScale);
    }

    private void EnsureFrames()
    {
        bool needsLoad = frames == null || frames.Length == 0 || frames.All(f => f == null);
        if (needsLoad)
        {
            frames = HalloweenWitchSpawner.GetWitchSprites();
        }

        if (frames != null && frames.Length > 0 && frames[0] != null && spriteRenderer != null)
        {
            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = frames[0];
            }
        }
    }

    private void Start()
    {
        EnsureFrames();

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 17;
            spriteRenderer.enabled = true;
        }

        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(stageScale);
    }

    private void OnDestroy()
    {
        foreach (var p in activeParticles)
        {
            if (p.go != null) Destroy(p.go);
        }
        activeParticles.Clear();

        OnWitchDestroyed?.Invoke(this);
    }

    public void Initialize(Vector3 spawnPosition, int initialDirection, float customLifetime, Rect bounds)
    {
        transform.position = spawnPosition;
        anchorPosition = spawnPosition;
        flyDirection = initialDirection != 0 ? initialDirection : (UnityEngine.Random.value > 0.5f ? 1 : -1);
        lifetime = Mathf.Max(3f, customLifetime);
        flightBounds = bounds;
        hasCustomBounds = true;
        age = 0f;
        isDissipating = false;

        EnsureFrames();

        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(stageScale);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        float currentStageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        UpdateBaseScale(currentStageScale);

        // 1. Detección de clic del usuario
        CheckMouseInteraction();

        // 2. Animación de sprites (9 cuadros de vuelo)
        UpdateSpriteFrame();

        // 3. Trayectoria y física de vuelo
        UpdateFlight(dt);
        UpdateSpringPhysics(dt);
        UpdateTransform();

        // 4. Alpha y desvanecimiento
        UpdateAlpha(dt);

        // 5. Estela mágica de escoba
        UpdateMagicalTrail(dt);

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
            if (Input.GetMouseButtonDown(0) && Camera.main != null)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if (boxCollider != null && boxCollider.OverlapPoint(mousePos))
                {
                    OnClicked();
                }
            }
            return;
        }

        if (window.LeftPressedThisFrame && boxCollider != null && boxCollider.OverlapPoint(window.CursorWorldPosition))
        {
            OnClicked();
        }
    }

    /// <summary>
    /// Reacción al clic: pirueta/salto elástico y ráfaga de chispas mágicas.
    /// </summary>
    public void OnClicked()
    {
        springVelocity = 7.0f;
        BurstMagicalSpren(8);
        flyDirection = -flyDirection;
    }

    private void UpdateSpriteFrame()
    {
        if (frames == null || frames.Length == 0 || frames.All(f => f == null))
        {
            EnsureFrames();
            if (frames == null || frames.Length == 0) return;
        }

        int frameIndex = Mathf.FloorToInt((Time.time * animationFps) % frames.Length);
        if (frameIndex >= 0 && frameIndex < frames.Length && frames[frameIndex] != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = frames[frameIndex];
        }
    }

    private void UpdateFlight(float dt)
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        // Traslación horizontal de la escoba
        anchorPosition.x += flyDirection * (flySpeed * scaleFactor) * dt;

        // Disipación al final del recorrido
        isDissipating = age >= (lifetime - fadeOutTime);
        if (isDissipating)
        {
            Rect b = GetCurrentBounds();
            float topY = b.yMin + b.height * 0.35f;
            anchorPosition.y = Mathf.Min(anchorPosition.y + (0.08f * scaleFactor) * dt, topY);
        }

        // Rebote suave al alcanzar los márgenes del escenario
        Rect bounds = GetCurrentBounds();
        float halfWidth = (32f / 100f) * baseScale.x * 0.5f;
        float padding = halfWidth + 0.1f * scaleFactor;

        if (flyDirection > 0 && anchorPosition.x >= bounds.xMax - padding)
        {
            flyDirection = -1;
            anchorPosition.x = bounds.xMax - padding;
        }
        else if (flyDirection < 0 && anchorPosition.x <= bounds.xMin + padding)
        {
            flyDirection = 1;
            anchorPosition.x = bounds.xMin + padding;
        }
    }

    private void UpdateSpringPhysics(float dt)
    {
        float force = -springStiffness * springDisplacement - springDamping * springVelocity;
        springVelocity += force * dt;
        springDisplacement += springVelocity * dt;

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

        // Ondulación de vuelo (Hovering y Swaying)
        float hoverY = (Mathf.Sin(t * hoverFrequency) * hoverAmplitude 
                     + Mathf.Sin(t * (hoverFrequency * 1.6f)) * (hoverAmplitude * 0.3f)) * scaleFactor;

        float swayX = Mathf.Cos(t * swayFrequency) * (swayAmplitude * scaleFactor);

        // Delimitar dentro del área visible justo arriba de los personajes
        Rect bounds = GetCurrentBounds();
        float minSafeY = bounds.yMin + bounds.height * 0.15f;
        float maxAllowedY = bounds.yMin + bounds.height * 0.35f;

        float finalY = Mathf.Clamp(anchorPosition.y + hoverY, minSafeY, maxAllowedY);
        transform.position = new Vector3(anchorPosition.x + swayX, finalY, anchorPosition.z);

        // Inclinación aerodinámica de la escoba
        float targetTilt = -flyDirection * maxTiltAngle;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * 5f);
        transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);

        // Orientación: el sheet original mira a la derecha
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = flyDirection < 0;
        }

        // Squash & Stretch dinámico
        float breath = Mathf.Sin(t * breathSpeed) * breathIntensity;
        float dynamicScaleY = 1f + breath + springDisplacement;
        dynamicScaleY = Mathf.Max(0.2f, dynamicScaleY);
        float dynamicScaleX = 1f / dynamicScaleY;

        float enterFactor = 1f;
        if (age < fadeInTime)
        {
            float progress = age / fadeInTime;
            enterFactor = Mathf.Lerp(0.4f, 1f, Mathf.Sin(progress * Mathf.PI * 0.5f));
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
            float t = Mathf.Clamp01(age / fadeInTime);
            alpha = Mathf.SmoothStep(0f, maxAlpha, t);
        }
        else if (age >= (lifetime - fadeOutTime))
        {
            float t = Mathf.Clamp01((age - (lifetime - fadeOutTime)) / fadeOutTime);
            alpha = Mathf.SmoothStep(maxAlpha, 0f, t);
        }

        spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
    }

    // ------------------------------------------------------------------
    // Estela Mágica de Escoba (Partículas)
    // ------------------------------------------------------------------

    private void UpdateMagicalTrail(float dt)
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        trailTimer += dt;
        if (trailTimer >= trailEmitInterval && age < (lifetime - fadeOutTime * 0.5f))
        {
            trailTimer = 0f;
            EmitTrailParticle();
        }

        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            var p = activeParticles[i];
            p.lifeTime += dt;

            if (p.lifeTime >= p.maxLifeTime)
            {
                if (p.go != null) Destroy(p.go);
                activeParticles.RemoveAt(i);
                continue;
            }

            float progress = p.lifeTime / p.maxLifeTime;
            p.position += (Vector3)(p.velocity * dt);
            p.velocity.y += (0.15f * scaleFactor) * dt;
            p.go.transform.position = p.position;

            float scale = (2.6f * scaleFactor) * (1f - progress * 0.7f);
            p.go.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = p.baseColor;
            c.a = p.baseColor.a * (1f - progress);
            p.sr.color = c;
        }
    }

    private void EmitTrailParticle()
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        // Posición en la cola de la escoba (detrás del sentido de avance)
        float tailOffsetX = -flyDirection * (0.18f * scaleFactor);
        Vector3 spawnPos = transform.position + new Vector3(
            tailOffsetX + UnityEngine.Random.Range(-0.04f, 0.04f) * scaleFactor,
            UnityEngine.Random.Range(-0.06f, 0.04f) * scaleFactor,
            0f
        );

        var p = CreateParticle(spawnPos);
        p.velocity = new Vector2(
            (-flyDirection * UnityEngine.Random.Range(0.2f, 0.5f) + UnityEngine.Random.Range(-0.1f, 0.1f)) * scaleFactor,
            UnityEngine.Random.Range(-0.1f, 0.3f) * scaleFactor
        );
        p.maxLifeTime = UnityEngine.Random.Range(0.6f, 0.95f);
        activeParticles.Add(p);
    }

    private void BurstMagicalSpren(int count)
    {
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ReferenceStageScale;
        float scaleFactor = GetScaleFactor(stageScale);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + new Vector3(
                UnityEngine.Random.Range(-0.14f, 0.14f) * scaleFactor,
                UnityEngine.Random.Range(-0.10f, 0.14f) * scaleFactor,
                0f
            );
            var p = CreateParticle(spawnPos);

            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(0.8f, 1.6f) * scaleFactor;
            p.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            p.maxLifeTime = UnityEngine.Random.Range(0.55f, 0.85f);

            activeParticles.Add(p);
        }
    }

    private MagicalTrailSpren CreateParticle(Vector3 pos)
    {
        var go = new GameObject("WitchSpren");
        go.transform.SetParent(trailRoot);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PetSprenEmitter.GetOrCreateSprenSprite();
        sr.sortingOrder = 18;

        Color col = witchTrailColors[UnityEngine.Random.Range(0, witchTrailColors.Length)];
        sr.color = col;

        return new MagicalTrailSpren
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

        if (hasCustomBounds) return flightBounds;

        if (Camera.main != null)
        {
            var cam = Camera.main;
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            return new Rect(-w * 0.5f, -h * 0.5f, w, h);
        }

        return new Rect(-5f, -1f, 10f, 2f);
    }

    private static void EnsureTrailRoot()
    {
        if (trailRoot != null) return;
        var go = new GameObject("[HalloweenWitchTrail]");
        DontDestroyOnLoad(go);
        trailRoot = go.transform;
    }
}
