using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hace hablar al personaje cuando le hacen click.
///
/// Las frases viven en un .json por personaje, no en el codigo, para que se
/// puedan traducir y para que la comunidad pueda escribirlas sin tocar Unity.
/// Si al personaje no se le asigno ninguno, usa el de fallback.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PetSpeech : MonoBehaviour
{
    /// <summary>Forma del archivo de frases. Los campos vacios no molestan.</summary>
    [Serializable]
    private class PhraseFile
    {
        public string[] click;
        public string[] idle;
        public string[] read;
        public string[] sleep;
    }

    [Tooltip("JSON con las frases de este personaje.")]
    [SerializeField] private TextAsset phrases;

    [Tooltip("Se usa si el personaje no tiene su propio archivo.")]
    [SerializeField] private TextAsset fallbackPhrases;

    [SerializeField] private GameObject bubblePrefab;

    [Tooltip("Aire entre la cabeza y la punta de la colita del globo, en unidades de " +
             "mundo. El globo crece hacia arriba desde ahi, asi que nunca tapa la cara " +
             "por largo que sea el texto.")]
    [SerializeField] private float bubbleGap = 0.08f;

    [Tooltip("Cuanto dura el globo en pantalla con una frase corta.")]
    [SerializeField] private float bubbleSeconds = 2.8f;

    /// <summary>Desde cuantos caracteres una frase se queda mas tiempo.</summary>
    [Serializable]
    private struct ReadingTime
    {
        [Tooltip("Desde cuantos caracteres aplica (contando espacios).")]
        public int fromCharacters;

        [Tooltip("Cuantos segundos queda el globo en pantalla.")]
        public float seconds;
    }

    [Tooltip("Frases largas: cuanto mas texto, mas tiempo queda el globo. Gana el umbral " +
             "mas alto que la frase alcance; el orden de la lista no importa. Con menos " +
             "caracteres que el primero, dura bubbleSeconds.")]
    [SerializeField] private ReadingTime[] longerPhrases =
    {
        new ReadingTime { fromCharacters = 40, seconds = 3.2f },
        new ReadingTime { fromCharacters = 80, seconds = 6f },
        new ReadingTime { fromCharacters = 150, seconds = 9f },
        new ReadingTime { fromCharacters = 220, seconds = 12f },
    };

    // Fila (en pixeles de la textura, desde abajo) hasta donde llega el dibujo
    // mas alto de cada hoja de sprites. Se mide una vez por hoja.
    private static readonly Dictionary<Texture2D, int> sheetTops = new Dictionary<Texture2D, int>();

    private Collider2D body;
    private SpriteRenderer spriteRenderer;
    private string[] clickPhrases;
    private SpeechBubble bubble;
    private int lastIndex = -1;

    private CharacterDragHandler dragHandler;
    private bool isWaitingClickRelease;

    private void Awake()
    {
        body = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        dragHandler = GetComponent<CharacterDragHandler>();
        clickPhrases = LoadClickPhrases();
    }

    private void Update()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null) return;

        // Clic inicial sobre el personaje
        if (window.LeftPressedThisFrame && body.OverlapPoint(window.CursorWorldPosition))
        {
            isWaitingClickRelease = true;
        }

        // Al soltar el clic
        if (isWaitingClickRelease && !window.IsLeftDown)
        {
            isWaitingClickRelease = false;

            if (dragHandler == null || !dragHandler.IsDragging)
            {
                Say(NextPhrase());
            }
        }
    }
    public void Say(string phrase)
    {
        if (string.IsNullOrEmpty(phrase) || bubblePrefab == null) return;

        // Un globo por personaje: si ya estaba hablando, reemplaza lo que decia.
        if (bubble != null) Destroy(bubble.gameObject);

        var instance = Instantiate(bubblePrefab, transform.position, Quaternion.identity);
        bubble = instance.GetComponent<SpeechBubble>();
        bubble.Show(phrase, transform, HeadTop() + bubbleGap, SecondsFor(phrase));
    }

    /// <summary>
    /// Altura de la cabeza sobre los pies, en el mundo. Sale de lo que esta
    /// dibujado y no del cuadro de 24x24 ni del collider: casi todos los
    /// personajes dejan filas vacias arriba, y el globo quedaria flotando lejos.
    ///
    /// Se toma el dibujo mas alto de toda la hoja y no el del cuadro actual,
    /// para que el globo salga siempre a la misma altura y no salte con la
    /// animacion. La malla Tight del sprite no sirve: en sprites de menos de
    /// 32 px Unity la reemplaza por el rectangulo entero.
    ///
    /// Necesita la textura con Read/Write; si no lo tiene, usa el collider.
    /// </summary>
    private float HeadTop()
    {
        Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (sprite != null && sprite.texture != null && sprite.texture.isReadable)
        {
            Rect rect = sprite.textureRect;
            int top = SheetTop(sprite.texture, rect);
            if (top > 0)
                return (top - rect.y - sprite.pivot.y) / sprite.pixelsPerUnit * transform.lossyScale.y;
        }

        return body.bounds.max.y - transform.position.y;
    }

    /// <summary>
    /// Fila mas alta con algo dibujado en la franja de la hoja donde esta el
    /// sprite (todos los cuadros de la tira miden lo mismo de alto). 0 si esta vacia.
    /// </summary>
    private static int SheetTop(Texture2D texture, Rect rect)
    {
        if (sheetTops.TryGetValue(texture, out int cached)) return cached;

        int yMin = Mathf.FloorToInt(rect.yMin);
        int height = Mathf.CeilToInt(rect.height);
        Color32[] pixels = texture.GetPixels32();
        int top = 0;

        for (int y = yMin + height - 1; y >= yMin && top == 0; y--)
        {
            int row = y * texture.width;
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[row + x].a > 8)
                {
                    top = y + 1;
                    break;
                }
            }
        }

        sheetTops[texture] = top;
        return top;
    }

    /// <summary>
    /// Cuanto tiempo tiene que quedar una frase para que se llegue a leer. Por
    /// umbrales y no por una cuenta, para que se pueda ajustar a ojo desde el
    /// Inspector sin pensar en caracteres por segundo.
    /// </summary>
    private float SecondsFor(string phrase)
    {
        int length = phrase.Trim().Length;
        float seconds = bubbleSeconds;

        if (longerPhrases == null) return seconds;

        foreach (var threshold in longerPhrases)
            if (length >= threshold.fromCharacters && threshold.seconds > seconds)
                seconds = threshold.seconds;

        return seconds;
    }

    /// <summary>
    /// Elige una frase evitando repetir la anterior. Con pocas frases y azar puro
    /// se repite la mitad de las veces y se nota muchisimo.
    /// </summary>
    private string NextPhrase()
    {
        if (clickPhrases == null || clickPhrases.Length == 0) return null;
        if (clickPhrases.Length == 1) return clickPhrases[0];

        int index;
        do { index = UnityEngine.Random.Range(0, clickPhrases.Length); }
        while (index == lastIndex);

        lastIndex = index;
        return clickPhrases[index];
    }

    private string[] LoadClickPhrases()
    {
        TextAsset source = phrases != null ? phrases : fallbackPhrases;

        if (source == null)
        {
            Debug.LogWarning($"[PetSpeech] {name} no tiene archivo de frases.");
            return Array.Empty<string>();
        }

        try
        {
            var parsed = JsonUtility.FromJson<PhraseFile>(source.text);
            if (parsed?.click != null && parsed.click.Length > 0) return parsed.click;

            Debug.LogWarning($"[PetSpeech] {name}: {source.name} no tiene frases en 'click'.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PetSpeech] {name}: no pude leer {source.name} — {e.Message}");
        }

        return Array.Empty<string>();
    }
}
