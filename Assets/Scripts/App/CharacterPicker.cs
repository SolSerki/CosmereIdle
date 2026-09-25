using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Selector de personajes. Abre un panel cuadrado en el medio de la pantalla con
/// todo el plantel en cuadricula, se pueden marcar varios, y al confirmar la
/// ventana vuelve a ser la franja y los elegidos quedan como mascotas. La
/// seleccion se recuerda entre sesiones.
///
/// La ventana cambia de forma pero la escena NO cambia: la ventana transparente
/// la maneja DesktopWindow desde la camara de Main, asi que si se cargara otra
/// escena esa camara se destruiria, la franja perderia el click-through y el
/// posicionamiento, y habria que rearmar todo al volver.
/// </summary>
public class CharacterPicker : MonoBehaviour
{
    public static CharacterPicker Instance { get; private set; }

    [Tooltip("Los prefabs de personaje, en el orden en que se muestran.")]
    [SerializeField] private GameObject[] characters;

    [Tooltip("Marcador que aparece arriba de cada personaje elegido.")]
    [SerializeField] private GameObject selectionMarker;

    [Header("Panel de seleccion")]
    [Tooltip("Fondo del panel. Ademas de dar superficie, su collider es lo que hace " +
             "que mientras se elige el cuadrado entero se coma los clicks en vez de " +
             "dejarlos pasar al escritorio.")]
    [SerializeField] private GameObject panelBackground;

    [Tooltip("Columnas de la cuadricula. 0 = automatico (raiz cuadrada del plantel), " +
             "que es lo que mantiene el panel mas o menos cuadrado cuando se suman " +
             "personajes.")]
    [SerializeField] private int gridColumns = 0;

    [Tooltip("Tamaño de cada celda de la cuadricula, en unidades de mundo. " +
             "Un personaje mide 0.96 x 0.96.")]
    [SerializeField] private Vector2 cellSize = new Vector2(1.4f, 1.6f);

    [Tooltip("Aire entre la cuadricula y el borde del panel, en unidades de mundo. " +
             "El de arriba tambien deja lugar para los botones de la esquina.")]
    [SerializeField] private Vector2 panelPadding = new Vector2(0.4f, 0.7f);

    [Tooltip("Franja que se reserva abajo de todo para el menu (elegir / continuar / " +
             "cerrar). El panel crece para que entre, en vez de que el menu se coma " +
             "la ultima fila de personajes.")]
    [SerializeField] private float menuBand = 1f;

