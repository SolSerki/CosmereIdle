using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Dueño de la ventana del SO. Tiene dos formas y sabe pasar de una a la otra:
///
///   Strip  - franja donde viven las mascotas. Por defecto ocupa el ancho de la
///            pantalla y se apoya sobre la barra de tareas, pero el jugador la
///            puede mover y redimensionar (ver LayoutEditor).
///   Panel  - cuadrado centrado en la pantalla. Es la pantalla de seleccion.
///
/// Es el unico lugar que toca la ventana del SO, y tambien el unico que fija el
/// orthographicSize de la camara: los dos valores son la misma decision, y si se
/// tocan por separado el pixel art deja de caer sobre la grilla de la pantalla.
///
/// La franja se piensa como un "escenario" (Stage) anclado al mundo por el
/// piso: el borde de abajo esta siempre en <see cref="FloorY"/>, mida lo que
/// mida la ventana. Asi el tamaño de la ventana y el de los personajes son
/// independientes:
///
///   - Redimensionar la franja solo cambia cuanto mundo se ve. Los personajes
///     siguen parados en el mismo piso y del mismo tamaño.
///   - El tamaño de los personajes (<see cref="StageScale"/>) es cuantos pixeles
///     de pantalla mide una unidad de mundo. Lo cambia el jugador con sus
///     propios botones y no mueve la ventana.
///
/// La ventana puede ser mas grande que el escenario: el menu de la esquina pide
/// lugar extra (SetOverlay) cuando se abre y el escenario es muy bajo. Como la
/// ventana es transparente y click-through, ese lugar de mas no se ve.
///
/// Requisitos que NO se configuran desde aca y sin los cuales esto no funciona.
/// Los tres primeros ya nos costaron un dia cada uno:
///   - Graphics API: Direct3D11, con Auto Graphics for Windows DESACTIVADO.
///     En D3D12 el swapchain siempre es flip model y no le pasa alpha a DWM.
///   - Use DXGI Flip Model Swapchain: OFF (solo aplica a D3D11).
///   - Camara: Solid Color con alpha 0 y HDR OFF. Con HDR prendido no funciona.
///   - Render pipeline: Built-in. URP no sirve para esto en Unity 6.
///   - Fullscreen Mode: Windowed, y Run In Background ON.
///
/// Solo Windows. En el Editor no hace nada: la transparencia solo existe en el build.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)] // El cursor tiene que estar leido antes que lo use nadie.
public class DesktopWindow : MonoBehaviour
{
    public enum WindowMode
    {
        /// <summary>Franja donde caminan las mascotas.</summary>
        Strip,

        /// <summary>Cuadrado centrado en la pantalla.</summary>
        Panel
    }

    public static DesktopWindow Instance { get; private set; }

    [Header("Franja")]
    [Tooltip("Alto de la franja automatica en pixeles de pantalla, con los personajes a tamaño 1. " +
             "Crece con el tamaño de los personajes para que siempre entren.")]
    [SerializeField] private int stripHeight = 200;

    [Tooltip("Corrimiento vertical en pixeles. Positivo baja la franja sobre la barra de tareas.")]
    [SerializeField] private int verticalOffset = 0;

    [Header("Tamaño de los personajes")]
    [SerializeField] private float minStageScale = 0.5f;
    [SerializeField] private float maxStageScale = 3f;

    [Tooltip("El tamaño va de a saltos. Los personajes estan a 4x, asi que con saltos " +
             "de 0.25 cada pixel del sprite cae en un numero entero de pixeles de " +
             "pantalla y el pixel art no se deforma.")]
    [SerializeField] private float stageScaleStep = 0.25f;

    [Header("Franja a mano")]
    [Tooltip("Tamaño minimo de la franja en pixeles, al redimensionarla.")]
    [SerializeField] private int minStageWidth = 240;
    [SerializeField] private int minStageHeight = 80;

    [Header("Panel")]
    [Tooltip("Lado del panel cuadrado, en pixeles. Lo pisa quien llame a SetPanel(), " +
             "que lo calcula para que le entre su contenido.")]
    [SerializeField] private int panelSide = 640;

