<div align="center">
  <img src="MacTemp/Assets/logo.png" width="180" height="180" alt="CherryHud Logo" />
  <h1>CherryHud 🍒⚡</h1>
  <p>
    <b>Le HUD de performance ultime pour macOS</b><br />
    <i>L'équivalent macOS de <b>MangoHud</b>, propulsé par Avalonia UI, .NET 8 et les APIs système natives Apple (IOKit, ScreenCaptureKit, AppleSMC).</i>
  </p>

  <p>
    <a href="#-fonctionnalités-mangohud"><img src="https://img.shields.io/badge/Overlay-Full%20Screen%20Gaming-FF2A55?style=flat-square" alt="Gaming HUD" /></a>
    <a href="#-compilation--lancement"><img src="https://img.shields.io/badge/Platform-macOS%20Apple%20Silicon%20%26%20Intel-00E676?style=flat-square" alt="macOS" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue?style=flat-square" alt="License MIT" /></a>
  </p>
</div>

---

## 🎮 Présentation

**CherryHud** est un overlay de performances en jeu léger, transparent et fluide conçu spécifiquement pour **macOS**. Inspiré de la disposition emblématique de **MangoHud** sous Linux, il affiche les métriques vitales de votre Mac en temps réel sans jamais perturber votre session de jeu :

- **Superposition au-dessus des jeux plein écran** (World of Warcraft, Baldur's Gate 3, Final Fantasy XIV, émulateurs, etc.) grâce au niveau de fenêtre `NSScreenSaverWindowLevel` et au comportement `NSWindowCollectionBehaviorFullScreenAuxiliary`.
- **Zéro interruption de jeu** : isolation native (`NSApplication` swizzling) pour ne jamais voler le focus clavier/souris ni invoquer intempestivement le Dock ou la barre des menus lors des clics.
- **Rendu graphique 100% transparent** : carte sombre translucide sans bordure carrée parasite, avec texte net et contrasté en police 16px bold pour une lisibilité parfaite en pleine action.

---

## ✨ Métriques & Fonctionnalités

### 🕹️ En-tête de session
- **Application / Jeu actif** : Détection automatique du nom du jeu au premier plan.
- **Batterie** : Pourcentage et statut de charge (`🔋 85%` ou `⚡ 85%`).
- **Horloge système** : Heure en temps réel (`HH:mm:ss`).
- **Bouton Fermer subtil (✕)** : Quittez en un clic ou masquez le HUD.

### 🔴 GPU (MangoHud Coral)
- **Charge GPU (%)** : Taux d'utilisation effectif du processeur graphique via `IOAccelerator`.
- **Température GPU** en temps réel lue directement dans les registres SMC matériels (`AppleSMC`).
- **VRAM Allouée** : Quantité de mémoire vidéo active (en Go).

### 🔵 CPU (MangoHud Cyan)
- **Charge CPU (%)** : Utilisation globale des cœurs processeur via l'API Mach du noyau macOS.
- **Température CPU** en temps réel via `AppleSMC` natif.

### 🟣 RAM (MangoHud Royal Blue)
- **Consommation Mémoire** : Mémoire vive utilisée (en Go).

### 🟢 FPS & Frametime (MangoHud Lime Green)
- **Compteur de FPS réel** (`ScreenCaptureKit`) filtré sur les trames effectives (`SCFrameStatusComplete`).
- **Temps de trame (*Frametime*)** mesuré trame par trame en millisecondes (ex. `16.6 ms` à 60 FPS, `8.3 ms` à 120 FPS).
- **Graphique Frametime épuré (Sparkline néon)** :
  - Historique fluide de 7,5 secondes (mise à jour à 100 ms).
  - Ligne néon vert pur (`#00E676`) avec point lumineux de tête pour visualiser instantanément la régularité du *frame pacing* et les micro-saccades (*stutters*).

---

## 🎛️ Contrôles & Intégration macOS

- **Déplacement libre** : Glissez-déposez la fenêtre n'importe où sur votre écran.
- **Icône dans la barre des menus (TrayIcon)** :
  - **Afficher / Masquer** : Permet de cacher temporairement le HUD d'un clic.
  - **Quitter CherryHud**.
- **Raccourcis clavier** :
  - <kbd>Cmd</kbd> + <kbd>Q</kbd> : Ferme proprement l'application.
- **Menu contextuel** : Clic droit sur le HUD pour accéder directement à « Quitter CherryHud ».

---

## 🔒 Autorisation macOS (Enregistrement de l'écran)

Pour que macOS autorise le widget à capturer les trames effectives de l'écran pour calculer les FPS sans aucun bridage :
1. Lancez **CherryHud**.
2. Si la ligne FPS affiche `Perm. requise`, cliquez dessus ou rendez-vous dans :  
   **Réglages Système > Confidentialité et sécurité > Enregistrement de l'écran**.
3. Activez l'autorisation pour l'application ou le terminal exécutant l'application. Cette validation ne s'effectue qu'une seule fois.

---

## 🚀 Compilation & Lancement

### Prérequis
- macOS 13.0+ (Ventura, Sonoma, Sequoia) sur Mac Apple Silicon (M1/M2/M3/M4) ou Intel.
- SDK .NET 8.0+.
- Clang / Xcode Command Line Tools (`xcode-select --install`).

### Lancer en ligne de commande

```bash
# Compilation et lancement direct
dotnet run --project MacTemp/MacTemp.csproj
```

---

## 📜 Licence

Ce projet est distribué sous licence [MIT](LICENSE).