    [Tooltip("Color de los que NO estan elegidos: se apagan para que resalten los elegidos.")]
    [SerializeField] private Color unselectedTint = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Altura del marcador sobre los pies. Va a altura fija y no sobre la " +
             "cabeza de cada uno porque no miden todos lo mismo y quedaria un serrucho.")]
    [SerializeField] private float markerHeight = 1.12f;

    [Header("Mascotas")]
    [Tooltip("Separacion entre mascotas al confirmar. Despues cada una camina sola.")]
    [SerializeField] private float petSpacing = 1.6f;

    [SerializeField] private string prefsKey = "cosmereidle.personajes";

    /// <summary>Si el panel de seleccion esta en pantalla.</summary>
    public bool IsPicking { get; private set; }

    /// <summary>Cuantos personajes hay marcados ahora mismo.</summary>
    public int SelectedCount => selected.Count;

    private readonly List<GameObject> row = new List<GameObject>();
    private readonly List<Vector2> cellFeet = new List<Vector2>();
    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<GameObject> pets = new List<GameObject>();
    private readonly HashSet<int> selected = new HashSet<int>();

    private GameObject panel;
    private Camera cam;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    /// <summary>
    /// Siempre se arranca en la pantalla de seleccion, aunque ya haya elegidos de
    /// la sesion pasada: es la unica pantalla donde estan todas las acciones
    /// (elegir spren, continuar, cerrar). Los recordados vienen ya marcados, asi
    /// que para quien no quiere cambiar nada es un solo click en Continuar.
    /// </summary>
    private void Start()
    {
        LoadSelection();
        ShowPicker();
    }

    private void Update()
    {
        if (!IsPicking) return;

        DesktopWindow window = DesktopWindow.Instance;
        if (window == null || !window.LeftPressedThisFrame) return;

        // OverlapPointAll y no OverlapPoint: abajo de los personajes esta el
        // collider del panel, y con un solo resultado podria tocarnos ese y
        // perderiamos el click.
        foreach (var hit in Physics2D.OverlapPointAll(window.CursorWorldPosition))
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i] == null) continue;

                if (hit.transform == row[i].transform || hit.transform.IsChildOf(row[i].transform))
                {
                    Toggle(i);
                    return;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Seleccion
    // ------------------------------------------------------------------

    /// <summary>
    /// Abre el panel: calcula la cuadricula, le pide a la ventana un cuadrado del
    /// tamaño justo, y reparte el plantel.
    /// </summary>
    public void ShowPicker()
    {
        ClearPets();
        ClearRow();

        int count = characters != null ? characters.Length : 0;
        if (count == 0)
        {
            Debug.LogWarning("[CharacterPicker] No hay personajes asignados.");
            return;
        }

        int columns = gridColumns > 0 ? gridColumns : Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt(count / (float)columns);

        // El alto de la cuadricula no es filas * celda: se mide de los pies de la
        // fila de abajo al tope del marcador de la de arriba, que es lo que ocupa.
        float gridWidth = columns * cellSize.x;
        float gridHeight = (rows - 1) * cellSize.y + markerHeight;

        // Abajo de la cuadricula va el menu, y el panel tiene que contener a los dos.
        float contentHeight = gridHeight + menuBand;

        // La ventana es cuadrada, asi que el lado lo manda la dimension mas grande.
        float side = Mathf.Max(gridWidth + panelPadding.x * 2f,
                               contentHeight + panelPadding.y * 2f);

        // Primero la ventana: cambiarle la forma le cambia el orthographicSize a la
        // camara, y todo lo que viene abajo se ubica en unidades de mundo.
        DesktopWindow window = DesktopWindow.Instance;
        if (window != null)
            window.SetPanel(Mathf.RoundToInt(side * window.PixelsPerUnit));

        ShowPanel(side);

        // Cuadricula y menu se centran juntos, y dentro de eso el menu ocupa la
        // franja de abajo: por eso la cuadricula queda un poco corrida hacia arriba.
        float contentBottom = -contentHeight * 0.5f;
        float topFeetY = contentBottom + menuBand + (rows - 1) * cellSize.y;

        for (int i = 0; i < count; i++)
        {
            int rowIndex = i / columns;
            int column = i % columns;

            // La ultima fila puede quedar incompleta; asi se centra sola en vez de
            // quedar pegada a la izquierda.
            int inThisRow = Mathf.Min(columns, count - rowIndex * columns);

            var feet = new Vector2(
                (column - (inThisRow - 1) * 0.5f) * cellSize.x,
                topFeetY - rowIndex * cellSize.y);

            var instance = Instantiate(characters[i],
                new Vector3(feet.x, feet.y, 0f), Quaternion.identity, transform);
            instance.name = characters[i].name;

            Freeze(instance);
            row.Add(instance);
            cellFeet.Add(feet);
        }

        IsPicking = true;
        RefreshIndicators();
    }

    private void ShowPanel(float side)
    {
        if (panelBackground == null) return;

        panel = Instantiate(panelBackground, Vector3.zero, Quaternion.identity, transform);

        var sprite = panel.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.size = new Vector2(side, side);

        var box = panel.GetComponent<BoxCollider2D>();
        if (box != null) box.size = new Vector2(side, side);
    }

    private void Toggle(int index)
    {
        if (!selected.Remove(index)) selected.Add(index);
        RefreshIndicators();
    }

    /// <summary>
    /// Confirma la seleccion. Sin nada marcado no hace nada: es preferible que el
    /// boton no responda a quedarse sin ninguna mascota y sin saber por que.
    /// </summary>
    public void Confirm()
    {
        if (selected.Count == 0) return;

        SaveSelection();
        ClearRow();
        IsPicking = false;

        // La ventana vuelve a ser franja ANTES de instanciar: SpawnPets apoya los
        // pies en FloorY(), que sale del orthographicSize que acaba de cambiar.
        DesktopWindow window = DesktopWindow.Instance;
        if (window != null) window.SetStrip();

        SpawnPets();

        Debug.Log("[CharacterPicker] Confirmado: " + string.Join(", ",
            selected.OrderBy(i => i).Select(i => characters[i].name)));
    }

    /// <summary>Apaga los no elegidos y pone un marcador arriba de los elegidos.</summary>
    private void RefreshIndicators()
    {
        foreach (var m in markers) if (m != null) Destroy(m);
        markers.Clear();

        for (int i = 0; i < row.Count; i++)
        {
            if (row[i] == null) continue;

            bool on = selected.Contains(i);

            var sprite = row[i].GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.color = on ? Color.white : unselectedTint;

            if (!on || selectionMarker == null || i >= cellFeet.Count) continue;

            var marker = Instantiate(selectionMarker,
                new Vector3(cellFeet[i].x, cellFeet[i].y + markerHeight, 0f),
                Quaternion.identity, transform);
            markers.Add(marker);
        }
    }

    // ------------------------------------------------------------------
    // Mascotas
    // ------------------------------------------------------------------

    private void SpawnPets()
    {
        ClearPets();

        var chosen = selected.Where(i => i >= 0 && i < characters.Length).OrderBy(i => i).ToList();
        float floorY = FloorY();
        float start = -(chosen.Count - 1) * petSpacing * 0.5f;

        for (int i = 0; i < chosen.Count; i++)
        {
            var pet = Instantiate(characters[chosen[i]],
                new Vector3(start + i * petSpacing, floorY, 0f), Quaternion.identity, transform);
            pet.name = characters[chosen[i]].name;
            pets.Add(pet);
        }
    }

    /// <summary>
    /// En el panel los personajes tienen que quedarse quietos. Se desactivan los
    /// componentes ANTES de que corra su Start, asi MascotaController nunca
    /// arranca su corrutina.
    /// </summary>
    private void Freeze(GameObject instance)
    {
        var walker = instance.GetComponent<StripWalker>();
        if (walker != null) walker.enabled = false;

        var mascota = instance.GetComponent<MascotaController>();
        if (mascota != null) mascota.enabled = false;

        // Si no, el click que elige tambien le saca un globo de dialogo.
        var speech = instance.GetComponent<PetSpeech>();
        if (speech != null) speech.enabled = false;
    }

    private void ClearRow()
    {
        foreach (var go in row) if (go != null) Destroy(go);
        row.Clear();
        cellFeet.Clear();

        foreach (var m in markers) if (m != null) Destroy(m);
        markers.Clear();

        if (panel != null) Destroy(panel);
        panel = null;
    }

    private void ClearPets()
    {
        foreach (var go in pets) if (go != null) Destroy(go);
        pets.Clear();
    }

    // ------------------------------------------------------------------

    private void SaveSelection()
    {
        PlayerPrefs.SetString(prefsKey, string.Join(",", selected.OrderBy(i => i)));
        PlayerPrefs.Save();
    }

    private void LoadSelection()
    {
        selected.Clear();

        foreach (var piece in PlayerPrefs.GetString(prefsKey, "").Split(','))
            if (int.TryParse(piece, out int i) && i >= 0 && i < characters.Length)
                selected.Add(i);
    }

    /// <summary>Piso de la franja. Con el pivot abajo, apoya los pies exactamente.</summary>
    private float FloorY() => cam != null ? -cam.orthographicSize : -1f;
}
