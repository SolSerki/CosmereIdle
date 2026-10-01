using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterPicker : MonoBehaviour
{
    public static CharacterPicker Instance { get; private set; }

    [Tooltip("Los prefabs de personaje, en el orden en que se muestran.")]
    [SerializeField] private GameObject[] characters;

    [Tooltip("Marcador que aparece arriba de cada personaje elegido.")]
    [SerializeField] private GameObject selectionMarker;

    [Header("Panel de seleccion")]
    [SerializeField] private GameObject panelBackground;
    [SerializeField] private int gridColumns = 0;
    [SerializeField] private Vector2 cellSize = new Vector2(1.4f, 1.6f);
    [SerializeField] private Vector2 panelPadding = new Vector2(0.4f, 1.5f);
    [SerializeField] private float menuBand = 1f;
    [SerializeField] private Color unselectedTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private float markerHeight = 1.12f;

    [Header("Mascotas")]
    [SerializeField] private float petSpacing = 1.6f;
    [SerializeField] private string prefsKey = "cosmereidle.personajes";

    [Header("Scroll de Personajes")]
    [Tooltip("Máximo de filas visibles a la vez. Evita que la ventana crezca infinitamente.")]
    [SerializeField] private int maxVisibleRows = 2;
    [Tooltip("Distancia desde los personajes hasta las flechas.")]
    [SerializeField] private float arrowOffset = 0.5f; 
    [Tooltip("Objeto de la flecha Arriba en la escena (necesita Collider2D).")]
    [SerializeField] private GameObject upArrowCollider;
    [Tooltip("Objeto de la flecha Abajo en la escena (necesita Collider2D).")]
    [SerializeField] private GameObject downArrowCollider;


    public bool IsPicking { get; private set; }
    public int SelectedCount => selected.Count;

    private readonly List<GameObject> row = new List<GameObject>();
    private readonly List<Vector2> cellFeet = new List<Vector2>();
    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<GameObject> pets = new List<GameObject>();
    private readonly HashSet<int> selected = new HashSet<int>();

    private GameObject panel;
    private Camera cam;

    // Variables para controlar el estado del scroll
    private int currentRowOffset = 0; 
    private int totalRows = 0;
    private int currentColumns = 1;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

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

        // Detectar clicks usando OverlapPointAll para que el panel de fondo no bloquee el click
        foreach (var hit in Physics2D.OverlapPointAll(window.CursorWorldPosition))
        {
            // 1. Chequear si se tocó alguna flecha de scroll
            if (upArrowCollider != null && hit.transform == upArrowCollider.transform)
            {
                if (currentRowOffset > 0)
                {
                    currentRowOffset--;
                    UpdateGridPositions();
                }
                return;
            }

            if (downArrowCollider != null && hit.transform == downArrowCollider.transform)
            {
                if (currentRowOffset < totalRows - maxVisibleRows)
                {
                    currentRowOffset++;
                    UpdateGridPositions();
                }
                return;
            }

            // 2. Chequear si se tocó a un personaje activo[cite: 3]
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i] == null || !row[i].activeSelf) continue;

                if (hit.transform == row[i].transform || hit.transform.IsChildOf(row[i].transform))
                {
                    Toggle(i);
                    return;
                }
            }
        }
    }

    public void ShowPicker()
    {
        ClearPets();
        ClearRow();
        currentRowOffset = 0; // Reiniciamos el scroll arriba de todo

        int count = characters != null ? characters.Length : 0;
        if (count == 0) return;

        currentColumns = gridColumns > 0 ? gridColumns : Mathf.CeilToInt(Mathf.Sqrt(count));
        totalRows = Mathf.CeilToInt(count / (float)currentColumns);

        // Limitamos las filas para que la ventana calcule su tamaño máximo en base a maxVisibleRows
        int visibleRowsForWindow = Mathf.Min(totalRows, maxVisibleRows);

        float gridWidth = currentColumns * cellSize.x;
        float gridHeight = (visibleRowsForWindow - 1) * cellSize.y + markerHeight;
        float contentHeight = gridHeight + menuBand;

        float side = Mathf.Max(gridWidth + panelPadding.x * 2f,
                               contentHeight + panelPadding.y * 2f);

        DesktopWindow window = DesktopWindow.Instance;
        if (window != null)
            window.SetPanel(Mathf.RoundToInt(side * window.PixelsPerUnit));

        ShowPanel(side);

        // Instanciamos todos los personajes pero aún no les asignamos posición
        for (int i = 0; i < count; i++)
        {
            var instance = Instantiate(characters[i], Vector3.zero, Quaternion.identity, transform);
            instance.name = characters[i].name;
            Freeze(instance);
            row.Add(instance);
            cellFeet.Add(Vector2.zero);
        }

        IsPicking = true;
        
        // Esta nueva función calculará qué personajes se ven y dónde
        UpdateGridPositions();
    }

    /// <summary>
    /// Calcula las posiciones de los personajes según el offset actual del scroll,
    /// ocultando los que queden por fuera del rango visible.
    /// </summary>
    private void UpdateGridPositions()
        {
            int count = row.Count;
            int visibleRows = Mathf.Min(totalRows, maxVisibleRows);
            
            float gridHeight = (visibleRows - 1) * cellSize.y + markerHeight;
            float contentHeight = gridHeight + menuBand;
            float contentBottom = -contentHeight * 0.5f;
            
            // Esta es la altura de los pies de la fila superior
            float topFeetY = contentBottom + menuBand + (visibleRows - 1) * cellSize.y;

            for (int i = 0; i < count; i++)
            {
                // ... (código intacto del paso anterior que posiciona y apaga personajes)
                int rowIndex = i / currentColumns;
                int column = i % currentColumns;

                if (rowIndex < currentRowOffset || rowIndex >= currentRowOffset + maxVisibleRows)
                {
                    row[i].SetActive(false);
                    continue;
                }

                row[i].SetActive(true);
                int relativeRow = rowIndex - currentRowOffset;
                int inThisRow = Mathf.Min(currentColumns, count - rowIndex * currentColumns);

                var feet = new Vector2(
                    (column - (inThisRow - 1) * 0.5f) * cellSize.x,
                    topFeetY - relativeRow * cellSize.y);

                row[i].transform.position = new Vector3(feet.x, feet.y, 0f);
                cellFeet[i] = feet;
            }

            // --- NUEVO: POSICIONAMIENTO AUTOMÁTICO DE FLECHAS ---
            
            if (upArrowCollider != null) 
            {
                // Apagamos o prendemos según si podemos subir
                upArrowCollider.SetActive(currentRowOffset > 0);
                // La flecha superior va centrada (X=0) y por encima del tope (pies + altura del marcador + offset)
                upArrowCollider.transform.position = new Vector3(0f, topFeetY + markerHeight + arrowOffset, 0f);
            }
            
            if (downArrowCollider != null) 
            {
                // Apagamos o prendemos según si podemos bajar
                downArrowCollider.SetActive(currentRowOffset < totalRows - maxVisibleRows);
                // Calculamos los pies de la fila más baja que se está mostrando actualmente
                float bottomFeetY = topFeetY - (visibleRows - 1) * cellSize.y;
                // La flecha inferior va centrada (X=0) y por debajo de esa última fila
                downArrowCollider.transform.position = new Vector3(0f, bottomFeetY - arrowOffset, 0f);
            }

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

    public void Confirm()
    {
        if (selected.Count == 0) return;

        SaveSelection();
        ClearRow();
        IsPicking = false;

        DesktopWindow window = DesktopWindow.Instance;
        if (window != null) window.SetStrip();

        SpawnPets();
    }

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

            // IMPORTANTE: Si el personaje está oculto por el scroll, no dibujamos el marcador[cite: 3]
            if (!on || selectionMarker == null || i >= cellFeet.Count || !row[i].activeSelf) continue;

            var marker = Instantiate(selectionMarker,
                new Vector3(cellFeet[i].x, cellFeet[i].y + markerHeight, 0f),
                Quaternion.identity, transform);
            markers.Add(marker);
        }
    }

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

    private void Freeze(GameObject instance)
    {
        var walker = instance.GetComponent<StripWalker>();
        if (walker != null) walker.enabled = false;
        var mascota = instance.GetComponent<MascotaController>();
        if (mascota != null) mascota.enabled = false;
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

    private float FloorY() => cam != null ? -cam.orthographicSize : -1f;
}