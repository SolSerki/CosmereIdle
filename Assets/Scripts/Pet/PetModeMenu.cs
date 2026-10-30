using UnityEngine;

/// <summary>
/// Menu de modos de un personaje: click derecho sobre el personaje y arriba de
/// su cabeza aparecen cuatro globos (Descanso, Lectura, Caminata, Todos). Click
/// en uno cambia el modo de ese personaje (<see cref="MascotaController"/>);
/// click en cualquier otro lado, o click derecho de nuevo, lo cierra. El click
/// derecho tambien corta la frase que el personaje este diciendo.
///
/// Es un solo objeto de la escena con los cuatro globos ya armados (prefab
/// ModeBubble), que se ubica arriba del personaje elegido. No crea nada.
///
/// Igual que los globos de dialogo: no crece con los personajes (lleva la
/// escala inversa a la de la franja), sigue el tamaño de texto elegido, no se
/// sale por los costados, y si arriba no entra le pide lugar a la ventana.
/// </summary>
public class PetModeMenu : MonoBehaviour
{
    [Tooltip("Los globos, en el orden en que se ven de izquierda a derecha.")]
    [SerializeField] private PetModeOption[] options;

    [Tooltip("Separacion entre globos, en unidades de mundo a escala 1.")]
    [SerializeField] private float gap = 0.06f;

    [Tooltip("Aire entre la cabeza y las puntas de las colitas.")]
    [SerializeField] private float headGap = 0.08f;

    [Header("Colores")]
    [SerializeField] private Color textColor = new Color32(34, 32, 52, 255);

    [Tooltip("Texto del modo que tiene ahora el personaje.")]
    [SerializeField] private Color currentTextColor = new Color(0.16f, 0.5f, 0.27f, 1f);

    [Tooltip("Tinte del globo con el cursor encima.")]
    [SerializeField] private Color hoverTint = new Color(0.8f, 0.9f, 1f, 1f);

    [Tooltip("Se cierra solo si pasa este tiempo sin que el cursor ande por el menu: un " +
             "click en el escritorio no le llega (la ventana es click-through ahi).")]
    [SerializeField] private float autoCloseSeconds = 6f;

    private MascotaController target;
    private float lastActivity;
    private float laidOutScale = -1f;

    // Para quien esta abierto. Estatico porque el personaje lo pregunta (se
    // queda quieto mientras se elige su modo) y hay un solo menu.
    private static MascotaController openFor;

    /// <summary>Si el menu esta abierto sobre ese personaje.</summary>
    public static bool IsOpenFor(MascotaController pet) => pet != null && openFor == pet;

    private void Awake() => Show(false);

    private void OnDisable() => Close();

