using System;
using UnityEngine;

/// <summary>
/// Dueño de la ventana del SO. Tiene dos formas y sabe pasar de una a la otra:
///
///   Strip  - franja del ancho de la pantalla apoyada sobre la barra de tareas.
///            Es donde viven las mascotas.
///   Panel  - cuadrado centrado en la pantalla. Es la pantalla de seleccion.
///
/// Es el unico lugar que toca la ventana del SO, y tambien el unico que fija el
/// orthographicSize de la camara: los dos valores son la misma decision, y si se
/// tocan por separado el pixel art deja de caer sobre la grilla de la pantalla.
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
public class DesktopWindow : MonoBehaviour
{
    public enum WindowMode
    {
        /// <summary>Franja del ancho de la pantalla, sobre la barra de tareas.</summary>
        Strip,

        /// <summary>Cuadrado centrado en la pantalla.</summary>
        Panel
    }

    public static DesktopWindow Instance { get; private set; }

    [Header("Franja")]
    [Tooltip("Alto de la franja en pixeles de pantalla. El ancho es el de la pantalla.")]
    [SerializeField] private int stripHeight = 200;

    [Tooltip("Corrimiento vertical en pixeles. Positivo baja la franja sobre la barra de tareas.")]
    [SerializeField] private int verticalOffset = 0;

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

    /// <summary>Rectangulo que ocupa la ventana, en pixeles de pantalla (origen arriba-izquierda).</summary>
    public RectInt Bounds { get; private set; }

    /// <summary>Posicion del cursor en coordenadas de mundo, valga o no el foco.</summary>
    public Vector3 CursorWorldPosition { get; private set; }

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

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

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

        if (autoClickThrough) ApplyClickThrough(!CursorOverContent);

        // La barra de tareas se mueve, cambia de alto y se autooculta; y otras apps
        // topmost nos pueden pasar por encima. Re-chequear sale barato.
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

    /// <summary>Vuelve a la franja sobre la barra de tareas.</summary>
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

        // Windows mide desde arriba-izquierda de la PANTALLA; Unity desde
        // abajo-izquierda de la VENTANA. Hay que trasladar y dar vuelta la Y.
        float localX = cursor.x - Bounds.x;
        float localY = Bounds.height - (cursor.y - Bounds.y);
        Vector2 screenPoint = new Vector2(localX, localY);

        leftDown = Win32.IsLeftMouseDown();
#else
        Vector2 screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
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
        // Sin bordes ni barra de titulo: la ventana es solo contenido.
        Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, Win32.WS_POPUP | Win32.WS_VISIBLE);

        ApplyClickThrough(true);

        // Tiene que ir DESPUES de prender WS_EX_LAYERED: los atributos de capa se
        // resetean cada vez que ese bit pasa de 0 a 1. Alpha 255 = capa totalmente
        // opaca, o sea que no tinta nada; el alpha real lo sigue aportando DWM.
        Win32.SetLayeredWindowAttributes(hwnd, 0, 255, Win32.LWA_ALPHA);

        EnablePerPixelAlpha();
    }

    /// <summary>
    /// Alpha por pixel real. El truco es pedirle a DWM un "blur behind" con una
    /// region VACIA: no difumina nada, pero prende el camino de composicion que
    /// respeta el alpha de cada pixel. El alpha lo aporta la camara.
    /// </summary>
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

    /// <summary>
    /// Prende o apaga que el mouse atraviese la ventana. Se llama seguido, asi que
    /// solo toca la API de Windows cuando el estado cambia de verdad.
    /// </summary>
    private void ApplyClickThrough(bool value)
    {
        if (hwnd == IntPtr.Zero) return;
        if (clickThroughApplied == value && Time.frameCount > 1) return;
        clickThroughApplied = value;

        // WS_EX_LAYERED va SIEMPRE, aunque no queramos click-through. La doc de
        // Microsoft define el pass-through de mouse de WS_EX_TRANSPARENT solo para
        // ventanas layered: "if the LAYERED window has the WS_EX_TRANSPARENT style
        // ... the mouse events will be passed to other windows underneath". Suelto,
        // WS_EX_TRANSPARENT solo habla de orden de pintado entre ventanas hermanas
        // y no deja pasar un solo click.
        uint style = Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW;
        if (value) style |= Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, style);

        Debug.Log($"[DesktopWindow] click-through={value} exStyle=0x{style:X8}");
    }

    private void Reposition()
    {
        RectInt target = CalculateBounds();

        if (hwnd != IntPtr.Zero)
        {
            // El panel va topmost si o si, aunque el usuario haya elegido que la
            // franja se esconda detras de las demas apps: una pantalla de seleccion
            // tapada por otra ventana parece una app colgada.
            bool onTop = alwaysOnTop || Mode == WindowMode.Panel;

            Win32.SetWindowPos(hwnd,
                onTop ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST,
                target.x, target.y, target.width, target.height,
                Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_FRAMECHANGED);
        }

        Bounds = target;
        ApplyCamera(target.height);
    }

    /// <summary>
    /// El zoom de la camara sale del alto de la ventana, no al reves. Es la regla de
    /// pixel perfect: orthographicSize = altoEnPixeles / (2 * PPU). Con eso una unidad
    /// de mundo mide siempre PPU pixeles, asi que los personajes y los botones se ven
    /// exactamente del mismo tamaño en la franja y en el panel, aunque midan distinto.
    /// </summary>
    private void ApplyCamera(int heightPixels)
    {
        if (cam == null || !cam.orthographic) return;

        cam.orthographicSize = heightPixels / (2f * Mathf.Max(1f, pixelsPerUnit));
    }

    private RectInt CalculateBounds()
    {
#if UNITY_STANDALONE_WIN
        return Mode == WindowMode.Panel ? CalculatePanel() : CalculateStrip();
#else
        return Mode == WindowMode.Panel
            ? new RectInt(0, 0, panelSide, panelSide)
            : new RectInt(0, 0, Screen.width, Mathf.Max(1, stripHeight));
#endif
    }

#if UNITY_STANDALONE_WIN
    private RectInt CalculateStrip()
    {
        int screenW = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
        int screenH = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);

        int bottom = screenH;

        if (Win32.TryGetTaskbar(out Win32.RECT bar, out uint edge, out bool autoHide) && !autoHide)
        {
            // Solo los bordes horizontales corren la franja. Con la barra a la
            // izquierda o a la derecha el fondo de la pantalla sigue libre.
            if (edge == Win32.ABE_BOTTOM) bottom = bar.top;
        }

        bottom += verticalOffset;

        int height = Mathf.Max(1, stripHeight);
        return new RectInt(0, bottom - height, screenW, height);
    }

    private RectInt CalculatePanel()
    {
        int screenW = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
        int screenH = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);

        // Si el plantel crece hasta no entrar en pantalla, preferimos recortar el
        // panel antes que abrir una ventana mas grande que el monitor.
        int side = Mathf.Clamp(panelSide, 64, Mathf.Min(screenW, screenH));

        return new RectInt((screenW - side) / 2, (screenH - side) / 2, side, side);
    }
#endif
}
