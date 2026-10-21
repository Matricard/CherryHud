using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CherryHud.Services;

public enum AppLanguage
{
    English = 0,
    French = 1
}

public class LocalizationService : ObservableObject
{
    public static LocalizationService Instance { get; } = new();

    private AppLanguage _currentLanguage = AppLanguage.English;
    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (SetProperty(ref _currentLanguage, value))
            {
                OnPropertyChanged(string.Empty);
                LanguageChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<AppLanguage>? LanguageChanged;

    public bool IsEnglish => CurrentLanguage == AppLanguage.English;
    public bool IsFrench => CurrentLanguage == AppLanguage.French;

    // Window & Header
    public string WindowTitle => IsFrench ? "Paramètres de CherryHud" : "CherryHud Settings";
    public string HeaderTitle => IsFrench ? "Paramètres CherryHud" : "CherryHud Settings";
    public string HeaderSubtitle => IsFrench ? "Personnalisez le comportement, les métriques et l'affichage" : "Customize behavior, metrics, and display";

    // Tabs
    public string TabGeneral => IsFrench ? "Général" : "General";
    public string TabDisplay => TabGeneral;
    public string TabMetrics => IsFrench ? "Métriques" : "Metrics";
    public string TabAbout => IsFrench ? "À propos" : "About";

    // Language Card
    public string LanguageCardTitle => IsFrench ? "Langue de l'application" : "Application Language";
    public string LanguageCardSubtitle => IsFrench ? "Choisissez la langue de l'interface" : "Choose the interface language";

    // Auto Mode Card
    public string AutoModeTitle => IsFrench ? "Mode Auto" : "Auto Mode";
    public string AutoModeSubtitle => IsFrench ? "Affiche le HUD uniquement lorsqu'un jeu 3D tourne" : "Only show the HUD when a 3D game is running";

    // Launch At Login Card
    public string LaunchAtLoginTitle => IsFrench ? "Lancer au démarrage" : "Start at Login";
    public string LaunchAtLoginSubtitle => IsFrench ? "Ouvrir CherryHud automatiquement à l'ouverture du Mac" : "Automatically open CherryHud when your Mac starts up";

    // Position Card
    public string PositionTitle => IsFrench ? "Emplacement de l'overlay" : "Overlay Position";
    public string PositionSubtitle => IsFrench ? "Coin de l'écran où ancrer le HUD" : "Screen corner where the HUD is anchored";
    public string PositionTopLeft => IsFrench ? "Haut Gauche" : "Top Left";
    public string PositionTopRight => IsFrench ? "Haut Droite" : "Top Right";
    public string PositionBottomLeft => IsFrench ? "Bas Gauche" : "Bottom Left";
    public string PositionBottomRight => IsFrench ? "Bas Droite" : "Bottom Right";

    // Theme Card
    public string ThemeTitle => IsFrench ? "Thème de l'overlay" : "Overlay Theme";
    public string ThemeSubtitle => IsFrench ? "Choisissez la densité d'affichage du HUD" : "Choose HUD display density";
    public string ThemeNormal => IsFrench ? "Normal" : "Normal";
    public string ThemeCompact => IsFrench ? "Compact" : "Compact";

    // Drag & Drop Card
    public string AllowDragTitle => IsFrench ? "Glisser-déposer de l'overlay" : "Overlay Drag & Drop";
    public string AllowDragSubtitle => IsFrench ? "Autoriser le déplacement libre de l'overlay à la souris" : "Allow moving the overlay freely with the mouse";

    // Metrics
    public string MetricSocTemp => IsFrench ? "Température moyenne du processeur (CPU + GPU)" : "Processor average temperature (CPU + GPU)";
    public string MetricGpu => IsFrench ? "Charge GPU & Température" : "GPU Load & Temperature";
    public string MetricCpu => IsFrench ? "Charge CPU & Température" : "CPU Load & Temperature";
    public string MetricGpuUsage => IsFrench ? "Charge GPU (%)" : "GPU Load (%)";
    public string MetricGpuTemp => IsFrench ? "Température GPU (°C)" : "GPU Temperature (°C)";
    public string MetricCpuUsage => IsFrench ? "Charge CPU (%)" : "CPU Load (%)";
    public string MetricCpuTemp => IsFrench ? "Température CPU (°C)" : "CPU Temperature (°C)";
    public string MetricVram => IsFrench ? "Mémoire Graphique (VRAM)" : "Video Memory (VRAM)";
    public string MetricRam => IsFrench ? "Mémoire Vive Système" : "System RAM Memory";
    public string MetricBattery => IsFrench ? "Batterie & État de charge" : "Battery & Charging State";
    public string HideBatteryWhenChargedTitle => IsFrench ? "Masquer la batterie si chargée" : "Hide battery when charged";
    public string HideBatteryWhenChargedSubtitle => IsFrench ? "Masque la batterie lorsque le Mac est branché et a atteint le pourcentage défini" : "Hides the battery when the Mac is plugged in and has reached the specified percentage";
    public string BatteryThresholdLabel => IsFrench ? "Seuil de masquage" : "Hide threshold";
    public string MetricFps => IsFrench ? "Images par seconde" : "Frames Per Second";
    public string MetricFrametime => IsFrench ? "Temps de trame (Frametime)" : "Frametime";
    public string MetricGraph => IsFrench ? "Graphique de Frametime" : "Frametime Sparkline Graph";
    public string BetaNoteText => IsFrench ? "Ces métriques sont en cours de développement." : "These metrics are currently under development.";

    // Advanced

    public string ScreenCaptureTitle => IsFrench ? "Autorisation macOS ScreenCaptureKit" : "macOS ScreenCaptureKit Permission";
    public string ScreenCaptureSubtitle => IsFrench ? "Requise par macOS pour mesurer les FPS des jeux (redémarrer CherryHud après activation)." : "Required by macOS to measure game FPS (restart CherryHud after enabling).";
    public string PermGranted => IsFrench ? "✓ Autorisation accordée" : "✓ Permission granted";
    public string PermRequired => IsFrench ? "⚠️ Enregistrement d'écran requis" : "⚠️ Screen recording required";
    public string RequestAccessButton => IsFrench ? "Demander l'accès" : "Request Access";
    public string OpenSystemSettingsButton => IsFrench ? "Ouvrir Réglages" : "Open Settings";
    public string PermissionRequiredTitle => IsFrench ? "Autorisation requise" : "Permission Required";
    public string PermissionRequiredForMetrics => IsFrench
        ? "L'enregistrement d'écran macOS (ScreenCaptureKit) est requis pour mesurer et afficher les FPS ainsi que le frametime.\n\nVeuillez autoriser l'accès dans l'onglet Général."
        : "macOS Screen Recording (ScreenCaptureKit) permission is required to measure and display FPS and frametimes.\n\nPlease enable it in the General tab.";
    public string GoToGeneralButton => IsFrench ? "Aller dans Général" : "Go to General";
    public string MetricsPermissionWarningBanner => IsFrench
        ? "L'autorisation ScreenCaptureKit est requise pour activer le FPS et le Frametime."
        : "ScreenCaptureKit permission is required to enable FPS and Frametime.";

    public string AutoCheckUpdatesTitle => IsFrench ? "Vérification automatique des mises à jour" : "Automatic Update Checks";
    public string AutoCheckUpdatesSubtitle => IsFrench ? "Vérifier la présence d'une nouvelle version au démarrage" : "Check for new versions on startup";

    public string GitHubRepoTitle => IsFrench ? "Dépôt GitHub source" : "GitHub Source Repository";
    public string GitHubRepoSubtitle => IsFrench ? "Référence GitHub Releases (propriétaire/dépôt)" : "GitHub Releases reference (owner/repo)";

    // About
    public string AboutDesc => IsFrench ? "Overlay de performance gaming natif pour macOS" : "Native macOS gaming performance overlay";
    public string GitHubCardTitle => IsFrench ? "Dépôt GitHub" : "GitHub Repository";
    public string GitHubCardSubtitle => "github.com/Matricard/CherryHud";
    public string LicenseCardTitle => IsFrench ? "Licence" : "License";
    public string LicenseCardSubtitle => IsFrench ? "Licence MIT Open-Source" : "MIT Open-Source License";

    public string SoftwareUpdatesTitle => IsFrench ? "Mises à jour du logiciel" : "Software Updates";
    public string SoftwareUpdatesSubtitle => IsFrench ? "Rechercher la dernière mise à jour disponible" : "Check for the latest available update";
    public string CheckButton => IsFrench ? "Vérifier..." : "Check...";
    public string UpdateButton => IsFrench ? "Mettre à jour" : "Update";

    // Update Window
    public string UpdateWindowTitle => IsFrench ? "Mise à jour de CherryHud" : "CherryHud Update";
    public string UpdateQueryingGitHub => IsFrench ? "Interrogation de GitHub Releases..." : "Querying GitHub Releases...";
    public string UpdateUpToDateSubtitle => IsFrench ? "Aucune nouvelle version requise." : "No new version required.";
    public string UpdateErrorSubtitle => IsFrench ? "Vérification impossible" : "Unable to check for updates";
    public string UpdateAutoRestartNote => IsFrench ? "Installation et redémarrage automatiques..." : "Automatic installation and restart...";
    public string UpdateViewOnGitHub => IsFrench ? "Voir sur GitHub" : "View on GitHub";
    public string UpdateRetry => IsFrench ? "Réessayer" : "Retry";

    public string AuthorText => IsFrench ? "Créé par Mathieu Ricard" : "Created by Mathieu Ricard";
    public string BuiltWithAvaloniaText => IsFrench ? "Construit avec Avalonia UI & .NET 10" : "Built with Avalonia UI & .NET 10";
    public string LicenseText => IsFrench ? "Copyright © 2026 • Licence MIT" : "Copyright © 2026 • MIT License";

    // Footer & Confirmation
    public string ResetDefaultsButton => IsFrench ? "Rétablir par défaut" : "Reset to Defaults";
    public string ResetConfirmTitle => IsFrench ? "Rétablir les paramètres ?" : "Reset Settings?";
    public string ResetConfirmMessage => IsFrench ? "Voulez-vous vraiment réinitialiser tous les réglages à leurs valeurs par défaut ?" : "Are you sure you want to reset all settings to their default values?";
    public string CancelButton => IsFrench ? "Annuler" : "Cancel";
    public string ConfirmResetButton => IsFrench ? "Rétablir" : "Reset";
    public string CloseButton => IsFrench ? "Fermer" : "Close";

    // Tray Menu
    public string TrayToggleVisibility => IsFrench ? "Afficher / Masquer" : "Show / Hide";
    public string TrayShow => IsFrench ? "Afficher" : "Show";
    public string TrayHide => IsFrench ? "Masquer" : "Hide";
    public string TrayAutoMode => IsFrench ? "Mode Auto" : "Auto Mode";
    public string TraySettings => IsFrench ? "Paramètres" : "Settings";
    public string TrayUpdate => IsFrench ? "Mettre à jour" : "Update";
    public string TrayCheckUpdates => TrayUpdate;
    public string TrayQuit => IsFrench ? "Quitter CherryHud" : "Quit CherryHud";
}
