using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Spawner de la Bruja de Halloween:
/// Genera periódicamente la aparición de una bruja volando sobre su escoba.
/// A diferencia de los fantasmas, SIEMPRE aparece exactamente 1 bruja y
/// COMO MÁXIMO 1 en pantalla de forma simultánea. Mientras haya una bruja
/// volando, el spawner no generará otra hasta que la actual se disipe.
/// </summary>
[DisallowMultipleComponent]
public class HalloweenWitchSpawner : MonoBehaviour
{
    public static HalloweenWitchSpawner Instance { get; private set; }

    [Header("Activación del Evento")]
    [Tooltip("Habilita o deshabilita la aparición de la bruja.")]
    [SerializeField] private bool enableWitch = true;

    [Tooltip("Si está activo, solo vuela en la franja con mascotas activas.")]
    [SerializeField] private bool onlyInStripMode = true;

    [Header("Intervalos de Tiempo")]
    [Tooltip("Tiempo mínimo en segundos entre apariciones.")]
    [SerializeField] private float minInterval = 6f;

    [Tooltip("Tiempo máximo en segundos entre apariciones.")]
    [SerializeField] private float maxInterval = 8f;

    [Tooltip("Tiempo de espera para la primera aparición al iniciar la app.")]
    [SerializeField] private float initialDelay = 3f;

    [Header("Altura de Vuelo")]
    [Tooltip("Alto en el mundo de los personajes más altos (32 px a escala 4). La bruja vuela " +
             "por encima de esto, entre la cabeza de los personajes y el borde de arriba de la franja.")]
    [SerializeField] private float characterHeight = 1.28f;

    [Header("Duración de Vuelo")]
    [Tooltip("Tiempo en segundos que permanece en pantalla.")]
    [SerializeField] private float witchLifetime = 6.5f;

    [Header("Recursos y Prefab")]
    [Tooltip("Prefab opcional para la bruja. Si no se asigna, se construye proceduralmente.")]
    [SerializeField] private GameObject witchPrefab;

    [Tooltip("Sprites de la bruja (9 frames). Si no se asignan, se cargan automáticamente.")]
    [SerializeField] private Sprite[] witchSprites;

    private float timer = 0f;
    private ProceduralWitch currentActiveWitch = null;
    private static Sprite[] cachedSprites;

