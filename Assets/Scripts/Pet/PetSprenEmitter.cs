using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema procedural de Spren del Cosmere (Alegrispren, Logispren y Chispas de Choque).
/// Genera partículas pixel art en forma de estrella destellante (✨) en memoria,
/// y anima su comportamiento mediante trayectorias físicas, rotación continua y órbitas en 3D.
/// </summary>
[DisallowMultipleComponent]
public class PetSprenEmitter : MonoBehaviour
{
    [Header("Alegrispren (Al acariciar / hacer clic)")]
    [Tooltip("Habilita o deshabilita la emisión de estrellas doradas al hacer clic.")]
    [SerializeField] private bool enableJoyspren = false;

    [Tooltip("Cantidad de estrellas doradas expulsadas por clic.")]
    [SerializeField] private int joysprenCount = 5;

    [Tooltip("Colores de los alegrispren (tonos cálidos y dorados).")]
    [SerializeField] private Color[] joysprenColors = new Color[]
    {
        new Color(1f, 0.85f, 0.2f, 1f),   // Oro brillante
        new Color(1f, 0.95f, 0.55f, 1f),  // Destello blanco cálido
        new Color(1f, 0.68f, 0.15f, 1f)   // Ámbar
    };

    [Header("Logispren (Al leer)")]
    [Tooltip("Cantidad de logispren geométricos que orbitan.")]
    [SerializeField] private int logisprenCount = 3;

    [Tooltip("Colores de los logispren (azul zafiro y celeste).")]
    [SerializeField] private Color[] logisprenColors = new Color[]
    {
        new Color(0.4f, 0.85f, 1f, 0.95f),  // Zafiro eléctrico
        new Color(0.75f, 0.95f, 1f, 0.95f)  // Resplandor celeste
    };

    [Tooltip("Radio horizontal de la órbita de lectura.")]
    [SerializeField] private float orbitRadiusX = 0.42f;

    [Tooltip("Radio vertical de la órbita.")]
    [SerializeField] private float orbitRadiusY = 0.18f;

    [Tooltip("Velocidad angular de la órbita.")]
    [SerializeField] private float orbitSpeed = 2.5f;

    // Sprite de 5x5 generado en memoria con silueta de estrella destellante
    private static Sprite sprenSprite;

    // Contenedor global para que los spren no se distorsionen con el squash del personaje
    private static Transform sprenRoot;

    private Animator animator;
    private Collider2D body;

