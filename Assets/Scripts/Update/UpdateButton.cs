using UnityEngine;

/// <summary>
/// Aviso de version nueva. Solo aparece si GitHubUpdater encontro una: es un
/// icono en la esquina que late para llamar la atencion, con el detalle en el
/// tooltip. Click = actualizar ahora.
///
/// Ademas, la primera vez que aparece una version, una de las mascotas lo dice
/// en un globo: un icono que aparece solo en la esquina es facil de no ver.
/// </summary>
public class UpdateButton : DebugIconButton
{
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.5f, 1f);

    [Tooltip("Latidos por segundo mientras espera que lo aprieten.")]
    [SerializeField] private float pulseSpeed = 1.2f;

    [Tooltip("Cada cuanto se busca una mascota para que avise, si todavia no hay ninguna.")]
    [SerializeField] private float announceRetrySeconds = 2f;

    /// <summary>Que version ya se anuncio, para no repetir el globo.</summary>
    private string announced;
    private bool announcedStartupInstall;
    private float nextAnnounceTry;

    private static GitHubUpdater Updater => GitHubUpdater.Instance;

    protected override bool IsAvailable =>
        Updater != null && Updater.State != GitHubUpdater.UpdateState.UpToDate;

    protected override string TooltipText
    {
        get
        {
            GitHubUpdater u = Updater;
            if (u == null) return "";

            switch (u.State)
            {
                case GitHubUpdater.UpdateState.Downloading:
                    return $"Descargando {u.LatestVersion}... {Mathf.RoundToInt(u.DownloadProgress * 100f)}%";

                case GitHubUpdater.UpdateState.Installing:
                    return "Instalando, ya vuelvo";

                default:
                    if (u.LastError != null) return "No se pudo actualizar. Click para reintentar";

                    return u.CanSelfInstall
                        ? $"Versión {u.LatestVersion} disponible. Click para actualizar"
                        : $"Versión {u.LatestVersion} disponible. Click para descargarla";
            }
        }
    }

    protected override Color GetColor(bool hovering)
    {
        GitHubUpdater u = Updater;
        if (u == null) return idleColor;

        // Bajando o instalando: fijo, ya no hace falta llamar la atencion.
        if (u.State != GitHubUpdater.UpdateState.Available) return highlightColor;
        if (hovering) return hoverColor;

        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * 2f * Mathf.PI);
        return Color.Lerp(idleColor, highlightColor, t);
    }

    protected override void OnClicked()
    {
        Updater?.InstallNow();
    }

    protected override void Update()
    {
        base.Update();
        Announce();
    }

    private void Announce()
    {
        GitHubUpdater u = Updater;
        if (u == null || u.LatestVersion == null) return;

        string phrase = null;

        if (u.IsStartupInstall && !announcedStartupInstall)
            phrase = $"¡Me actualizo a la {u.LatestVersion}! Ya vuelvo.";
        else if (!u.IsStartupInstall && u.State == GitHubUpdater.UpdateState.Available && announced != u.LatestVersion)
            phrase = $"¡Salió la versión {u.LatestVersion}! Tocá el icono amarillo de arriba.";

        if (phrase == null || Time.unscaledTime < nextAnnounceTry) return;
        nextAnnounceTry = Time.unscaledTime + announceRetrySeconds;

        // Si todavia no hay mascotas (pantalla de seleccion abierta), se reintenta.
        PetSpeech pet = FindAnyObjectByType<PetSpeech>();
        if (pet == null || !pet.isActiveAndEnabled) return;

        pet.Say(phrase);

        if (u.IsStartupInstall) announcedStartupInstall = true;
        announced = u.LatestVersion;
    }
}
