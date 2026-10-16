# MacTemp 🌡️

Widget de bureau moderne et léger développé avec **Avalonia UI** et **.NET 8+** pour afficher en temps réel les températures du **CPU** et du **GPU** sur macOS.

---

## ✨ Fonctionnalités & Améliorations

- ⚡ **P/Invoke natif macOS (IOKit AppleSMC)** : Suppression complète de toute dépendance à des binaires externes (`smctemp` CLI) et de `Process.Start`. La lecture se fait désormais directement en mémoire via les API C d'Apple (`IOKit.framework`).
- 🍏 **Compatibilité universelle** :
  - Support de toutes les générations **Apple Silicon** (M1, M2, M3, M4, M5, ...).
  - Support des processeurs **Intel**.
  - Détection automatique et dynamique des sondes disponibles.
- 🎨 **Design HUD Moderne** :
  - Fenêtre flottante translucide (Mica / Acrylic blur).
  - Coins arrondis et ombre portée élégante.
  - Déplacement de la fenêtre par simple glisser-déposer (*drag & drop*).
  - Indicateurs de couleur dynamiques (Cyan/Corail par défaut, Orange/Rouge en cas de chauffe élevée).
- 🧱 **Architecture MVVM propre** :
  - Utilisation de `CommunityToolkit.Mvvm`.
  - Service de monitoring `SmcTemperatureService` découplé via l'interface `ITemperatureService`.
  - Boucle d'actualisation asynchrone sécurisée avec `PeriodicTimer` et `CancellationToken`.
- 📄 **Licence 100% permissive (MIT)** : Le code source est désormais libre de toute contrainte copyleft GPL.

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
