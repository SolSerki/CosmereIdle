using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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
    [Tooltip("Cuantas filas tarda en desvanecerse un personaje que sale del area visible.")]
    [SerializeField] private float edgeFadeRows = 0.6f;
    [Tooltip("Filas que avanza cada muesca de la rueda del mouse.")]
    [SerializeField] private float wheelRowsPerNotch = 0.5f;
    [Tooltip("Que tan rapido alcanza el scroll a la rueda. Mas alto = mas seco.")]
    [SerializeField] private float scrollSmoothing = 14f;

    [Header("Barra de scroll")]
    [Tooltip("Riel de la barra. Necesita SpriteRenderer y BoxCollider2D: click en el riel salta ahi.")]
    [FormerlySerializedAs("upArrowCollider")]
    [SerializeField] private GameObject scrollTrack;
    [Tooltip("Manija de la barra. Necesita SpriteRenderer y BoxCollider2D.")]
    [FormerlySerializedAs("downArrowCollider")]
    [SerializeField] private GameObject scrollThumb;
    [SerializeField] private float scrollbarWidth = 0.12f;
    [Tooltip("Ancho extra del collider para que la barra sea facil de agarrar.")]
    [SerializeField] private float scrollbarGrabWidth = 0.36f;
    [Tooltip("Distancia entre el borde de la cuadricula y la barra.")]
    [SerializeField] private float scrollbarGap = 0.08f;
    [Tooltip("Distancia minima entre la barra y el borde del panel.")]
    [SerializeField] private float scrollbarMargin = 0.2f;
    [SerializeField] private float minThumbHeight = 0.4f;
    [Tooltip("Lugar que ocupa el nombre debajo de los pies; la barra baja hasta ahi.")]
    [SerializeField] private float labelBand = 0.4f;
    [SerializeField] private Color trackColor = new Color(1f, 1f, 1f, 0.1f);
    [SerializeField] private Color thumbColor = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] private Color thumbActiveColor = new Color(1f, 1f, 1f, 0.75f);

    public bool IsPicking { get; private set; }
    public int SelectedCount => selected.Count;

    // Todas estas listas van por casillero de la cuadricula. displayOrder
    // traduce casillero -> indice en `characters`, que es lo que se guarda.
    private readonly List<int> displayOrder = new List<int>();
    private readonly List<GameObject> row = new List<GameObject>();
    private readonly List<SpriteRenderer> rowSprites = new List<SpriteRenderer>();
    private readonly List<TextMeshPro> rowLabels = new List<TextMeshPro>();
    private readonly List<Vector2> cellFeet = new List<Vector2>();
    private readonly List<float> visibility = new List<float>();
    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<GameObject> pets = new List<GameObject>();
    private readonly HashSet<int> selected = new HashSet<int>();

    private GameObject panel;
    private Camera cam;

    private int totalRows = 0;
    private int visibleRows = 1;
    private int currentColumns = 1;
    private float topFeetY;

    // El scroll se mide en filas: 0 es la primera fila arriba de todo,
    // MaxScroll es la ultima abajo de todo. Es continuo para que la lista
    // pueda quedar a mitad de camino entre dos filas.
    private float scroll;
    private float targetScroll;
    private float appliedScroll = float.NaN;
    private float MaxScroll => Mathf.Max(0, totalRows - visibleRows);
    private bool CanScroll => MaxScroll > 0;

    private float trackTop;
    private float trackBottom;
    private float thumbHeight;
    private bool draggingThumb;
    private float thumbGrabOffset;

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
        if (window == null) return;

        if (window.LeftPressedThisFrame) HandlePress(window);

        if (draggingThumb)
        {
            if (window.IsLeftDown) DragThumb(window);
            else EndThumbDrag();
        }
        else
        {
            ReadWheel(window);
        }

        scroll = Mathf.Lerp(scroll, targetScroll, 1f - Mathf.Exp(-scrollSmoothing * Time.unscaledDeltaTime));
        if (Mathf.Abs(scroll - targetScroll) < 0.001f) scroll = targetScroll;

        if (scroll != appliedScroll) UpdateGridPositions();
        UpdateThumbColor(window);
    }

    private void HandlePress(DesktopWindow window)
    {
        Vector3 cursor = window.CursorWorldPosition;
        var hits = Physics2D.OverlapPointAll(cursor);

        // La barra va primero: su collider es mas ancho que el dibujo y puede
        // pisar el borde de la cuadricula.
        if (CanScroll)
        {
            foreach (var hit in hits)
            {
                if (IsPartOf(hit, scrollThumb))
                {
                    BeginThumbDrag(window, cursor.y - scrollThumb.transform.position.y);
                    return;
                }
            }

            foreach (var hit in hits)
            {
                if (IsPartOf(hit, scrollTrack))
                {
                    // Click en el riel: la manija salta debajo del cursor y
                    // queda agarrada, asi se puede seguir arrastrando.
                    BeginThumbDrag(window, 0f);
                    DragThumb(window);
                    return;
                }
            }
        }

        foreach (var hit in hits)
        {
            for (int i = 0; i < row.Count; i++)
            {
                // Un personaje que esta saliendo del area no se puede elegir:
                // se ve a medias y probablemente el click era para otro.
                if (row[i] == null || !row[i].activeSelf || visibility[i] < 0.5f) continue;

                if (IsPartOf(hit, row[i]))
                {
                    Toggle(displayOrder[i]);
                    return;
                }
            }
        }
    }

    private static bool IsPartOf(Collider2D hit, GameObject target) =>
        target != null && (hit.transform == target.transform || hit.transform.IsChildOf(target.transform));

    private void BeginThumbDrag(DesktopWindow window, float grabOffset)
    {
        draggingThumb = true;
        thumbGrabOffset = grabOffset;
        // Si el cursor se sale del panel mientras arrastra, la ventana tiene
        // que seguir capturando el mouse; si no, el soltar se va al escritorio.
        window.IsDraggingContent = true;
    }

    private void EndThumbDrag()
    {
        if (!draggingThumb) return;
        draggingThumb = false;

        DesktopWindow window = DesktopWindow.Instance;
        if (window != null) window.IsDraggingContent = false;
    }

    private void DragThumb(DesktopWindow window)
    {
        float highest = trackTop - thumbHeight * 0.5f;
        float lowest = trackBottom + thumbHeight * 0.5f;
        if (highest - lowest <= 0f) return;

        float thumbY = window.CursorWorldPosition.y - thumbGrabOffset;
        float t = Mathf.InverseLerp(highest, lowest, thumbY);

        // Arrastrando no se suaviza: la lista tiene que ir pegada a la mano.
        targetScroll = scroll = t * MaxScroll;
    }

    private void ReadWheel(DesktopWindow window)
    {
        if (!CanScroll || !window.CursorOverContent) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float wheel = mouse.scroll.ReadValue().y;
        if (wheel == 0f) return;

        // Segun la plataforma una muesca llega como 120 o como 1. Los
        // touchpads mandan valores chicos y continuos, que se usan tal cual.
        float notches = Mathf.Abs(wheel) >= 30f ? wheel / 120f : wheel;
        targetScroll = Mathf.Clamp(targetScroll - notches * wheelRowsPerNotch, 0f, MaxScroll);
    }

    public void ShowPicker()
    {
        ClearPets();
        ClearRow();
        scroll = targetScroll = 0f;
        appliedScroll = float.NaN;

        int count = characters != null ? characters.Length : 0;
        if (count == 0)
        {
            Debug.LogWarning("[CharacterPicker] No hay personajes asignados.");
            return;
        }

        currentColumns = gridColumns > 0 ? gridColumns : Mathf.CeilToInt(Mathf.Sqrt(count));
        totalRows = Mathf.CeilToInt(count / (float)currentColumns);
        visibleRows = Mathf.Clamp(totalRows, 1, Mathf.Max(1, maxVisibleRows));

        float gridWidth = currentColumns * cellSize.x;
        float gridHeight = (visibleRows - 1) * cellSize.y + markerHeight;
        float contentHeight = gridHeight + menuBand;

        // La barra va a la derecha de la cuadricula. Para que la cuadricula
        // siga centrada, el lugar que ocupa se reserva de los dos lados.
        float halfWidth = gridWidth * 0.5f + panelPadding.x;
        if (CanScroll)
            halfWidth = Mathf.Max(halfWidth,
                gridWidth * 0.5f + scrollbarGap + scrollbarWidth + scrollbarMargin);

        float side = Mathf.Max(halfWidth * 2f, contentHeight + panelPadding.y * 2f);

        DesktopWindow window = DesktopWindow.Instance;
        if (window != null)
            window.SetPanel(Mathf.RoundToInt(side * window.PixelsPerUnit));

        ShowPanel(side);

        float contentBottom = -contentHeight * 0.5f;
        topFeetY = contentBottom + menuBand + (visibleRows - 1) * cellSize.y;
        float bottomFeetY = topFeetY - (visibleRows - 1) * cellSize.y;
        trackTop = topFeetY + markerHeight;
        trackBottom = bottomFeetY - labelBand;
        LayoutScrollbar(gridWidth * 0.5f + scrollbarGap + scrollbarWidth * 0.5f);

        // Se muestran en orden alfabetico, pero la seleccion se sigue guardando
        // por indice de `characters`: reordenar el array romperia la eleccion
        // guardada de quien ya tenia personajes elegidos.
        displayOrder.Clear();
        displayOrder.AddRange(Enumerable.Range(0, count)
            .OrderBy(i => DisplayName(characters[i].name), StringComparer.CurrentCultureIgnoreCase));

        // Instanciar personajes y crear su texto abajo
        foreach (int c in displayOrder)
        {
            var instance = Instantiate(characters[c], Vector3.zero, Quaternion.identity, transform);
            instance.name = characters[c].name;
            Freeze(instance);

            // Creamos el texto hijo para el nombre del personaje
            CreateNameLabel(instance, characters[c].name);

            row.Add(instance);
            rowSprites.Add(instance.GetComponent<SpriteRenderer>());
            rowLabels.Add(instance.GetComponentInChildren<TextMeshPro>());
            cellFeet.Add(Vector2.zero);
            visibility.Add(1f);
            markers.Add(null);
        }

        IsPicking = true;
        UpdateGridPositions();
    }

    /// <summary>
    /// Ubica y dimensiona el riel una sola vez por apertura. La manija se
    /// mueve despues, en <see cref="UpdateScrollbar"/>.
    /// </summary>
    private void LayoutScrollbar(float x)
    {
        bool show = CanScroll;
        if (scrollTrack != null) scrollTrack.SetActive(show);
        if (scrollThumb != null) scrollThumb.SetActive(show);
        if (!show) return;

        float trackLength = trackTop - trackBottom;
        thumbHeight = Mathf.Clamp(trackLength * visibleRows / totalRows, minThumbHeight, trackLength);

        if (scrollTrack != null)
        {
            scrollTrack.transform.position = new Vector3(x, (trackTop + trackBottom) * 0.5f, 0f);
            SizeBar(scrollTrack, trackLength, trackColor);
        }

        if (scrollThumb != null)
        {
            scrollThumb.transform.position = new Vector3(x, trackTop - thumbHeight * 0.5f, 0f);
            SizeBar(scrollThumb, thumbHeight, thumbColor);
        }
    }

    private void SizeBar(GameObject bar, float height, Color color)
    {
        // Se estira con la escala y no con drawMode Sliced: el sprite cuadrado
        // no esta importado como Full Rect y Sliced se queja.
        var sprite = bar.GetComponent<SpriteRenderer>();
        if (sprite == null || sprite.sprite == null) return;

        sprite.drawMode = SpriteDrawMode.Simple;
        sprite.color = color;

        Vector2 native = sprite.sprite.bounds.size;
        var scale = new Vector3(scrollbarWidth / native.x, height / native.y, 1f);
        bar.transform.localScale = scale;

        // El collider vive en espacio local, asi que se divide por la escala.
        var box = bar.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.offset = Vector2.zero;
            box.size = new Vector2(Mathf.Max(scrollbarWidth, scrollbarGrabWidth) / scale.x, native.y);
        }
    }

    private void UpdateScrollbar()
    {
        if (!CanScroll || scrollThumb == null) return;

        float t = scroll / MaxScroll;
        float highest = trackTop - thumbHeight * 0.5f;
        float lowest = trackBottom + thumbHeight * 0.5f;

        Vector3 p = scrollThumb.transform.position;
        scrollThumb.transform.position = new Vector3(p.x, Mathf.Lerp(highest, lowest, t), p.z);
    }

    private void UpdateThumbColor(DesktopWindow window)
    {
        if (!CanScroll || scrollThumb == null) return;

        var sprite = scrollThumb.GetComponent<SpriteRenderer>();
        if (sprite == null) return;

        var box = scrollThumb.GetComponent<Collider2D>();
        bool hovering = box != null && box.OverlapPoint(window.CursorWorldPosition);
        sprite.color = draggingThumb || hovering ? thumbActiveColor : thumbColor;
    }

    /// <summary>
    /// El nombre que ve el jugador. Limpia nombres técnicos como
    /// "Joch_Spritesheet_0" -> "Joch".
    /// </summary>
    private static string DisplayName(string rawName)
    {
        string cleanName = rawName.Replace("(Clone)", "").Trim();
        if (cleanName.Contains("_"))
        {
            cleanName = cleanName.Split('_')[0];
        }
        return cleanName;
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
        string cleanName = DisplayName(rawName);

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

        for (int i = 0; i < count; i++)
        {
            int rowIndex = i / currentColumns;
            int column = i % currentColumns;

            // Cuanto se paso la fila del area visible, en filas. Adentro es 0;
            // a medida que sale se desvanece, y del todo afuera se apaga para
            // que su collider no se coma clicks.
            float relativeRow = rowIndex - scroll;
            float outside = Mathf.Max(0f, Mathf.Max(-relativeRow, relativeRow - (visibleRows - 1)));
            float alpha = edgeFadeRows > 0f
                ? Mathf.Clamp01(1f - outside / edgeFadeRows)
                : (outside > 0f ? 0f : 1f);
            visibility[i] = alpha;

            bool shown = alpha > 0f;
            if (row[i].activeSelf != shown) row[i].SetActive(shown);
            if (!shown) continue;

            int inThisRow = Mathf.Min(currentColumns, count - rowIndex * currentColumns);

            var feet = new Vector2(
                (column - (inThisRow - 1) * 0.5f) * cellSize.x,
                topFeetY - relativeRow * cellSize.y);

            row[i].transform.position = new Vector3(feet.x, feet.y, 0f);
            cellFeet[i] = feet;
        }

        appliedScroll = scroll;
        UpdateScrollbar();
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

    /// <summary>
    /// Tiñe cada personaje segun si esta elegido y cuanto se ve, y acomoda su
    /// marcador. Corre en cada frame de scroll, asi que los marcadores no se
    /// recrean: cada personaje tiene el suyo y se prende o se apaga.
    /// </summary>
    private void RefreshIndicators()
    {
        Color markerColor = Color.white;
        var markerSprite = selectionMarker != null ? selectionMarker.GetComponent<SpriteRenderer>() : null;
        if (markerSprite != null) markerColor = markerSprite.color;

        for (int i = 0; i < row.Count; i++)
        {
            if (row[i] == null) continue;

            bool on = selected.Contains(displayOrder[i]);
            Color tint = on ? Color.white : unselectedTint;
            tint.a *= visibility[i];

            if (rowSprites[i] != null) rowSprites[i].color = tint;

            // También podemos atenuar el texto si el personaje no está seleccionado
            if (rowLabels[i] != null) rowLabels[i].color = tint;

            if (!on || selectionMarker == null || !row[i].activeSelf)
            {
                if (markers[i] != null) markers[i].SetActive(false);
                continue;
            }

            if (markers[i] == null)
                markers[i] = Instantiate(selectionMarker, transform);

            var marker = markers[i];
            marker.SetActive(true);
            marker.transform.position = new Vector3(cellFeet[i].x, cellFeet[i].y + markerHeight, 0f);

            var sprite = marker.GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                Color c = markerColor;
                c.a *= visibility[i];
                sprite.color = c;
            }
        }
    }

    private void SpawnPets()
    {
        ClearPets();
        // En la franja salen en el mismo orden que en la lista.
        var chosen = displayOrder.Where(i => selected.Contains(i)).ToList();
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

        var drag = instance.GetComponent<CharacterDragHandler>();
        if (drag != null) drag.enabled = false;
    }
    private void ClearRow()
    {
        EndThumbDrag();

        foreach (var go in row) if (go != null) Destroy(go);
        row.Clear();
        rowSprites.Clear();
        rowLabels.Clear();
        cellFeet.Clear();
        visibility.Clear();
        foreach (var m in markers) if (m != null) Destroy(m);
        markers.Clear();
        if (panel != null) Destroy(panel);
        panel = null;

        if (scrollTrack != null) scrollTrack.SetActive(false);
        if (scrollThumb != null) scrollThumb.SetActive(false);
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

    // El piso es el borde de abajo de la franja, no de la camara: la ventana
    // puede ser mas alta que la franja cuando el menu de la esquina esta abierto.
    private float FloorY()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window != null) return window.StageWorldRect.yMin;
        return cam != null ? -cam.orthographicSize : -1f;
    }
}