    // Base64 embebido del spritesheet (2.8 KB) como seguro absoluto e indestructible
    private const string EmbeddedWitchPngB64 = 
        "iVBORw0KGgoAAAANSUhEUgAAASAAAAAgCAYAAACy9KU0AAAAAXNSR0IArs4c6QAACuxJREFUeJztnH+IVccVx7+vlUsTY6zdbJe1NRu22Wqb/We1Wl41Gpu0CQk00tJYkr/qH6G1IPhHaUmpRgohpQVBsAb/MIXQ/BHS0ooVQ5Ima3d5ZeOu/6ywZa3kaXjLY5GIa1NzMd7+se9Mzp03M3funZn7YvYdWN7d++tzzplzzszcO+9VcItKFEUJbcdxXOmsNl3pSleKSOHE7WQB4GwuZerRLYBdWcriK/4LXRhFUTK8aYP4f3pi0lkRW+6KnlVYuPw+OH9qrIYoikDHytCjE/bLOtB2Jwpgl790+T7jP9cFZDSHcwmZiNzhWfzQOnTCflmPThbALn9p8kPEv/WJstEmmZ6Y9OoMneH1SxfTJ8Y3AIBGQXCZYqp06JT9XAd0sAB2+UuXHyr+rU7SwXUFAB8XAWdHRFGUxHGM9VuqYt+fN14W29/4y3/F9u03l4ntZnPOW0PI9rfZTRLAfp0OJglRALv8pcsPmf+ZJ6jgvABsffVDAMD0m0+nzukdPuDsBF7x65cuAvENnPnRSnz5wOfx3v4r4nPrqx8G4SOH/bKELIC6AhyqAHayA+ryzcUnpUOgAUDI/F9mOsjhZOiZ7y9PFYDTuCKUGH7wqFKZIkLFZ0V/b+rZDrAS7+2/AgDik0Tm0z2KNoSt/WsPv4v56f2pa3uHDzjzZR3QanzOP8ODYHR3iu9DjAG4cbkxALv8wHwAW+vLgvHLyH9tASJ4/dJFLMzNsyPL2wpAszmXMpi2XRNwRX8vFubmEccxoigS+9cefrft3JVf/RWiKEIcx+gdPpCaJ09PTCZ5dchj//CmDdi+60Tqelc+NAGAjWr/QxF0ZRVgCsDBbYcAABdG9xTBOfN9doCfBr5L+5eV/9oDURQlrABUaN+/f3ZPWwHImpsWmZPq+Fm8+XpDbPcOrC6sg8n+H7zTY+T64qsCoEz/6/iyqDoEWQokQQIAPQNfcuK7Fl8f9vssACr+d098RnmPFX2rAM/5VzT+oPFD7gKkA/pOQOJxNucS74Mb1/GFO1bh+vX/Ke/hWgSy+AAw8thg6rqzf7/gzO+0/30GYEh+iOKbhx9Kh7wdYP0/F3D7ss/hgxvXxb6Brwx65+tszYo/nR5GpWwTEFISnjo2jr6+/jYlXILBNvkB4PgLbyGKIqEDTVGKFkAT/5d/+rHY3n3/fvzhnwfw2sujKR848gGgTQfSgwJu4fL7qWOu/vddAH3xiySAS9zx6zrZAUOyX8ULyXfN/xYfcs0xPoS2hVMSUgKeOjaOZnMOfX39mK83CiegDfvhJ7eJbeKj5QTSoa+vX+iQR/IUn9deHsXu+xc/SVz5LTHqsPnxrysvoiAo6v84jisLc/NOAThfbxQuApyvSvy8fDmZbXWgbVf7hzdtyP0s0Kb4qDrgU8fGKdmFuPDzdsCkg0La2MYClAfOE/CRXZu9FAC0nDA9MSlWQVNlNfH5tc3mXMKv8cl//qkXhQ4PP7kNj//026IgtoLAiW8qgNx2EgqAvQ89B3gogK4ByAtgkc6HruFFqAifdC/6MgKa4ie3gcl+eHoZUKQDRqDZh4/8Uz+9YtJKQNAfl+efelFs8wQkp7QUsLM0QweV87L4Ueu7Y65rcnR8lQ4AsPeh50TSuPJ1/udc4OOGp0/S2dX/eQKQf4JNPfv6+lNfpcnL1yWgLX++3iicgPI1uqlPlv1FhWyfGqthaqyWOpZlv4/2N+X/vp1HxLYu/7Jsz90bqG4o9wQ8Aan39bUoSuY3m3M4+MYzqX37dh7B5bmm6L19LghT8QHgkV2bATby4deE5MtByIsgWsHv6n9ePHQjUBXfVwwU5fNrfPkgr/998HX22/BD55+sA1jbc911/FxvJVRwGuaNPDYo5n0hElCX/HEcY82aAS3fp+h04PaHYqv4VPzkAgypAJRVAFVsTwmQtPiQ7TfZDil5Xf1wq3TAsv0hi09W/meJUwFqNudQrS4uOnv77d/h7rvvcZ7vZkiyZs2Akv/Eb+7Avp1HtF9EXb+lmmDxpzsK90Iu9vvgg/WAfGgtB0AovksHFMr/UNgfgm/SoSz7oRgBuRaAPPwQ+Z/5DIhEnk/y7auN83jggZ+npiPynH/9lmpCjVBUevr70GzOiT960n+1cR6v/PqaeBX9vZ9sT/E516TD0NA67bGi9st2u/iAFx9q4Gp1DwYHf4i/HfmH9lmDL74sZK/Ml0cdPPl88qGxX8eHg/2mBCyDD03xyeJzrgvbJf+JreIrqxQl4uzsTNtxfmOqflcb53GuflIMBWn+SUkig217gaGhdYmsg4rfaJxAvX6hjS/9aFkqEFx7Ikj2X8MMZmdn6Jw2Nm279ITyfDqKoqRa3YOrjfO4c/W9qNUOpc5X9bwmvm270711fHoLmoevamuVDvIUzBffRjifku1W4XNxHQXSNo9/Of5MizM5XzkCmp2dqczOzlSGhtYl8qhA9UboXP1k2z2GN21IVd2psVpFbgST6EYjcRxXVvSsEv8vfDQuEl/my9ypsVqFnuTrdCCbZT79r7O/Xr+Aw6efxeHTz6buJze2iq+yleuhYlPyk9Rqh3DwjWdEESb/gxXfKIoSk/1yu3O9iE1/Mn909Pdim6YCKj6dI/Op+Kh8z3XgI2DOV4mt/aaRr44PloA2fNqXFX9cVLmn4l+8Vku1v+x/5Mg/k/9hiH85/rgQW8XX9oIq2T3Sg5caNzE9MSmMXxu/CQD44+Q5cZ6u94f0WldXiVU67B5ZXH7+UuOm+AnWgYFBbL/rtja+Yf6Z8NEJ52cFIdmOVhCp7KefhFXpsH5LNZmemFSyIY0AdLrwEQIvANTz8BGgilPU97oRUVl8lQ6++TZFyMX/We1v0oU6BeJTIa9W92Dho3Gx1sjGfkids4mtaneyQxX/Ml91f7kNrBuBJyBYJf/tjo0AgF/89R3jgyebBsjDpzURB59YfP2995VxmB6qc768JsQm+Cztp8NKPcj5qjUpPgogSVYB1v1apK0ODnzjWxlXPk8ADSNZv6UazP9ZCWhq/ywdOJ+vB3q6OiL4JKp7m/LPV/wX4YuV0LbDUNmprcS3uo4qsLygqWy+SqiXyauDLR9sVbXrW4rhTRtEr7ujFQCYtHvzwF7N52LyACzK59+tcykAOr787FHBF6NnF7Hhy4sGkdH+trGHxWQGWnF4tHbW6pqs/MsjReJfxzcO+blkPSC0FV0PaNLBF9vEL0MPXQLS8NrE3n7XbThaO+u0lkZOPJUOMEyBW9Nf54f3Kj7XQ3W9K5+P/oqMAj3wnQowPOWBywg0BD/TCSGSX9cAqiQsi8/1kPf5cry8j3RQvQHy7YcoipL7Bh7FnavvRdT8F16fGcu8H9fhaw/24/gLbznpMDK0I7HhB2oDUYCzpsohYtCmAIfuhIvGvy8dVPzUl1F5AfCZ+GANwD+5E2Re2XxYvgouKnEcV3QJqGL61qNla/LNz34RNoN2+WGk6k1jEbHhyyMxT76o3DfwqFUBDJEHpvbnXPi3G7CMf534LMAyv+1BVKgEhKEHLINt4pclI0M7ku+s+RbONo6XzredAn0S+IHiwXoKFioOVe0fkqfiZ8V/yFxU8cUIqCwnqHrAstg6fhkSRVFyrn5SrJny+R0tG4mln7Qok52XHzIesh5Ah2Lr2r/M2IdF/IfWR+Z/InvATyNf1qFsdlcA+lJrS0r3fyfbv9Pxr+MvqQTsNL8rXemkdDr+O83vSle60pWU/B/Kgxyypt7PRgAAAABJRU5ErkJggg==";

