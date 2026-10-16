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
    public string TabDisplay => IsFrench ? "Affichage" : "Display";
    public string TabMetrics => IsFrench ? "Métriques" : "Metrics";
    public string TabAdvanced => IsFrench ? "Avancé" : "Advanced";
    public string TabAbout => IsFrench ? "À propos" : "About";

    // Language Card
    public string LanguageCardTitle => IsFrench ? "Langue de l'application" : "Application Language";
    public string LanguageCardSubtitle => IsFrench ? "Choisissez la langue de l'interface" : "Choose the interface language";

    // Theme Card
    public string ThemeCardTitle => IsFrench ? "Thème visuel de l'overlay" : "Overlay Visual Theme";
    public string ThemeCardSubtitle => IsFrench ? "Palette classique de CherryHud" : "Classic CherryHud palette";
    public string ThemeClassic => IsFrench ? "Classique" : "Classic";

    // White Temp Card
    public string WhiteTempTitle => IsFrench ? "Températures en blanc" : "White Temperatures";
    public string WhiteTempSubtitle => IsFrench ? "Laisse les températures CPU et GPU en blanc fixe au lieu du dégradé thermique" : "Keep CPU and GPU temperatures in solid white instead of thermal gradient";

    // Auto Mode Card
    public string AutoModeTitle => IsFrench ? "Mode Automatique (En jeu)" : "Automatic Game Mode";
    public string AutoModeSubtitle => IsFrench ? "Affiche le HUD uniquement lorsqu'un jeu 3D tourne" : "Only show the HUD when a 3D game is running";

    // Position Card
    public string PositionTitle => IsFrench ? "Emplacement de l'overlay" : "Overlay Position";
    public string PositionSubtitle => IsFrench ? "Coin de l'écran où ancrer le HUD" : "Screen corner where the HUD is anchored";
    public string PositionTopLeft => IsFrench ? "Haut Gauche" : "Top Left";
    public string PositionTopRight => IsFrench ? "Haut Droite" : "Top Right";
    public string PositionBottomLeft => IsFrench ? "Bas Gauche" : "Bottom Left";
    public string PositionBottomRight => IsFrench ? "Bas Droite" : "Bottom Right";

    // Opacity Card
    public string OpacityTitle => IsFrench ? "Transparence de l'overlay" : "Overlay Transparency";
    public string OpacitySubtitle => IsFrench ? "De 40% (très transparent) à 100% (opaque)" : "From 40% (translucent) to 100% (solid)";

    // Metrics
    public string MetricGpu => IsFrench ? "Charge GPU & Température" : "GPU Load & Temperature";
    public string MetricCpu => IsFrench ? "Charge CPU & Température" : "CPU Load & Temperature";
    public string MetricVram => IsFrench ? "Mémoire Graphique (VRAM)" : "Video Memory (VRAM)";
    public string MetricRam => IsFrench ? "Mémoire Vive Système" : "System RAM Memory";
    public string MetricBattery => IsFrench ? "Batterie & État de charge" : "Battery & Charging State";
    public string MetricFps => IsFrench ? "Images par seconde & Frametime" : "Frames Per Second & Frametime";
    public string MetricGraph => IsFrench ? "Graphique de Frametime" : "Frametime Sparkline Graph";

    // Advanced
    public string GpuMemLabelTitle => IsFrench ? "Libellé mémoire vidéo" : "Video Memory Label";
    public string GpuMemLabelSubtitle => IsFrench ? "VRAM (Gaming) ou GPU MEM (Technique)" : "VRAM (Gaming) or GPU MEM (Technical)";

    public string GraphFpsTitle => IsFrench ? "Fréquence du graphique" : "Graph Refresh Rate";
    public string GraphFpsSubtitle => IsFrench ? "Taux d'animation de la courbe sparkline" : "Frametime sparkline animation rate";
    public string GraphFps60 => "60 FPS";
    public string GraphFps120 => "120 FPS";
    public string GraphFps30 => "30 FPS";

    public string ScreenCaptureTitle => IsFrench ? "Autorisation macOS ScreenCaptureKit" : "macOS ScreenCaptureKit Permission";
    public string ScreenCaptureSubtitle => IsFrench ? "Requise par macOS pour mesurer les trames des jeux." : "Required by macOS to measure game frames.";
    public string PermGranted => IsFrench ? "✓ Autorisation accordée" : "✓ Permission granted";
    public string PermRequired => IsFrench ? "⚠️ Enregistrement d'écran requis" : "⚠️ Screen recording required";
    public string RequestAccessButton => IsFrench ? "Demander l'accès" : "Request Access";
    public string OpenSystemSettingsButton => IsFrench ? "Ouvrir Réglages" : "Open Settings";

    public string AutoCheckUpdatesTitle => IsFrench ? "Vérification automatique des mises à jour" : "Automatic Update Checks";
    public string AutoCheckUpdatesSubtitle => IsFrench ? "Vérifier la présence d'une nouvelle version au démarrage" : "Check for new versions on startup";

    public string GitHubRepoTitle => IsFrench ? "Dépôt GitHub source" : "GitHub Source Repository";
    public string GitHubRepoSubtitle => IsFrench ? "Référence GitHub Releases (propriétaire/dépôt)" : "GitHub Releases reference (owner/repo)";

    // About
    public string AboutDesc => IsFrench ? "Overlay de performance gaming natif pour macOS" : "Native macOS gaming performance overlay";
    public string AboutFeat1 => IsFrench ? "Superposition au-dessus des jeux plein écran" : "Display floating over full-screen games";
    public string AboutFeat2 => IsFrench ? "Détection automatique & arrêt hors jeu" : "Automatic game detection & idle stop";
    public string AboutFeat3 => IsFrench ? "Métriques matérielles IOKit & AppleSMC" : "IOKit & AppleSMC hardware metrics";
    public string AboutFeat4 => IsFrench ? "Intégration macOS avec icône dans la barre des menus" : "macOS integration with menu bar tray icon";

    public string SoftwareUpdatesTitle => IsFrench ? "Mises à jour du logiciel" : "Software Updates";
    public string SoftwareUpdatesSubtitle => IsFrench ? "Rechercher une nouvelle version ou un artéfact" : "Check for a new version or artifact";
    public string CheckButton => IsFrench ? "Vérifier..." : "Check...";

    public string AuthorText => IsFrench ? "Créé par Mathieu Ricard" : "Created by Mathieu Ricard";
    public string LicenseText => IsFrench ? "Copyright © 2026 • Licence MIT" : "Copyright © 2026 • MIT License";

    // Footer
    public string ResetDefaultsButton => IsFrench ? "Rétablir par défaut" : "Reset to Defaults";
    public string CloseButton => IsFrench ? "Fermer" : "Close";

    // Tray Menu
    public string TrayToggleVisibility => IsFrench ? "Afficher / Masquer" : "Show / Hide";
    public string TrayAutoMode => IsFrench ? "Mode Auto (En jeu uniquement)" : "Auto Mode (In-Game Only)";
    public string TraySettings => IsFrench ? "Paramètres..." : "Settings...";
    public string TrayCheckUpdates => IsFrench ? "Vérifier les mises à jour..." : "Check for Updates...";
    public string TrayQuit => IsFrench ? "Quitter CherryHud" : "Quit CherryHud";
}
