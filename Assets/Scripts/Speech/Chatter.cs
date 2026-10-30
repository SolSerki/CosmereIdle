using System;
using UnityEngine;

/// <summary>
/// Charla automatica: los personajes dicen una frase solos cada tanto, ademas
/// de cuando les hacen click. Se prende y se apaga desde el menu de la franja,
/// y ahi mismo se eligen el minimo y el maximo de la espera.
///
/// Cada personaje espera un tiempo al azar entre el minimo y el maximo, asi con
/// muchos en pantalla no hablan todos juntos. Ademas, entre una frase automatica
/// y la siguiente (de cualquier personaje) pasa al menos <see cref="MinGap"/>:
/// con veinte personajes el azar solo no alcanza para que no se pisen.
///
/// Se guarda en PlayerPrefs como el resto de las preferencias.
/// </summary>
public static class Chatter
{
    /// <summary>
    /// Valores que se pueden elegir, en segundos, de 5 s a 30 min. Van de a
    /// saltos que crecen: entre 5 y 10 s la diferencia se nota, entre 20 y
    /// 20:05 min no, y de a 5 s llegar a media hora llevaria cientos de clicks.
    /// </summary>
    public static readonly float[] Steps =
    {
        5f, 10f, 15f, 20f, 30f, 45f,
        60f, 90f, 120f, 180f, 240f, 300f, 420f,
        600f, 900f, 1200f, 1500f, 1800f,
    };

    private const float DefaultMin = 30f;
    private const float DefaultMax = 60f;

    /// <summary>Segundos minimos entre dos frases automaticas de personajes distintos.</summary>
    public const float MinGap = 2.5f;

    private const string EnabledKey = "CosmereIdle_ChatterEnabled";
    private const string MinKey = "CosmereIdle_ChatterMin";
    private const string MaxKey = "CosmereIdle_ChatterMax";

    /// <summary>Avisa cuando cambia algo, para que cada personaje vuelva a sortear su espera.</summary>
    public static event Action Changed;

    private static bool loaded;
    private static bool enabled;
    private static int minStep;
    private static int maxStep;
    private static float lastAutoPhraseAt = float.NegativeInfinity;

    public static bool Enabled
    {
        get { Load(); return enabled; }
        set
        {
            Load();
            if (enabled == value) return;
            enabled = value;
            Save();
        }
    }

    public static float MinSeconds { get { Load(); return Steps[minStep]; } }
    public static float MaxSeconds { get { Load(); return Steps[maxStep]; } }

    public static bool CanStepMin(int direction) => InRange(minStep + direction);
    public static bool CanStepMax(int direction) => InRange(maxStep + direction);

    /// <summary>
    /// Sube (+1) o baja (-1) el minimo un salto. Si pasa al maximo, lo empuja:
    /// asi nunca queda un minimo mas grande que el maximo.
    /// </summary>
    public static void StepMin(int direction)
    {
        Load();
        if (!InRange(minStep + direction)) return;
        minStep += direction;
        maxStep = Mathf.Max(maxStep, minStep);
        Save();
    }

    /// <summary>Sube o baja el maximo un salto. Si pasa al minimo, lo empuja.</summary>
    public static void StepMax(int direction)
    {
        Load();
        if (!InRange(maxStep + direction)) return;
        maxStep += direction;
        minStep = Mathf.Min(minStep, maxStep);
        Save();
    }

    /// <summary>Cuanto espera un personaje hasta su proxima frase automatica.</summary>
    public static float NextDelay() => UnityEngine.Random.Range(MinSeconds, MaxSeconds);

    /// <summary>
    /// Pide el turno para decir una frase automatica. Si otro personaje acaba
    /// de hablar solo, devuelve false y hay que esperar un poco.
    /// </summary>
    public static bool TryTakeTurn()
    {
        if (Time.time - lastAutoPhraseAt < MinGap) return false;
        lastAutoPhraseAt = Time.time;
        return true;
    }

    /// <summary>"45 s", "1 min", "1 min 30 s", "30 min".</summary>
    public static string Format(float seconds)
    {
        int total = Mathf.RoundToInt(seconds);
        if (total < 60) return $"{total} s";

        int minutes = total / 60;
        int rest = total % 60;
        return rest == 0 ? $"{minutes} min" : $"{minutes} min {rest} s";
    }

    private static bool InRange(int step) => step >= 0 && step < Steps.Length;

    /// <summary>El salto mas cercano a un valor guardado, por si se cambia la lista.</summary>
    private static int NearestStep(float seconds)
    {
        int best = 0;
        for (int i = 1; i < Steps.Length; i++)
            if (Mathf.Abs(Steps[i] - seconds) < Mathf.Abs(Steps[best] - seconds)) best = i;
        return best;
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        enabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        minStep = NearestStep(PlayerPrefs.GetFloat(MinKey, DefaultMin));
        maxStep = Mathf.Max(minStep, NearestStep(PlayerPrefs.GetFloat(MaxKey, DefaultMax)));
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
        PlayerPrefs.SetFloat(MinKey, Steps[minStep]);
        PlayerPrefs.SetFloat(MaxKey, Steps[maxStep]);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Con "Enter Play Mode" sin recargar el dominio, lo estatico sobrevive entre
    // corridas del Editor: se vuelve a leer de PlayerPrefs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        lastAutoPhraseAt = float.NegativeInfinity;
        Changed = null;
    }
}
