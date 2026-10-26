using System;

/// <summary>
/// La lista de archivos de una version, tal como la publica el menu
/// "Publicar version" (files.json, adjunto al release).
///
/// Sirve para la actualizacion parcial: el juego compara sus propios archivos
/// con esta lista y, si los unicos distintos son los que trae el parche, baja
/// el parche (un par de MB) en vez del instalador completo.
///
/// La escribe el editor y la lee el juego con JsonUtility: por eso campos
/// publicos y nombres en minuscula, que son los del JSON.
/// </summary>
[Serializable]
public class UpdateManifest
{
    /// <summary>La version que describe, sin "v" ("1.9").</summary>
    public string version;

    /// <summary>Nombre del instalador completo adjunto al mismo release.</summary>
    public string full;

    /// <summary>Nombre del parche adjunto al mismo release. Vacio si esta version no tiene.</summary>
    public string patch;

    public UpdateManifestFile[] files;
}

[Serializable]
public class UpdateManifestFile
{
    /// <summary>Ruta relativa a la carpeta del juego, con "/" ("CosmereArgIdle_Data/level0").</summary>
    public string path;

    /// <summary>SHA-256 del archivo, en hexa minuscula.</summary>
    public string sha256;

    public long size;

    /// <summary>
    /// Si viene dentro del parche. Los que no vienen tienen que estar ya
    /// instalados exactamente asi para que el parche sirva.
    /// </summary>
    public bool inPatch;
}