    [Tooltip("Pixeles de sprite por unidad de mundo. TIENE que ser el mismo PPU con el " +
             "que se importan los sprites (100). Si no coincide, el pixel art se ve " +
             "borroso y tiembla al moverse.")]
    [SerializeField] private float pixelsPerUnit = 100f;

    [Header("Siempre visible")]
    [Tooltip("Si esta prendido la franja queda por encima de todas las ventanas. " +
             "Si se apaga, se comporta como una ventana normal y las demas apps la tapan.")]
    [SerializeField] private bool alwaysOnTop = true;

    [Header("Click-through")]
    [Tooltip("El mouse atraviesa la ventana salvo cuando esta sobre algo con collider. " +
             "Asi el escritorio se puede usar normal, pero al personaje se le puede hacer click.")]
    [SerializeField] private bool autoClickThrough = true;

    [Tooltip("Que capas cuentan como 'contenido clickeable'. Todo lo que este aca " +
             "necesita un Collider2D.")]
    [SerializeField] private LayerMask interactiveLayers = ~0;

    [Header("Interno")]
    [Tooltip("Cada cuanto se re-chequea la barra de tareas y se reafirma el z-order.")]
    [SerializeField] private float refreshInterval = 1f;

    private const string MonitorKey = "CosmereIdle_MonitorIndex";
    private const string StageKey = "CosmereIdle_StageRect";
    private const string ScaleKey = "CosmereIdle_PetScale";

    /// <summary>Rectangulo que ocupa la ventana, en pixeles de pantalla (origen arriba-izquierda).</summary>
    public RectInt Bounds { get; private set; }

    /// <summary>
    /// Rectangulo del escenario en pixeles de pantalla. En la franja puede ser
    /// mas chico que <see cref="Bounds"/> si el menu pidio lugar extra; en el
    /// panel son iguales.
    /// </summary>
    public RectInt Stage { get; private set; }

    /// <summary>
    /// El escenario en coordenadas de mundo. En la franja su borde de abajo es
    /// <see cref="FloorY"/>; en el panel esta centrado en el origen.
    /// </summary>
    public Rect StageWorldRect { get; private set; }

    /// <summary>Altura del piso de la franja en el mundo. No cambia nunca.</summary>
    public float FloorY => -stripHeight * 0.5f / Mathf.Max(1f, pixelsPerUnit);

    /// <summary>Si el jugador acomodo la franja a mano (si no, sigue a la barra de tareas).</summary>
    public bool HasCustomStage => customStage;

    /// <summary>
    /// Tamaño de los personajes. En el panel es siempre 1: su layout ya se
    /// calcula a medida.
    /// </summary>
    public float StageScale => Mode == WindowMode.Panel ? 1f : stageScale;

    /// <summary>El tamaño elegido para los personajes, valga o no en la forma actual.</summary>
    public float PetScale => stageScale;

    public float MinPetScale => minStageScale;
    public float MaxPetScale => maxStageScale;
    public float PetScaleStep => stageScaleStep;

    /// <summary>
    /// Cuantos pixeles de pantalla mide un pixel de la interfaz (menu, botones,
    /// tooltips). Es entero para que los iconos no se deformen, y no baja de 1:
    /// achicar la franja achica a los personajes, pero un menu de 8 pixeles no
    /// se puede leer.
    /// </summary>
    public int UiPixelScale => Mode == WindowMode.Panel ? 1 : Mathf.Max(1, Mathf.FloorToInt(stageScale + 0.5f));

    /// <summary>
    /// localScale que tiene que llevar algo de la interfaz para medir en
    /// pantalla lo mismo que a escala 1 (multiplicado por <see cref="UiPixelScale"/>).
    /// </summary>
    public float UiScale => UiPixelScale / StageScale;

    /// <summary>Unidades de mundo que mide un pixel de pantalla.</summary>
    public float WorldUnitsPerPixel => 1f / (Mathf.Max(1f, pixelsPerUnit) * StageScale);

    /// <summary>Si el botón izquierdo se mantiene presionado actualmente.</summary>
    public bool IsLeftDown => leftWasDown;

