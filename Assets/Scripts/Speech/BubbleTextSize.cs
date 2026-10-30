using System;
using UnityEngine;

/// <summary>
/// Tamaño del texto de los globos de dialogo, aparte del tamaño de los
/// personajes: agrandar los personajes no agranda el texto, y al reves. Se
/// elige desde el menu de la franja y se guarda en PlayerPrefs.
///
/// Es un factor sobre el tamaño de fuente del prefab del globo. Va de a
/// cuartos a proposito: el contorno del globo es pixel art y, con saltos de
/// 0.25, no se deforma.
/// </summary>
public static class BubbleTextSize
{
    /// <summary>Factores que se pueden elegir, del mas chico al mas grande.</summary>
    public static readonly float[] Steps = { 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f };

    private const int DefaultStep = 1;
    private const string Key = "CosmereIdle_TextScale";

    /// <summary>Avisa cuando cambia, para que los globos en pantalla se vuelvan a medir.</summary>
    public static event Action Changed;

    private static bool loaded;
    private static int step;

    public static float Scale { get { Load(); return Steps[step]; } }

    public static bool CanStep(int direction)
    {
        Load();
        int next = step + direction;
        return next >= 0 && next < Steps.Length;
    }

    /// <summary>Agranda (+1) o achica (-1) el texto un salto.</summary>
    public static void Step(int direction)
    {
        if (!CanStep(direction)) return;
        step += direction;
        PlayerPrefs.SetFloat(Key, Steps[step]);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>"100%", "125%".</summary>
    public static string Format() => $"{Mathf.RoundToInt(Scale * 100f)}%";

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        // El salto mas cercano a lo guardado, por si se cambia la lista.
        float saved = PlayerPrefs.GetFloat(Key, Steps[DefaultStep]);
        step = 0;
        for (int i = 1; i < Steps.Length; i++)
            if (Mathf.Abs(Steps[i] - saved) < Mathf.Abs(Steps[step] - saved)) step = i;
    }

    // Con "Enter Play Mode" sin recargar el dominio, lo estatico sobrevive entre
    // corridas del Editor: se vuelve a leer de PlayerPrefs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }
}
