using System;
using UnityEngine;

/// <summary>
/// Estado global del evento y decoración de Halloween (Fantasmas y Bruja).
/// Permite activar o desactivar la aparición de elementos de Halloween
/// desde el menú de la esquina y persiste la preferencia en PlayerPrefs.
/// </summary>
public static class HalloweenEvent
{
    private const string EnabledKey = "CosmereIdle_HalloweenEnabled";

    /// <summary>Se dispara cada vez que el usuario activa o desactiva la opción en el menú.</summary>
    public static event Action Changed;

    private static bool loaded;
    private static bool enabled = true;

    public static bool Enabled
    {
        get
        {
            Load();
            return enabled;
        }
        set
        {
            Load();
            if (enabled == value) return;
            enabled = value;
            Save();
            Changed?.Invoke();
        }
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        // Por defecto activado (1) durante la temporada
        enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0;
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