    // Pool y lista activa de Alegrispren
    private class JoyParticle
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector3 position;
        public Vector2 velocity;
        public float wobbleFreq;
        public float wobbleAmp;
        public float lifeTime;
        public float maxLifeTime;
        public float rotationZ;
        public float spinSpeed;
        public Color baseColor;
    }

    private readonly List<JoyParticle> activeJoyspren = new List<JoyParticle>();
    private readonly Stack<JoyParticle> joysprenPool = new Stack<JoyParticle>();

    // Logispren fijos para la órbita de lectura
    private class LogiParticle
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float phase;
        public float bobFreq;
        public Color baseColor;
    }

    private readonly List<LogiParticle> logispren = new List<LogiParticle>();
    private float logiAlpha = 0f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<Collider2D>();

        EnsureRoot();
        InitLogispren();
    }

    private void OnDestroy()
    {
        // Limpiar GameObjects asociados al destruirse la mascota
        foreach (var p in activeJoyspren) if (p.go != null) Destroy(p.go);
        foreach (var p in joysprenPool) if (p.go != null) Destroy(p.go);
        foreach (var p in logispren) if (p.go != null) Destroy(p.go);
        activeJoyspren.Clear();
        joysprenPool.Clear();
        logispren.Clear();
    }

    private void Update()
    {
        UpdateJoyspren(Time.deltaTime);
        UpdateLogispren(Time.deltaTime);
    }

    // ------------------------------------------------------------------
    // Alegrispren (Joyspren)
    // ------------------------------------------------------------------

    /// <summary>
    /// Hace brotar un estallido de estrellas doradas de felicidad alrededor de la cabeza.
    /// </summary>
    public void SpawnJoyspren()
    {
        if (!enableJoyspren) return;

        Vector3 head = GetHeadPosition();

        for (int i = 0; i < joysprenCount; i++)
        {
            JoyParticle p = GetJoyParticle();

            // Desfase aleatorio alrededor de la cabeza
            p.position = head + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.05f, 0.1f), 0f);
            
            // Velocidad: arco ascendente en abanico
            float angle = Random.Range(60f, 120f) * Mathf.Deg2Rad;
            float speed = Random.Range(0.9f, 1.6f);
            p.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

            p.wobbleFreq = Random.Range(8f, 14f);
            p.wobbleAmp = Random.Range(0.2f, 0.5f);
            p.lifeTime = 0f;
            p.maxLifeTime = Random.Range(0.65f, 0.95f);
            p.rotationZ = Random.Range(0f, 360f);
            p.spinSpeed = Random.Range(-150f, 150f);
            p.baseColor = joysprenColors[Random.Range(0, joysprenColors.Length)];

            p.go.transform.position = p.position;
            p.go.transform.rotation = Quaternion.Euler(0f, 0f, p.rotationZ);
            p.sr.color = p.baseColor;
            p.sr.sortingOrder = 15; // Por encima de la mascota
            p.go.SetActive(true);

            activeJoyspren.Add(p);
        }
    }

    /// <summary>
    /// Spawnea estrellitas que saltan de la cabeza cuando la mascota se choca contra la pared.
    /// </summary>
    public void SpawnBonkStars(int hitDirection)
    {
        Vector3 head = GetHeadPosition();

        for (int i = 0; i < 3; i++)
        {
            JoyParticle p = GetJoyParticle();
            p.position = head + new Vector3(hitDirection * 0.08f, Random.Range(0f, 0.08f), 0f);

            // Salto en arco hacia el lado opuesto al choque (hacia adentro de la pantalla)
            float angle = (hitDirection > 0 ? Random.Range(120f, 160f) : Random.Range(20f, 60f)) * Mathf.Deg2Rad;
            float speed = Random.Range(1.1f, 1.8f);
            p.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

            p.wobbleFreq = 12f;
            p.wobbleAmp = 0.15f;
            p.lifeTime = 0f;
            p.maxLifeTime = Random.Range(0.65f, 0.85f); // Suficiente tiempo para apreciarlas claramente
            p.rotationZ = Random.Range(0f, 360f);
            p.spinSpeed = Random.Range(-250f, 250f);
            p.baseColor = new Color(1f, 0.95f, 0.55f, 1f); // Estrellas doradas brillantes

            p.go.transform.position = p.position;
            p.go.transform.rotation = Quaternion.Euler(0f, 0f, p.rotationZ);
            p.sr.color = p.baseColor;
            p.sr.sortingOrder = 15;
            p.go.SetActive(true);

            activeJoyspren.Add(p);
        }
    }

    private void UpdateJoyspren(float dt)
    {
        for (int i = activeJoyspren.Count - 1; i >= 0; i--)
        {
            var p = activeJoyspren[i];
            p.lifeTime += dt;

            if (p.lifeTime >= p.maxLifeTime)
            {
                p.go.SetActive(false);
                activeJoyspren.RemoveAt(i);
                joysprenPool.Push(p);
                continue;
            }

            float progress = p.lifeTime / p.maxLifeTime;

            // Movimiento ondulatorio senoidal
            p.position.y += p.velocity.y * dt;
            p.position.x += (p.velocity.x + Mathf.Sin(p.lifeTime * p.wobbleFreq) * p.wobbleAmp) * dt;
            p.velocity.y *= Mathf.Exp(-1.5f * dt); // Suave resistencia

            // Rotación continua de estrella
            p.rotationZ += p.spinSpeed * dt;

            p.go.transform.position = p.position;
            p.go.transform.rotation = Quaternion.Euler(0f, 0f, p.rotationZ);

            // Escala pixel art dinámica (curva suave de aparición y contracción)
            float scaleCurve = Mathf.Sin(progress * Mathf.PI);
            float scale = 3.5f * Mathf.Lerp(0.6f, 1.25f, scaleCurve);
            p.go.transform.localScale = new Vector3(scale, scale, 1f);

            // Desvanecimiento suave
            Color c = p.baseColor;
            c.a = p.baseColor.a * (1f - progress);
            p.sr.color = c;
        }
    }

    private JoyParticle GetJoyParticle()
    {
        if (joysprenPool.Count > 0)
            return joysprenPool.Pop();

        var go = new GameObject("Alegrispren");
        go.transform.SetParent(sprenRoot);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrCreateSprenSprite();

        return new JoyParticle { go = go, sr = sr };
    }

    // ------------------------------------------------------------------
    // Logispren (Logicspren)
    // ------------------------------------------------------------------

    private void InitLogispren()
    {
        for (int i = 0; i < logisprenCount; i++)
        {
            var go = new GameObject("Logispren_" + i);
            go.transform.SetParent(sprenRoot);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateSprenSprite();

            var p = new LogiParticle
            {
                go = go,
                sr = sr,
                phase = i * (Mathf.PI * 2f / Mathf.Max(1, logisprenCount)),
                bobFreq = Random.Range(2.5f, 4.5f),
                baseColor = logisprenColors[i % logisprenColors.Length]
            };

            go.SetActive(false);
            logispren.Add(p);
        }
    }

    private void UpdateLogispren(float dt)
    {
        // Estado 2 en MascotaController corresponde a la lectura (Read)
        bool isReading = animator != null && animator.GetInteger("Estado") == 2;
        float targetAlpha = isReading ? 1f : 0f;
        logiAlpha = Mathf.MoveTowards(logiAlpha, targetAlpha, dt * 2.5f);

        if (logiAlpha <= 0.001f)
        {
            for (int i = 0; i < logispren.Count; i++)
            {
                if (logispren[i].go.activeSelf) logispren[i].go.SetActive(false);
            }
            return;
        }

        Vector3 head = GetHeadPosition();
        float time = Time.time;

        for (int i = 0; i < logispren.Count; i++)
        {
            var p = logispren[i];
            if (!p.go.activeSelf) p.go.SetActive(true);

            float angle = time * orbitSpeed + p.phase;
            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            // Órbita elíptica alrededor de la cabeza
            float x = head.x + cosA * orbitRadiusX;
            float y = head.y + sinA * orbitRadiusY + Mathf.Sin(time * p.bobFreq + p.phase) * 0.04f;

            p.go.transform.position = new Vector3(x, y, 0f);

            // Rotación suave continua para que giren en el espacio mientras orbitan
            float spinAngle = time * 75f + p.phase * Mathf.Rad2Deg;
            p.go.transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);

            // Profundidad 3D: por delante cuando sinA < 0, por detrás cuando sinA > 0
            p.sr.sortingOrder = sinA < 0 ? 15 : -5;

            // Ligera perspectiva de profundidad (más grande adelante, más chico atrás)
            float depth = (1f - sinA) * 0.5f; // 0 atrás, 1 adelante
            float scale = 3.2f * Mathf.Lerp(0.75f, 1.25f, depth);
            p.go.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = p.baseColor;
            c.a = p.baseColor.a * logiAlpha;
            p.sr.color = c;
        }
    }

    // ------------------------------------------------------------------
    // Utilidades y Creación Procedural del Sprite de Estrella 5x5
    // ------------------------------------------------------------------

    private Vector3 GetHeadPosition()
    {
        if (body != null)
        {
            return new Vector3(transform.position.x, body.bounds.max.y, 0f);
        }
        return transform.position + new Vector3(0f, 0.4f, 0f);
    }

    private static void EnsureRoot()
    {
        if (sprenRoot != null) return;
        var go = new GameObject("[SprenSystem]");
        DontDestroyOnLoad(go);
        sprenRoot = go.transform;
    }

    /// <summary>
    /// Genera dinámicamente un sprite pixel art de 5x5 con forma de estrella destellante (✨):
    /// 4 puntas cardinales afiladas con hombros diagonales que evitan la silueta de cruz.
    /// </summary>
    public static Sprite GetOrCreateSprenSprite()
    {
        if (sprenSprite != null) return sprenSprite;

        int size = 5;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color O = Color.clear;
        Color W = Color.white;
        Color T = new Color(1f, 1f, 1f, 0.85f); // Puntas afiladas
        Color D = new Color(1f, 1f, 1f, 0.50f); // Hombros diagonales (da forma de estrella)

        // Matriz de 5x5:
        // .  .  T  .  .  (y=4)
        // .  D  W  D  .  (y=3)
        // T  W  W  W  T  (y=2)
        // .  D  W  D  .  (y=1)
        // .  .  T  .  .  (y=0)
        Color[] pixels = new Color[25]
        {
            O, O, T, O, O,
            O, D, W, D, O,
            T, W, W, W, T,
            O, D, W, D, O,
            O, O, T, O, O
        };

        tex.SetPixels(pixels);
        tex.Apply();

        sprenSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return sprenSprite;
    }
}