    /// <summary>Bandera para avisar a DesktopWindow que estamos arrastrando y no debe volver click-through la ventana.</summary>
    public bool IsDraggingContent { get; set; }

    /// <summary>Posicion del cursor en coordenadas de mundo, valga o no el foco.</summary>
    public Vector3 CursorWorldPosition { get; private set; }

    /// <summary>
    /// Posicion del cursor en pixeles del escritorio (origen arriba-izquierda).
    /// Es lo que hay que usar para mover o redimensionar la ventana: las
    /// coordenadas de mundo cambian justamente cuando la ventana cambia.
    /// </summary>
    public Vector2Int CursorScreenPixel { get; private set; }

    /// <summary>Si el cursor esta sobre algo clickeable de la ventana.</summary>
    public bool CursorOverContent { get; private set; }

    /// <summary>Flanco de bajada del boton izquierdo, leido global (sin depender del foco).</summary>
    public bool LeftPressedThisFrame { get; private set; }

    public bool AlwaysOnTop => alwaysOnTop;

    /// <summary>Que forma tiene la ventana ahora mismo.</summary>
    public WindowMode Mode { get; private set; } = WindowMode.Strip;

    /// <summary>Para que quien arme un layout pueda pasar de unidades de mundo a pixeles.</summary>
    public float PixelsPerUnit => pixelsPerUnit;

    private IntPtr hwnd = IntPtr.Zero;
    private Camera cam;
    private float nextRefresh;
    private bool clickThroughApplied;
    private bool leftWasDown;
    private int currentMonitorIndex = 0;

    // Franja acomodada a mano, en pixeles del escritorio.
    private bool customStage;
    private RectInt customRect;
    private float stageScale = 1f;

    // Lugar extra que pide el menu, anclado a la esquina de abajo a la derecha
    // del escenario.
    private Vector2Int overlay;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        // Cargar monitor guardado
        currentMonitorIndex = PlayerPrefs.GetInt(MonitorKey, 0);
        LoadStage();

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        hwnd = Win32.FindPlayerWindow();

        if (hwnd == IntPtr.Zero)
        {
            Debug.LogError("[DesktopWindow] No se encontro la ventana del player.");
            enabled = false;
            return;
        }

        MakeTransparentOverlay();
#endif

        Reposition();
    }

    private void Update()
    {
        UpdateCursor();
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (hwnd == IntPtr.Zero) return;

        // Si el cursor está sobre contenido O estamos arrastrando algo, NO debe ser click-through
        bool interceptInput = CursorOverContent || IsDraggingContent;
        if (autoClickThrough) ApplyClickThrough(!interceptInput);

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + refreshInterval;
            Reposition();
        }
