using UnityEngine;

/// <summary>
/// Icono de actualizaciones en la esquina. Aparece en dos casos:
///
///   - Mientras se consulta a GitHub: tenue y latiendo despacio, para que se
///     vea que esta buscando.
///   - Si hay una version nueva: amarillo y latiendo, con el detalle en el
///     tooltip. Click = actualizar ahora.
///
/// Con las mascotas en el escritorio, la primera vez que aparece una version
/// una de ellas lo dice en un globo: un icono que aparece solo en la esquina es
/// facil de no ver. En la pantalla de seleccion las mascotas estan congeladas y
/// no hablan; ahi el aviso lo da UpdateStatusLabel.
/// </summary>
public class UpdateButton : DebugIconButton
{
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField] private Color checkingColor = new Color(1f, 1f, 1f, 0.6f);

    [Tooltip("Latidos por segundo mientras espera que lo aprieten.")]
    [SerializeField] private float pulseSpeed = 1.2f;

    [Tooltip("Latidos por segundo mientras busca versiones. Mas lento: no pide nada.")]
    [SerializeField] private float checkingPulseSpeed = 0.6f;

    [Tooltip("Cada cuanto se busca una mascota para que avise, si todavia no hay ninguna.")]
    [SerializeField] private float announceRetrySeconds = 2f;

    /// <summary>Que version ya se anuncio, para no repetir el globo.</summary>
    private string announced;
    private bool announcedStartupInstall;
    private float nextAnnounceTry;

    private static GitHubUpdater Updater => GitHubUpdater.Instance;

    /// <summary>Solo busca, no hay nada nuevo (todavia).</summary>
    private static bool OnlyChecking =>
        Updater != null && Updater.IsChecking && Updater.State == GitHubUpdater.UpdateState.UpToDate;

    protected override bool IsAvailable =>
        Updater != null && (Updater.State != GitHubUpdater.UpdateState.UpToDate || Updater.IsChecking);

    protected override string TooltipText
    {
        get
        {
            GitHubUpdater u = Updater;
            if (u == null) return "";
            if (OnlyChecking) return "Buscando actualizaciones...";

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

        if (OnlyChecking) return Color.Lerp(idleColor, checkingColor, Pulse(checkingPulseSpeed));

        // Bajando o instalando: fijo, ya no hace falta llamar la atencion.
        if (u.State != GitHubUpdater.UpdateState.Available) return highlightColor;
        if (hovering) return hoverColor;

        return Color.Lerp(idleColor, highlightColor, Pulse(pulseSpeed));
    }

    private static float Pulse(float speed) =>
        0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed * 2f * Mathf.PI);

    protected override void OnClicked()
    {
        // Mientras solo busca no hay nada que hacer todavia.
        if (OnlyChecking) return;
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
        bool startup = u.IsStartupInstall;

        if (startup && !announcedStartupInstall)
            phrase = $"¡Me actualizo a la {u.LatestVersion}! Ya vuelvo.";
        else if (!startup && u.State == GitHubUpdater.UpdateState.Available && announced != u.LatestVersion)
            phrase = $"¡Salió la versión {u.LatestVersion}! Tocá el icono amarillo de arriba.";

        if (phrase == null) return;

        // En la pantalla de seleccion lo muestra el cartel del panel. Se da por
        // anunciado: si no, al confirmar la seleccion una mascota repetiria algo
        // que el usuario ya vio.
        CharacterPicker picker = CharacterPicker.Instance;
        if (picker != null && picker.IsPicking)
        {
            MarkAnnounced(u, startup);
            return;
        }

        if (Time.unscaledTime < nextAnnounceTry) return;
        nextAnnounceTry = Time.unscaledTime + announceRetrySeconds;

        PetSpeech pet = FindAnyObjectByType<PetSpeech>();
        if (pet == null || !pet.isActiveAndEnabled) return;

        pet.Say(phrase);
        MarkAnnounced(u, startup);
    }

    private void MarkAnnounced(GitHubUpdater u, bool startup)
    {
        if (startup) announcedStartupInstall = true;
        announced = u.LatestVersion;
    }
}