    public bool HasActiveWitch => currentActiveWitch != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        timer = Mathf.Max(0.5f, initialDelay);
    }

        private void OnEnable()
    {
        ProceduralWitch.OnWitchDestroyed += HandleWitchDestroyed;
        HalloweenEvent.Changed += HandleHalloweenChanged;
    }

    private void OnDisable()
    {
        ProceduralWitch.OnWitchDestroyed -= HandleWitchDestroyed;
        HalloweenEvent.Changed -= HandleHalloweenChanged;
    }

    private void HandleHalloweenChanged()
    {
        if (!HalloweenEvent.Enabled)
        {
            if (currentActiveWitch != null)
            {
                Destroy(currentActiveWitch.gameObject);
                currentActiveWitch = null;
            }
        }
        else
        {
            ResetTimer();
        }
    }

    private void Start()
    {
        if (witchPrefab == null)
        {
            witchPrefab = Resources.Load<GameObject>("Halloween/Bruja");
            if (witchPrefab == null)
            {
                witchPrefab = Resources.Load<GameObject>("Halloween/Witch");
            }
        }

        if (witchSprites == null || witchSprites.Length == 0 || witchSprites.All(s => s == null))
        {
            witchSprites = GetWitchSprites();
        }
    }

        private void Update()
    {
        if (!enableWitch || !HalloweenEvent.Enabled) return;

        // Atajo de prueba en el Editor (Ctrl + W)
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log("[HalloweenWitchSpawner] Spawn forzado de bruja mediante atajo (Ctrl + W)");
            ForceSpawnWitch();
        }
