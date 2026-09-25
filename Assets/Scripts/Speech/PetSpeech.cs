using System;
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

    [Tooltip("Altura del globo sobre los pies, en unidades de mundo.")]
    [SerializeField] private float bubbleHeight = 1.15f;

    [Tooltip("Cuanto dura el globo en pantalla.")]
    [SerializeField] private float bubbleSeconds = 2.8f;

    private Collider2D body;
    private string[] clickPhrases;
    private SpeechBubble bubble;
    private int lastIndex = -1;

    private void Awake()
    {
        body = GetComponent<Collider2D>();
        clickPhrases = LoadClickPhrases();
    }

    private void Update()
    {
        DesktopWindow window = DesktopWindow.Instance;
        if (window == null || !window.LeftPressedThisFrame) return;

        // El picker congela este componente mientras la fila esta abierta, asi
        // que si llegamos aca el click es para hablar y no para elegir.
        if (!body.OverlapPoint(window.CursorWorldPosition)) return;

        Say(NextPhrase());
    }

    public void Say(string phrase)
    {
        if (string.IsNullOrEmpty(phrase) || bubblePrefab == null) return;

        // Un globo por personaje: si ya estaba hablando, reemplaza lo que decia.
        if (bubble != null) Destroy(bubble.gameObject);

        var instance = Instantiate(bubblePrefab, transform.position, Quaternion.identity);
        bubble = instance.GetComponent<SpeechBubble>();
        bubble.Show(phrase, transform, bubbleHeight, bubbleSeconds);
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