    private void Update()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null || window.Mode != DesktopWindow.WindowMode.Strip || LayoutEditor.IsEditing)
        {
            Close();
            return;
        }

        Vector3 cursor = window.CursorWorldPosition;

        if (window.RightPressedThisFrame)
        {
            MascotaController pet = PetAt(cursor);

            // Click derecho sobre un personaje que esta hablando: corta la frase,
            // que si no queda encimada con los globos del menu.
            if (pet != null) pet.GetComponent<PetSpeech>()?.Hush();

            if (pet != null && pet != target) Open(pet);
            else Close();
        }

        if (target == null) return;
        if (!target.isActiveAndEnabled)
        {
            Close();
            return;
        }

        PetModeOption hovered = OptionAt(cursor);

        if (window.LeftPressedThisFrame)
        {
            if (hovered != null) target.SetMode(hovered.Mode);
            Close();
            return;
        }

        if (hovered != null) lastActivity = Time.time;
        if (Time.time - lastActivity > autoCloseSeconds)
        {
            Close();
            return;
        }

        Layout(window, hovered);
    }

    private void Open(MascotaController pet)
    {
        target = pet;
        openFor = pet;
        lastActivity = Time.time;
        laidOutScale = -1f;
        Show(true);
    }

    private void Close()
    {
        if (target == null && laidOutScale < 0f) return;

        target = null;
        openFor = null;
        laidOutScale = -1f;
        Show(false);

        DesktopWindow window = DesktopWindow.Instance;
        if (window != null) window.SetOverlay(this, new RectInt());
    }

    private void Show(bool visible)
    {
        foreach (PetModeOption option in options)
            if (option != null && option.gameObject.activeSelf != visible) option.gameObject.SetActive(visible);
    }

    /// <summary>El personaje bajo el cursor, si tiene modos (los del selector estan congelados).</summary>
    private static MascotaController PetAt(Vector3 cursor)
    {
        foreach (Collider2D hit in Physics2D.OverlapPointAll(cursor))
        {
            var pet = hit.GetComponent<MascotaController>();
            if (pet != null && pet.isActiveAndEnabled) return pet;
        }
        return null;
    }

    private PetModeOption OptionAt(Vector3 cursor)
    {
        foreach (PetModeOption option in options)
            if (option != null && option.gameObject.activeSelf && option.Contains(cursor)) return option;
        return null;
    }

    private void Layout(DesktopWindow window, PetModeOption hovered)
    {
        // Los globos se miden de nuevo solo si cambio el tamaño del texto.
        float k = BubbleTextSize.Scale;
        if (!Mathf.Approximately(k, laidOutScale))
        {
            foreach (PetModeOption option in options) option.Layout(k);
            laidOutScale = k;
        }

        // En sus unidades: la fila entera y lo que mide el mas alto.
        float rowWidth = gap * (options.Length - 1);
        float rowHeight = 0f;
        foreach (PetModeOption option in options)
        {
            rowWidth += option.Size.x;
            rowHeight = Mathf.Max(rowHeight, option.TailDrop + option.Size.y);
        }

        // Escala inversa a la de la franja: no crece con los personajes.
        float s = 1f / Mathf.Max(0.01f, window.StageScale);
        transform.localScale = new Vector3(s, s, 1f);
        float edge = 0.05f * s;

        // La raiz va donde apuntan las colitas: arriba de la cabeza.
        Vector3 pet = target.transform.position;
        var speech = target.GetComponent<PetSpeech>();
        float head = speech != null ? speech.HeadTop() : 0f;

        Rect stage = window.StageWorldRect;
        float half = rowWidth * 0.5f * s;
        float x = Mathf.Clamp(pet.x, stage.xMin + half + edge, Mathf.Max(stage.xMin + half + edge, stage.xMax - half - edge));
        float y = pet.y + head + headGap;

        // Si arriba no entra, la ventana crece hasta el monitor; recien ahi se baja.
        float top = y + rowHeight * s + edge;
        window.SetOverlay(this, top <= stage.yMax
            ? new RectInt()
            : window.WorldToDesktopRect(new Rect(stage.xMin, stage.yMax, stage.width, top - stage.yMax)));
        float screenTop = window.DesktopToWorldRect(window.Limits).yMax;
        y = Mathf.Min(y, screenTop - rowHeight * s - edge);

        // El borde izquierdo de la fila, justo sobre un pixel de pantalla: los
        // globos son piezas de pixel art y entre dos pixeles se ven cortes. Los
        // anchos y la separacion ya son pixeles enteros, asi que alcanza con eso.
        Vector2 corner = window.SnapToPixel(new Vector2(x - half, y));
        x = corner.x + half;
        y = corner.y;

        transform.position = new Vector3(x, y, 0f);

        // De izquierda a derecha, todos apoyados en la misma linea; las colitas
        // se corren para apuntar al personaje.
        float left = -rowWidth * 0.5f;
        foreach (PetModeOption option in options)
        {
            Vector2 size = option.Size;
            float cx = left + size.x * 0.5f;
            option.transform.localPosition = new Vector3(cx, option.TailDrop + size.y * 0.5f, 0f);
            option.SetTailX((pet.x - x) / s - cx);
            option.SetLook(option.Mode == target.CurrentMode, option == hovered, textColor, currentTextColor, hoverTint);
            left += size.x + gap;
        }
    }
}