#endif

        if (onlyInStripMode && DesktopWindow.Instance != null)
        {
            if (DesktopWindow.Instance.Mode != DesktopWindow.WindowMode.Strip)
            {
                return;
            }
        }

        // Si ya hay una bruja activa en pantalla, no iniciar cuenta regresiva para otra
        if (currentActiveWitch != null)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnWitch();
            ResetTimer();
        }
    }

    private void ResetTimer()
    {
        timer = Random.Range(minInterval, maxInterval);
    }

    private void HandleWitchDestroyed(ProceduralWitch witch)
    {
        if (currentActiveWitch == witch)
        {
            currentActiveWitch = null;
            ResetTimer();
        }
    }

    /// <summary>
    /// Fuerza la aparición de una bruja (reemplazando la anterior si existiera en modo prueba).
    /// </summary>
    [ContextMenu("Spawn 1 Witch (Test)")]
    public void ForceSpawnWitch()
    {
        if (currentActiveWitch != null)
        {
            Destroy(currentActiveWitch.gameObject);
            currentActiveWitch = null;
        }
        SpawnWitch();
    }

    /// <summary>
    /// Genera exactamente 1 bruja si no hay ninguna en pantalla (como máximo 1).
    /// </summary>
    public void SpawnWitch()
    {
        if (currentActiveWitch != null)
        {
            return; // Garantía estricta: como máximo 1 bruja simultánea
        }

        Rect bounds = GetStageBounds();
        float stageScale = DesktopWindow.Instance != null ? DesktopWindow.Instance.StageScale : ProceduralWitch.ReferenceStageScale;
        float scaleFactor = ProceduralWitch.GetScaleFactor(stageScale);

        // Dirección de vuelo aleatoria: de izquierda a derecha (+1) o de derecha a izquierda (-1)
        int dir = Random.value > 0.5f ? 1 : -1;

        // Posición horizontal inicial: cerca del borde correspondiente a la dirección
        float borderPadding = 0.5f * scaleFactor;
        float spawnX;
        if (dir > 0)
        {
            spawnX = bounds.xMin + borderPadding + Random.Range(0f, 0.4f * scaleFactor);
        }
        else
        {
            spawnX = bounds.xMax - borderPadding - Random.Range(0f, 0.4f * scaleFactor);
        }

        // Altura de vuelo: surcando el aire por encima de las cabezas de las mascotas,
        // dejando lugar arriba para la ondulación. Si los personajes llenan la franja,
        // vuela pegada al borde de arriba.
        float halfHeight = (32f / 100f) * ProceduralWitch.GetFixedWorldScale(stageScale) * 0.5f;
        float maxY = ProceduralWitch.GetTopLimitY(bounds, stageScale) - 0.06f * scaleFactor;
        float minY = Mathf.Min(bounds.yMin + characterHeight + halfHeight, maxY);
        float spawnY = Random.Range(minY, maxY);

        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0f);
        float customLife = Mathf.Max(4.5f, witchLifetime + Random.Range(-0.5f, 0.5f));

        CreateWitchInstance(spawnPos, dir, customLife, bounds);
    }

    private void CreateWitchInstance(Vector3 pos, int dir, float life, Rect bounds)
    {
        var sprites = GetWitchSprites();
        GameObject witchGo;

        if (witchPrefab != null)
        {
            witchGo = Instantiate(witchPrefab, pos, Quaternion.identity);
        }
        else
        {
            witchGo = new GameObject("HalloweenWitch");
            witchGo.transform.position = pos;

            var sr = witchGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 17;

            var col = witchGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.32f, 0.32f);
        }

        var renderer = witchGo.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            if (sprites != null && sprites.Length > 0 && sprites[0] != null)
            {
                renderer.sprite = sprites[0];
            }
            renderer.sortingOrder = 17;
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.enabled = true;
        }

        var witch = witchGo.GetComponent<ProceduralWitch>();
        if (witch == null) witch = witchGo.AddComponent<ProceduralWitch>();
        
        witch.Initialize(pos, dir, life, bounds);
        currentActiveWitch = witch;
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
    /// Obtiene los 9 cuadros del spritesheet de vuelo en cualquier entorno:
    /// 1. Caché interno.
    /// 2. Recursos (Resources.LoadAll).
    /// 3. Editor (AssetDatabase).
    /// 4. Slicing procedimental en caliente desde el PNG en disco (9 cuadros de 32x32).
    /// 5. Fallback embebido Base64 indestructible.
    /// </summary>
    public static Sprite[] GetWitchSprites()
    {
        if (cachedSprites != null && cachedSprites.Length > 0 && cachedSprites.All(s => s != null))
            return cachedSprites;

        // 1. Resources.LoadAll
        var resSprites = Resources.LoadAll<Sprite>("Halloween/Witch1-Sheet-export");
        if (resSprites != null && resSprites.Length > 0 && resSprites.Any(s => s != null))
        {
            cachedSprites = resSprites.Where(s => s != null).OrderBy(s => s.name).ToArray();
            if (cachedSprites.Length > 0) return cachedSprites;
        }

#if UNITY_EDITOR
        // 2. AssetDatabase
        string[] editorAssetPaths = new string[]
        {
            "Assets/Art/Halloween/Witch1-Sheet-export.png",
            "Assets/Resources/Halloween/Witch1-Sheet-export.png"
        };
        foreach (var p in editorAssetPaths)
        {
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p);
            var list = new List<Sprite>();
            foreach (var a in assets)
            {
                if (a is Sprite s && s != null) list.Add(s);
            }
            if (list.Count > 0)
            {
                list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                cachedSprites = list.ToArray();
                return cachedSprites;
            }
        }
