using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Spawner del Evento de Halloween:
/// Genera periódicamente apariciones aleatorias de fantasmas (de 1 a 3) en distintas
/// posiciones de la pantalla/franja, los deja flotar durante 4 a 5 segundos y los disipa.
/// El tamaño visual del fantasma se mantiene constante y fijado al correspondiente a la resolución 0.75x.
/// </summary>
[DisallowMultipleComponent]
public class HalloweenGhostSpawner : MonoBehaviour
{
    public static HalloweenGhostSpawner Instance { get; private set; }

    [Header("Activación del Evento")]
    [Tooltip("Habilita o deshabilita la aparición de fantasmas.")]
    [SerializeField] private bool enableHalloween = true;

    [Tooltip("Si está activo, solo aparecen en la franja con las mascotas activas (no durante el menú de selección).")]
    [SerializeField] private bool onlyInStripMode = true;

    [Header("Intervalos de Tiempo")]
    [Tooltip("Tiempo mínimo en segundos entre apariciones aleatorias.")]
    [SerializeField] private float minInterval = 8f;

    [Tooltip("Tiempo máximo en segundos entre apariciones.")]
    [SerializeField] private float maxInterval = 15f;

    [Tooltip("Tiempo de espera para la primera aparición al iniciar la app.")]
    [SerializeField] private float initialDelay = 3f;

    [Header("Altura de Vuelo")]
    [Tooltip("Alto en el mundo de los personajes más altos (32 px a escala 4). Los fantasmas vuelan " +
             "por encima de esto, entre la cabeza de los personajes y el borde de arriba de la franja.")]
    [SerializeField] private float characterHeight = 1.28f;

    [Header("Cantidad de Fantasmas")]
    [Tooltip("Cantidad mínima de fantasmas por aparición.")]
    [SerializeField] private int minGhosts = 1;

    [Tooltip("Cantidad máxima de fantasmas por aparición.")]
    [SerializeField] private int maxGhosts = 3;

    [Tooltip("Espera mínima en segundos entre un fantasma y el siguiente de la misma oleada.")]
    [SerializeField] private float minStagger = 0.4f;

    [Tooltip("Espera máxima en segundos entre un fantasma y el siguiente de la misma oleada.")]
    [SerializeField] private float maxStagger = 2.8f;

    [Header("Duración de los Fantasmas")]
    [Tooltip("Tiempo base que permanecen en pantalla antes de desaparecer (4 a 5 segundos).")]
    [SerializeField] private float ghostLifetime = 6f;

    [Header("Recursos y Prefab")]
    [Tooltip("Prefab opcional para el fantasma. Si no se asigna, se construye proceduralmente en memoria.")]
    [SerializeField] private GameObject ghostPrefab;

    [Tooltip("Sprites del fantasma (8 frames). Si no se asignan, se cargan automáticamente.")]
    [SerializeField] private Sprite[] ghostSprites;

    private float timer = 0f;
    private Coroutine waveRoutine;
    private static Sprite[] cachedSprites;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Primer intervalo: demora breve inicial para que aparezcan pronto
                timer = Mathf.Max(0.5f, initialDelay);
    }

    private void OnEnable()
    {
        HalloweenEvent.Changed += HandleHalloweenChanged;
    }

    private void OnDisable()
    {
        HalloweenEvent.Changed -= HandleHalloweenChanged;

        // Unity corta las corrutinas al desactivar: sin esto la oleada quedaría "en curso" para siempre
        StopWave();
    }

    private void HandleHalloweenChanged()
    {
        if (!HalloweenEvent.Enabled)
        {
            StopWave();

            var ghosts = FindObjectsByType<ProceduralGhost>(FindObjectsSortMode.None);
            foreach (var g in ghosts)
            {
                if (g != null) Destroy(g.gameObject);
            }
        }
        else
        {
            ResetTimer();
        }
    }

    private void Start()
    {
        if (ghostPrefab == null)
        {
            ghostPrefab = Resources.Load<GameObject>("Halloween/Fantasmaspren");
        }

        if (ghostSprites == null || ghostSprites.Length == 0)
        {
            ghostSprites = GetGhostSprites();
        }
    }

        private void Update()
    {
        if (!enableHalloween || !HalloweenEvent.Enabled) return;

        // Comprobación de atajo de prueba en el Editor (Ctrl + H para probar instantáneamente)
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("[HalloweenGhostSpawner] Spawn forzado mediante atajo (Ctrl + H)");
            SpawnGhostWave();
        }