#endif
    }

    // ------------------------------------------------------------------
    // Forma de la ventana
    // ------------------------------------------------------------------

    /// <summary>Vuelve a la franja.</summary>
    public void SetStrip()
    {
        Mode = WindowMode.Strip;
        Reposition();
    }

    /// <summary>
    /// Pasa a un cuadrado centrado en la pantalla. El lado lo decide quien llama,
    /// porque depende de su contenido; aca solo se recorta para que el panel nunca
    /// salga mas grande que el monitor.
    /// </summary>
    public void SetPanel(int sidePixels)
    {
        panelSide = sidePixels;
        Mode = WindowMode.Panel;
        Reposition();
    }

    // ------------------------------------------------------------------
    // Mover y redimensionar la franja
    // ------------------------------------------------------------------

    /// <summary>
    /// Lleva la esquina de arriba a la izquierda de la franja a ese punto del
    /// escritorio, sin cambiarle el tamaño. Se recorta para que no se salga del
    /// monitor en el que cae.
    /// </summary>
    public void MoveStage(Vector2Int topLeft)
    {
        MakeStageCustom();
        customRect.position = topLeft;
        Reposition();
    }

    /// <summary>
    /// Le da a la franja ese rectangulo del escritorio. Solo cambia cuanto
    /// mundo se ve: los personajes no se mueven ni cambian de tamaño.
    ///
    /// anchor dice que esquina queda quieta (la opuesta a la que se agarro):
    /// x = 1 si es la de la derecha, y = 1 si es la de abajo. Si el rectangulo
    /// queda mas chico que el minimo, crece alejandose de esa esquina.
    /// </summary>
    public void ResizeStage(RectInt rect, Vector2Int anchor)
    {
        MakeStageCustom();

#if UNITY_STANDALONE_WIN
        // Lo que se pase del monitor se recorta, en vez de empujar la franja:
        // si no, al tirar de una esquina contra el borde se correria la otra.
        RectInt m = MonitorAt(Vector2Int.RoundToInt(rect.center)).Full;
        int xMin = Mathf.Max(rect.xMin, m.xMin);
        int yMin = Mathf.Max(rect.yMin, m.yMin);
        int xMax = Mathf.Min(rect.xMax, m.xMax);
        int yMax = Mathf.Min(rect.yMax, m.yMax);
        rect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
#endif

        int width = Mathf.Max(rect.width, minStageWidth);
        int height = Mathf.Max(rect.height, minStageHeight);
        int x = anchor.x > 0 ? rect.xMax - width : rect.xMin;
        int y = anchor.y > 0 ? rect.yMax - height : rect.yMin;

        customRect = new RectInt(x, y, width, height);
        Reposition();
    }

    /// <summary>
    /// Cambia el tamaño de los personajes. No toca la ventana, salvo la franja
    /// automatica, que crece con ellos para que siempre entren.
    /// </summary>
    public void SetPetScale(float scale)
    {
        scale = SnapScale(scale);
        if (Mathf.Approximately(scale, stageScale)) return;

        stageScale = scale;
        PlayerPrefs.SetFloat(ScaleKey, stageScale);
        PlayerPrefs.Save();
        Reposition();
    }

    /// <summary>Redondea al salto de tamaño permitido y lo recorta al rango.</summary>
    public float SnapScale(float scale)
    {
        float step = Mathf.Max(0.01f, stageScaleStep);
        float snapped = Mathf.Round(scale / step) * step;
        return Mathf.Clamp(snapped, minStageScale, maxStageScale);
    }

    /// <summary>Alto de la franja automatica en pixeles para un tamaño de personajes dado.</summary>
    public int StageHeightFor(float scale) => Mathf.Max(1, Mathf.RoundToInt(stripHeight * scale));

    /// <summary>
    /// Vuelve a la franja de siempre: ancho completo, sobre la barra de tareas.
    /// El tamaño de los personajes no se toca: tiene sus propios botones.
    /// </summary>
    public void ResetStage()
    {
        customStage = false;
        PlayerPrefs.DeleteKey(StageKey);
        PlayerPrefs.Save();
        Reposition();
    }

    /// <summary>Guarda como quedo la franja. Se llama al terminar de acomodarla, no en cada frame.</summary>
    public void SaveStage()
    {
        if (!customStage) return;

        PlayerPrefs.SetString(StageKey, string.Join(",",
            customRect.x.ToString(CultureInfo.InvariantCulture),
            customRect.y.ToString(CultureInfo.InvariantCulture),
            customRect.width.ToString(CultureInfo.InvariantCulture),
            customRect.height.ToString(CultureInfo.InvariantCulture)));
        PlayerPrefs.SetInt(MonitorKey, currentMonitorIndex);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Lugar extra que necesita alguien (el menu de la esquina) por encima y a
    /// la izquierda de la esquina de abajo a la derecha del escenario, en
    /// pixeles. Vector2Int.zero lo libera.
    /// </summary>
    public void SetOverlay(Vector2Int sizePixels)
    {
        if (overlay == sizePixels) return;
        overlay = sizePixels;
        Reposition();
    }

    private void MakeStageCustom()
    {
        if (customStage) return;

        // La primera vez que se toca, arranca de donde estaba la franja automatica.
        customStage = true;
        customRect = Stage;
    }

    private void LoadStage()
    {
        customStage = false;
        stageScale = SnapScale(PlayerPrefs.GetFloat(ScaleKey, 1f));

        string[] parts = PlayerPrefs.GetString(StageKey, "").Split(',');
        if (parts.Length != 4) return;

        if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) &&
            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) &&
            int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h))
        {
            customStage = true;
            customRect = new RectInt(x, y, w, h);
        }
    }

    // ------------------------------------------------------------------
    // Siempre visible
    // ------------------------------------------------------------------

    public void SetAlwaysOnTop(bool value)
    {
        if (alwaysOnTop == value) return;
        alwaysOnTop = value;
        Reposition();
    }

    public void ToggleAlwaysOnTop() => SetAlwaysOnTop(!alwaysOnTop);

    // ------------------------------------------------------------------
    // Cursor
    // ------------------------------------------------------------------

    /// <summary>
    /// Lee el cursor y el boton izquierdo por Win32, no por el Input System.
    /// Cuando la ventana es click-through no recibe mensajes de mouse, asi que
    /// Unity dejaria de ver el cursor justo cuando hace falta decidir si volver
    /// a capturarlo. GetCursorPos es global y no tiene ese problema.
    /// </summary>
    private void UpdateCursor()
    {
        bool leftDown = false;

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (!Win32.GetCursorPos(out Win32.POINT cursor))
        {
            CursorOverContent = false;
            LeftPressedThisFrame = false;
            return;
        }

        CursorScreenPixel = new Vector2Int(cursor.x, cursor.y);

        // Windows mide desde arriba-izquierda de la PANTALLA; Unity desde
        // abajo-izquierda de la VENTANA. Hay que trasladar y dar vuelta la Y.
        float localX = cursor.x - Bounds.x;
        float localY = Bounds.height - (cursor.y - Bounds.y);
        Vector2 screenPoint = new Vector2(localX, localY);

        leftDown = Win32.IsLeftMouseDown();
#else
        Vector2 screenPoint = Vector2.zero;
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            screenPoint = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            leftDown = UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
        }

        // En el Editor no hay ventana que mover: se simula como si el Game
        // View fuera la ventana, para que la logica al menos corra.
        CursorScreenPixel = new Vector2Int(
            Bounds.x + Mathf.RoundToInt(screenPoint.x),
            Bounds.y + Bounds.height - Mathf.RoundToInt(screenPoint.y));
