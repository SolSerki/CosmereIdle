using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro; // Requerido para TextMeshPro

/// <summary>
/// Selector de personajes con scroll vertical y etiquetas de nombres.
/// </summary>
public class CharacterPicker : MonoBehaviour
{
    public static CharacterPicker Instance { get; private set; }

    [Tooltip("Los prefabs de personaje, en el orden en que se muestran.")]
    [SerializeField] private GameObject[] characters;

    [Tooltip("Marcador que aparece arriba de cada personaje elegido.")]
    [SerializeField] private GameObject selectionMarker;

    [Header("Panel de seleccion")]
    [Tooltip("Fondo del panel.")]
    [SerializeField] private GameObject panelBackground;

    [SerializeField] private int gridColumns = 0;
    [SerializeField] private Vector2 cellSize = new Vector2(1.4f, 1.8f); // Aumentamos un poco Y para dar lugar al texto
    [SerializeField] private Vector2 panelPadding = new Vector2(0.4f, 1.5f);
    [SerializeField] private float menuBand = 1f;
    [SerializeField] private Color unselectedTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private float markerHeight = 1.12f;

    [Header("Etiquetas de Nombres")]
    [Tooltip("Distancia hacia abajo desde los pies del personaje para ubicar el texto.")]
    [SerializeField] private float nameOffsetY = 0.25f;
    [Tooltip("Tamaño de la fuente para el nombre.")]
    [SerializeField] private float nameFontSize = 2.5f;
    [Tooltip("Fuente TMP a utilizar (opcional, si queda vacío usa la estándar).")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Mascotas")]
    [SerializeField] private float petSpacing = 1.6f;
    [SerializeField] private string prefsKey = "cosmereidle.personajes";

    [Header("Scroll de Personajes")]
    [SerializeField] private int maxVisibleRows = 2;
    [SerializeField] private float arrowOffset = 0.5f; 
    [SerializeField] private GameObject upArrowCollider;
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

        foreach (var hit in Physics2D.OverlapPointAll(window.CursorWorldPosition))
        {
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
        currentRowOffset = 0;

        int count = characters != null ? characters.Length : 0;
        if (count == 0)
        {
            Debug.LogWarning("[CharacterPicker] No hay personajes asignados.");
            return;
        }

        currentColumns = gridColumns > 0 ? gridColumns : Mathf.CeilToInt(Mathf.Sqrt(count));
        totalRows = Mathf.CeilToInt(count / (float)currentColumns);

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

        // Instanciar personajes y crear su texto abajo
        for (int i = 0; i < count; i++)
        {
            var instance = Instantiate(characters[i], Vector3.zero, Quaternion.identity, transform);
            instance.name = characters[i].name;
            Freeze(instance);
            
            // Creamos el texto hijo para el nombre del personaje
            CreateNameLabel(instance, characters[i].name);

            row.Add(instance);
            cellFeet.Add(Vector2.zero);
        }

        IsPicking = true;
        UpdateGridPositions();
    }

    /// <summary>
    /// Crea un objeto TextMeshPro como hijo del personaje para que se mueva y oculte con él.
    /// </summary>
 /// <summary>
    /// Crea un texto TextMeshPro proporcionado a la escala de la mascota.
    /// </summary>
    /// <summary>
    /// Crea un texto TextMeshPro centrado y ajustado al tamaño de la celda.
    /// </summary>
    private void CreateNameLabel(GameObject parent, string rawName)
    {
        // 1. Limpiamos nombres técnicos como "Joch_Spritesheet_0" -> "Joch"
        string cleanName = rawName.Replace("(Clone)", "").Trim();
        if (cleanName.Contains("_"))
        {
            cleanName = cleanName.Split('_')[0];
        }

        GameObject textObj = new GameObject("CharacterNameLabel");
        textObj.transform.SetParent(parent.transform, false);

        // Alineado justo abajo de los pies (-0.08f es ideal para no chocar la fila inferior)
        textObj.transform.localPosition = new Vector3(0f, -0.08f, 0f);

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = cleanName;
        // Top y Center para que el origen sea la parte superior del texto
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.sortingOrder = 15; // Por encima de fondos y sprites

        RectTransform rt = textObj.GetComponent<RectTransform>();
        if (rt != null) 
        {
            // Pivot arriba al centro para un anclaje predecible
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(cellSize.x * 0.95f, 0.35f);
        }

        tmp.fontSize = 0.32f;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        if (fontAsset != null)
            tmp.font = fontAsset;
    }

    private void UpdateGridPositions()
    {
        int count = row.Count;
        int visibleRows = Mathf.Min(totalRows, maxVisibleRows);
        
        float gridHeight = (visibleRows - 1) * cellSize.y + markerHeight;
        float contentHeight = gridHeight + menuBand;
        float contentBottom = -contentHeight * 0.5f;
        float topFeetY = contentBottom + menuBand + (visibleRows - 1) * cellSize.y;

        for (int i = 0; i < count; i++)
        {
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

        if (upArrowCollider != null) 
        {
            upArrowCollider.SetActive(currentRowOffset > 0);
            upArrowCollider.transform.position = new Vector3(0f, topFeetY + markerHeight + arrowOffset, 0f);
        }
        
        if (downArrowCollider != null) 
        {
            downArrowCollider.SetActive(currentRowOffset < totalRows - maxVisibleRows);
            float bottomFeetY = topFeetY - (visibleRows - 1) * cellSize.y;
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

            // También podemos atenuar el texto si el personaje no está seleccionado
            var label = row[i].GetComponentInChildren<TextMeshPro>();
            if (label != null)
                label.color = on ? Color.white : unselectedTint;

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