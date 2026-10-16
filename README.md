# MacTemp 🌡️

Widget de bureau moderne et léger développé avec **Avalonia UI** et **.NET 8+** pour afficher en temps réel les températures du **CPU**, du **GPU** et les **FPS de vos jeux** avec un graphique dynamique style **MangoHud** sur macOS.

---

## ✨ Fonctionnalités & Améliorations

- ⚡ **P/Invoke natif macOS (IOKit AppleSMC)** : Suppression complète de toute dépendance à des binaires externes (`smctemp` CLI) et de `Process.Start`. La lecture se fait directement en mémoire via les API C d'Apple (`IOKit.framework`).
- 🎮 **Compteur de FPS de jeu en direct (`ScreenCaptureKit`)** :
  - Détection automatique de la fenêtre de jeu ou de l'application active au premier plan (y compris en plein écran).
  - Échantillonnage ultra-rapide et optimisé (buffer miniature 64×64, impact CPU/GPU quasi nul).
  - Affiche les **vrais 60 / 120 / 144+ FPS du jeu**, sans aucun bridage par macOS.
- 📈 **Graphique FPS style MangoHud** :
  - Sparkline réactive en temps réel avec dégradé vert néon (*framerate history*).
  - Ligne de repère cible (60 / 120 FPS).
  - Point lumineux sur la valeur actuelle.
- 🍏 **Compatibilité universelle** :
  - Support de toutes les générations **Apple Silicon** (M1, M2, M3, M4, M5, ...).
  - Support des processeurs **Intel**.
  - Détection automatique et dynamique des sondes disponibles.
- 🎨 **Design HUD Moderne & Flottant** :
  - Carte aux coins arrondis sur fond 100% transparent (aucun artefact de contour ou de fenêtre).
  - Badges colorés (`CPU`, `GPU`, `FPS`).
  - Déplacement de la fenêtre par simple glisser-déposer (*drag & drop*).
  - Indicateurs de couleur dynamiques (Cyan, Corail, Vert néon, Orange/Rouge en cas de chauffe).
- 🧱 **Architecture MVVM propre** :
  - Utilisation de `CommunityToolkit.Mvvm`.
  - Services de monitoring découplés (`SmcTemperatureService`, `FpsService`).
  - Boucle d'actualisation asynchrone avec `PeriodicTimer` et `CancellationToken`.
- 📄 **Licence 100% permissive (MIT)** : Le code source est entièrement libre de toute contrainte copyleft GPL.

---

## 🔒 Autorisation macOS (Enregistrement de l'écran)

Pour que macOS autorise le widget à lire les images du jeu sans le brider à 8 FPS, l'autorisation d'enregistrement de l'écran est requise :
1. Lancez l'application.
2. Si `Perm. requise` s'affiche, cliquez dessus ou allez dans :
   **Réglages Système > Confidentialité et sécurité > Enregistrement de l'écran**.
3. Cochez l'autorisation pour votre application / terminal. Cette étape ne se fait qu'**une seule fois**.

---

## 🚀 Compilation & Exécution

### Prérequis
- macOS (Apple Silicon ou Intel)
- .NET 8.0 SDK ou version ultérieure (ou via JetBrains Rider)

### Lancement
```bash
dotnet run --project MacTemp/MacTemp.csproj
```

---

## 📜 Licence

Ce projet est sous licence [MIT](LICENSE).