#endif

        if (cam != null)
        {
            CursorWorldPosition = cam.ScreenToWorldPoint(
                new Vector3(screenPoint.x, screenPoint.y, -cam.transform.position.z));

            Collider2D hit = Physics2D.OverlapPoint(CursorWorldPosition, interactiveLayers);
            CursorOverContent = hit != null;
        }

        LeftPressedThisFrame = leftDown && !leftWasDown;
        leftWasDown = leftDown;
    }

    // ------------------------------------------------------------------
    // Ventana
    // ------------------------------------------------------------------

    private void MakeTransparentOverlay()
    {
        Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, Win32.WS_POPUP | Win32.WS_VISIBLE);

        ApplyClickThrough(true);

        Win32.SetLayeredWindowAttributes(hwnd, 0, 255, Win32.LWA_ALPHA);

        EnablePerPixelAlpha();
    }

    private void EnablePerPixelAlpha()
    {
        IntPtr emptyRegion = Win32.CreateRectRgn(0, 0, -1, -1);

        var blurBehind = new Win32.DWM_BLURBEHIND
        {
            dwFlags = Win32.DWM_BB_ENABLE | Win32.DWM_BB_BLURREGION,
            fEnable = true,
            hRgnBlur = emptyRegion,
            fTransitionOnMaximized = false
        };

        int hr = Win32.DwmEnableBlurBehindWindow(hwnd, ref blurBehind);
        Win32.DeleteObject(emptyRegion);

        if (hr != 0)
            Debug.LogError($"[DesktopWindow] DwmEnableBlurBehindWindow devolvio 0x{hr:X8}.");
    }

    private void ApplyClickThrough(bool value)
    {
        if (hwnd == IntPtr.Zero) return;
        if (clickThroughApplied == value && Time.frameCount > 1) return;
        clickThroughApplied = value;

        uint style = Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW;
        if (value) style |= Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, style);

        Debug.Log($"[DesktopWindow] click-through={value} exStyle=0x{style:X8}");
    }

    private void Reposition()
    {
        RectInt stage = CalculateStage(out RectInt limits);
        RectInt target = stage;

        // El menu pide lugar arriba y a la izquierda de su esquina. La ventana
        // crece para abarcarlo, pero nunca mas alla del monitor.
        if (Mode == WindowMode.Strip && (overlay.x > 0 || overlay.y > 0))
        {
            int xMin = Mathf.Max(limits.xMin, Mathf.Min(stage.xMin, stage.xMax - overlay.x));
            int yMin = Mathf.Max(limits.yMin, Mathf.Min(stage.yMin, stage.yMax - overlay.y));
            target = new RectInt(xMin, yMin, stage.xMax - xMin, stage.yMax - yMin);
        }

        if (hwnd != IntPtr.Zero)
        {
            bool onTop = alwaysOnTop || Mode == WindowMode.Panel;

            Win32.SetWindowPos(hwnd,
                onTop ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST,
                target.x, target.y, target.width, target.height,
                Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_FRAMECHANGED);
        }

        Stage = stage;
        Bounds = target;
        ApplyCamera(target, stage);
    }

    /// <summary>
    /// Zoom y posicion de la camara. El centro del escenario es siempre el
    /// origen del mundo; si la ventana es mas grande que el escenario, la
    /// camara se corre para que el escenario no se mueva en pantalla.
    /// </summary>
    private void ApplyCamera(RectInt window, RectInt stage)
    {
        float unitsPerPixel = WorldUnitsPerPixel;

        // La franja se apoya en el piso: cambie lo que cambie el alto de la
        // ventana, los personajes siguen parados donde estaban. El panel arma
        // su layout alrededor del origen, asi que ese va centrado.
        float worldHeight = stage.height * unitsPerPixel;
        float bottom = Mode == WindowMode.Panel ? -worldHeight * 0.5f : FloorY;

        StageWorldRect = new Rect(
            -stage.width * 0.5f * unitsPerPixel,
            bottom,
            stage.width * unitsPerPixel,
            worldHeight);

        if (cam == null || !cam.orthographic) return;

        cam.orthographicSize = window.height * 0.5f * unitsPerPixel;

        // Pixeles de pantalla: la Y crece hacia abajo, en el mundo hacia arriba.
        float dx = window.center.x - stage.center.x;
        float dy = window.center.y - stage.center.y;

        Vector3 p = cam.transform.position;
        cam.transform.position = new Vector3(
            dx * unitsPerPixel,
            StageWorldRect.center.y - dy * unitsPerPixel,
            p.z);
    }

    /// <summary>
    /// Rectangulo del escenario, y dentro de que limites puede crecer la
    /// ventana (el monitor en el que esta).
    /// </summary>
    private RectInt CalculateStage(out RectInt limits)
    {
#if UNITY_STANDALONE_WIN
        if (Mode == WindowMode.Panel)
        {
            var monitor = GetTargetMonitorArea();
            limits = monitor.Full;
            return CalculatePanel(monitor);
        }

        return customStage ? CalculateCustomStrip(out limits) : CalculateStrip(out limits);
#else
        RectInt r = Mode == WindowMode.Panel
            ? new RectInt(0, 0, panelSide, panelSide)
            : new RectInt(0, 0, Screen.width, StageHeightFor(stageScale));
        limits = r;
        return r;
#endif
    }