#endif

        // 3. Fallback en tiempo de ejecución: segmentar PNG en 9 cuadros de 32x32 desde disco
        string[] searchPaths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "Art/Halloween/Witch1-Sheet-export.png"),
            System.IO.Path.Combine(Application.dataPath, "Resources/Halloween/Witch1-Sheet-export.png"),
            System.IO.Path.Combine(Application.dataPath, "../Assets/Art/Halloween/Witch1-Sheet-export.png"),
            System.IO.Path.Combine(Application.dataPath, "../Assets/Resources/Halloween/Witch1-Sheet-export.png"),
            System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets/Art/Halloween/Witch1-Sheet-export.png"),
            System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets/Resources/Halloween/Witch1-Sheet-export.png")
        };

        foreach (var path in searchPaths)
        {
            if (System.IO.File.Exists(path))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(288, 32, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                if (tex.LoadImage(bytes))
                {
                    int cols = tex.width / 32;
                    cachedSprites = new Sprite[cols];
                    for (int i = 0; i < cols; i++)
                    {
                        cachedSprites[i] = Sprite.Create(
                            tex,
                            new Rect(i * 32, 0, 32, 32),
                            new Vector2(0.5f, 0.5f),
                            100f
                        );
                        cachedSprites[i].name = $"Witch_{i}";
                    }
                    return cachedSprites;
                }
            }
        }

        // 4. Fallback absoluto desde Base64 (garantiza sprites en 100% de los casos)
        try
        {
            byte[] embeddedBytes = System.Convert.FromBase64String(EmbeddedWitchPngB64);
            var fallbackTex = new Texture2D(288, 32, TextureFormat.RGBA32, false);
            fallbackTex.filterMode = FilterMode.Point;
            fallbackTex.wrapMode = TextureWrapMode.Clamp;

            if (fallbackTex.LoadImage(embeddedBytes))
            {
                int cols = fallbackTex.width / 32;
                cachedSprites = new Sprite[cols];
                for (int i = 0; i < cols; i++)
                {
                    cachedSprites[i] = Sprite.Create(
                        fallbackTex,
                        new Rect(i * 32, 0, 32, 32),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    cachedSprites[i].name = $"Witch_{i}";
                }
                return cachedSprites;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HalloweenWitchSpawner] Error al decodificar sprites de bruja: {ex.Message}");
        }

        return System.Array.Empty<Sprite>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<HalloweenWitchSpawner>() == null)
        {
            var go = new GameObject("[HalloweenWitchSpawner]");
            DontDestroyOnLoad(go);
            go.AddComponent<HalloweenWitchSpawner>();
        }
    }
}