#endif

        // Si solo debe correr en modo Franja, verificar DesktopWindow
        if (onlyInStripMode && DesktopWindow.Instance != null)
        {
            if (DesktopWindow.Instance.Mode != DesktopWindow.WindowMode.Strip)
            {
                return; // Esperar a que el usuario termine en el menú
            }
        }

        // Mientras una oleada sigue soltando fantasmas, la cuenta para la próxima no corre
        if (waveRoutine != null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnGhostWave();
            ResetTimer();
        }
    }

    private void ResetTimer()
    {
        timer = Random.Range(minInterval, maxInterval);
    }

    /// <summary>
    /// Genera una oleada aleatoria de 1 a 3 fantasmas en distintas posiciones de la pantalla.
    /// </summary>
    [ContextMenu("Spawn Ghost Wave (Aleatorio 1 a 3)")]
    public void SpawnGhostWave()
    {
        int count = Random.Range(minGhosts, maxGhosts + 1);
        SpawnGhosts(count);
    }

    [ContextMenu("Spawn 1 Ghost")]
    public void SpawnOneGhost() => SpawnGhosts(1);

    [ContextMenu("Spawn 3 Ghosts")]
    public void SpawnThreeGhosts() => SpawnGhosts(3);

    /// <summary>
    /// Genera la cantidad solicitada de fantasmas distribuidos armónicamente en el ancho de la franja.
    /// No salen todos juntos: el primero aparece enseguida y cada uno de los siguientes
    /// después de una espera aleatoria (minStagger a maxStagger).
    /// </summary>
    public void SpawnGhosts(int count)
    {
        count = Mathf.Clamp(count, 1, 10);
        StopWave();
        waveRoutine = StartCoroutine(SpawnWaveRoutine(count));
    }

    private IEnumerator SpawnWaveRoutine(int count)
    {
        // Cada fantasma va a un sector distinto del ancho, en orden mezclado:
        // si no, con el desfase aparecerían siempre de izquierda a derecha.
        int[] sectors = Enumerable.Range(0, count).OrderBy(_ => Random.value).ToArray();

        for (int i = 0; i < count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(Random.Range(minStagger, maxStagger));
            SpawnGhostInSector(sectors[i], count);
        }

        waveRoutine = null;
    }

    private void StopWave()
    {
        if (waveRoutine == null) return;
        StopCoroutine(waveRoutine);
        waveRoutine = null;
    }

    /// <summary>
    /// Genera un fantasma en el sector indicado del ancho de la franja (de un total de sectorCount).
    /// La posición se calcula al momento de aparecer, por si la franja cambió durante la oleada.
    /// </summary>
    private void SpawnGhostInSector(int sector, int sectorCount)
    {
        Rect bounds = GetStageBounds();

        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ProceduralGhost.ReferenceStageScale;
        float scaleFactor = ProceduralGhost.GetScaleFactor(stageScale);

        // Subdividir el ancho de la pantalla en sectores para que no salgan superpuestos
        float borderPadding = 0.4f * scaleFactor;
        float availableWidth = Mathf.Max(1f, bounds.width - borderPadding * 2f);
        float sectorWidth = availableWidth / sectorCount;
        float startX = bounds.xMin + borderPadding;

        // Altura de vuelo: por encima de las cabezas de las mascotas, dejando lugar arriba
        // para el balanceo. Si los personajes llenan la franja, vuelan pegados al borde de arriba.
        float ghostHalfHeight = ProceduralGhost.GetVisibleHalfHeight(stageScale);
        float calculatedMaxY = ProceduralGhost.GetTopLimitY(bounds, stageScale) - 0.09f * scaleFactor;
        float calculatedMinY = Mathf.Min(bounds.yMin + characterHeight + ghostHalfHeight, calculatedMaxY);

        // Posición X aleatoria dentro de su sector
        float minX = startX + sector * sectorWidth + 0.1f * scaleFactor;
        float maxX = startX + (sector + 1) * sectorWidth - 0.1f * scaleFactor;
        float spawnX = Random.Range(minX, maxX);

        // Altura Y: volando por encima de las mascotas
        float spawnY = Random.Range(calculatedMinY, calculatedMaxY);

        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0f);

        // Dirección inicial y tiempo con ligera variación orgánica
        int dir = Random.value > 0.5f ? 1 : -1;
        float variance = Random.Range(-0.35f, 0.45f);
        float customLife = Mathf.Max(3.5f, ghostLifetime + variance);

        CreateGhostInstance(spawnPos, dir, customLife, bounds);
    }

    private void CreateGhostInstance(Vector3 pos, int dir, float life, Rect bounds)
    {
        GameObject ghostGo;

        if (ghostPrefab != null)
        {
            ghostGo = Instantiate(ghostPrefab, pos, Quaternion.identity);
        }
        else
        {
            // Creación procedimental completa en memoria
            ghostGo = new GameObject("Fantasmaspren");
            ghostGo.transform.position = pos;

            var sr = ghostGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 16;

            var col = ghostGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.24f, 0.24f);

            var ghostComp = ghostGo.AddComponent<ProceduralGhost>();
            ghostComp.Initialize(pos, dir, life, bounds);
            return;
        }

        var ghost = ghostGo.GetComponent<ProceduralGhost>();
        if (ghost == null) ghost = ghostGo.AddComponent<ProceduralGhost>();
        ghost.Initialize(pos, dir, life, bounds);
    }

    private Rect GetStageBounds()
    {
        if (DesktopWindow.Instance != null)
        {
            return DesktopWindow.Instance.StageWorldRect;
        }

        if (Camera.main != null)
        {
            var cam = Camera.main;
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            return new Rect(-w * 0.5f, -h * 0.5f, w, h);
        }

        return new Rect(-4f, -0.6f, 8f, 1.6f);
    }

    /// <summary>
    /// Garantiza la obtención de los 8 frames del spritesheet en cualquier entorno:
    /// 1. Caché interno.
    /// 2. Inspector / Recursos (Resources/Halloween/Fantasmaspren).
    /// 3. Editor (AssetDatabase).
    /// 4. Slicing procedimental en caliente desde el PNG en disco.
    /// </summary>
    public static Sprite[] GetGhostSprites()
    {
        if (cachedSprites != null && cachedSprites.Length > 0)
            return cachedSprites;

        // 1. Resources.LoadAll
        var resSprites = Resources.LoadAll<Sprite>("Halloween/Fantasmaspren");
        if (resSprites != null && resSprites.Length > 0)
        {
            cachedSprites = resSprites.OrderBy(s => s.name).ToArray();
            return cachedSprites;
        }

#if UNITY_EDITOR
        // 2. AssetDatabase en modo editor
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Halloween/Fantasmaspren.png");
        var list = new List<Sprite>();
        foreach (var a in assets)
        {
            if (a is Sprite s) list.Add(s);
        }
        if (list.Count > 0)
        {
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            cachedSprites = list.ToArray();
            return cachedSprites;
        }
#endif

        // 3. Fallback en tiempo de ejecución: cargar bytes del PNG y segmentar en 8 cuadros de 24x24
        string[] searchPaths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "Art/Halloween/Fantasmaspren.png"),
            System.IO.Path.Combine(Application.dataPath, "Resources/Halloween/Fantasmaspren.png")
        };

        foreach (var path in searchPaths)
        {
            if (System.IO.File.Exists(path))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(192, 24, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                if (tex.LoadImage(bytes))
                {
                    int cols = tex.width / 24;
                    cachedSprites = new Sprite[cols];
                    for (int i = 0; i < cols; i++)
                    {
                        cachedSprites[i] = Sprite.Create(
                            tex,
                            new Rect(i * 24, 0, 24, 24),
                            new Vector2(0.5f, 0.5f),
                            100f
                        );
                        cachedSprites[i].name = $"Fantasmaspren_{i}";
                    }
                    return cachedSprites;
                }
            }
        }

        return System.Array.Empty<Sprite>();
    }

    /// <summary>
    /// Auto-arranque al cargar la escena para que la funcionalidad esté lista
    /// inmediatamente sin requerir configuración manual en la escena.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<HalloweenGhostSpawner>() == null)
        {
            var go = new GameObject("[HalloweenGhostSpawner]");
            DontDestroyOnLoad(go);
            go.AddComponent<HalloweenGhostSpawner>();
        }
    }
}