#if UNITY_STANDALONE_WIN
    private Win32.MonitorArea GetTargetMonitorArea()
    {
        var monitors = Win32.GetMonitors();
        if (monitors != null && monitors.Count > 0)
        {
            int idx = Mathf.Clamp(currentMonitorIndex, 0, monitors.Count - 1);
            return monitors[idx];
        }

        return PrimaryScreenFallback();
    }

    private static Win32.MonitorArea PrimaryScreenFallback()
    {
        int w = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
        int h = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);

        return new Win32.MonitorArea
        {
            Index = 0,
            X = 0,
            Y = 0,
            Width = w,
            Height = h,
            Full = new RectInt(0, 0, w, h)
        };
    }

    /// <summary>La franja automatica: ancho completo, sobre la barra de tareas.</summary>
    private RectInt CalculateStrip(out RectInt limits)
    {
        var monitor = GetTargetMonitorArea();
        limits = monitor.Full;

        int screenW = monitor.Width;
        int screenH = monitor.Height;
        int bottom = monitor.Y + screenH;

        // Si es el monitor principal (Index 0), respetamos la barra de tareas si no se autooculta
        if (monitor.Index == 0 && Win32.TryGetTaskbar(out Win32.RECT bar, out uint edge, out bool autoHide) && !autoHide)
        {
            if (edge == Win32.ABE_BOTTOM) bottom = bar.top;
        }

        bottom += verticalOffset;

        int height = StageHeightFor(stageScale);
        return new RectInt(monitor.X, bottom - height, screenW, height);
    }

    /// <summary>
    /// La franja acomodada a mano. Se recorta contra el monitor entero (no el
    /// area de trabajo) para que se pueda poner encima de la barra de tareas.
    /// El monitor es en el que cae su centro: asi se la puede arrastrar de una
    /// pantalla a otra.
    /// </summary>
    private RectInt CalculateCustomStrip(out RectInt limits)
    {
        var monitor = MonitorAt(Vector2Int.RoundToInt(customRect.center));
        RectInt m = monitor.Full;
        limits = m;
        currentMonitorIndex = monitor.Index;

        int width = Mathf.Clamp(customRect.width, Mathf.Min(minStageWidth, m.width), m.width);
        int height = Mathf.Clamp(customRect.height, Mathf.Min(minStageHeight, m.height), m.height);
        int x = Mathf.Clamp(customRect.x, m.xMin, m.xMax - width);
        int y = Mathf.Clamp(customRect.y, m.yMin, m.yMax - height);

        // Se guarda ya recortado: si no, al arrastrar contra un borde la
        // posicion guardada se seguiria alejando y despues tardaria en volver.
        customRect = new RectInt(x, y, width, height);

        return customRect;
    }

    /// <summary>El monitor que contiene ese punto, o el mas cercano si no cae en ninguno.</summary>
    private Win32.MonitorArea MonitorAt(Vector2Int point)
    {
        var monitors = Win32.GetMonitors();
        if (monitors == null || monitors.Count == 0) return PrimaryScreenFallback();

        Win32.MonitorArea best = monitors[0];
        float bestDistance = float.MaxValue;

        foreach (var monitor in monitors)
        {
            RectInt r = monitor.Full;
            if (r.Contains(point)) return monitor;

            float dx = Mathf.Max(r.xMin - point.x, 0, point.x - r.xMax);
            float dy = Mathf.Max(r.yMin - point.y, 0, point.y - r.yMax);
            float distance = dx * dx + dy * dy;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = monitor;
            }
        }

        return best;
    }

    private RectInt CalculatePanel(Win32.MonitorArea monitor)
    {
        int screenW = monitor.Width;
        int screenH = monitor.Height;

        int side = Mathf.Clamp(panelSide, 64, Mathf.Min(screenW, screenH));

        return new RectInt(
            monitor.X + (screenW - side) / 2,
            monitor.Y + (screenH - side) / 2,
            side,
            side);
    }
#endif

    public void SwitchToMonitor(int monitorIndex)
    {
#if UNITY_STANDALONE_WIN
        // Una franja acomodada a mano se lleva al mismo lugar relativo del
        // otro monitor. Si no, se perderia lo que el jugador acomodo.
        if (customStage)
        {
            var monitors = Win32.GetMonitors();
            if (monitors != null && monitorIndex >= 0 && monitorIndex < monitors.Count)
            {
                RectInt from = MonitorAt(customRect.position).Full;
                RectInt to = monitors[monitorIndex].Full;
                customRect.position = customRect.position - from.position + to.position;
            }
        }
#endif

        currentMonitorIndex = monitorIndex;
        PlayerPrefs.SetInt(MonitorKey, currentMonitorIndex);
        PlayerPrefs.Save();

        Reposition();
        SaveStage();
    }
}
