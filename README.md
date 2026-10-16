# MacTemp 🌡️🎮

Le HUD de performance ultime pour macOS, inspiré de **MangoHud** et développé avec **Avalonia UI** et **.NET 8+**.

Affichez en temps réel un overlay flottant ultra-complet pour vos jeux et applications : **GPU, CPU, RAM, VRAM, FPS, Frametime, Batterie, Horloge et Graphique d'historique**.

---

## ✨ Fonctionnalités complètes MangoHud

### 🎮 En-tête de session
- **Nom du jeu / Application active** : Détection automatique de la fenêtre active au premier plan.
- **Batterie** : État de charge et pourcentage restant (`🔋 80%` ou `⚡ 80%`).
- **Horloge système** : Heure en temps réel.

### 🔴 GPU (Coral)
- **Température GPU** en temps réel via `AppleSMC` natif.
- **Charge GPU (%)** : Taux d'utilisation du processeur graphique via `IOAccelerator`.
- **VRAM Allouée** : Quantité de mémoire vidéo utilisée (en Go).

### 🔵 CPU (Cyan)
- **Température CPU** en temps réel via `AppleSMC` natif.
- **Charge CPU (%)** : Utilisation globale des cœurs processeur via l'API Mach du noyau.

### 🟣 RAM (Royal Blue)
- **Consommation Mémoire** : RAM utilisée / RAM totale (ex. `18.9 / 24 GB`).
- **Pourcentage d'utilisation** de la mémoire vive.

### 🟢 FPS & Frametime (Lime Green)
- **Compteur de FPS réel** (`ScreenCaptureKit`) filtré sur les trames effectives du jeu (`SCFrameStatusComplete`).
- **Temps de trame (*Frametime*)** en millisecondes (ex. `16.6 ms` à 60 FPS).
- **Graphique Sparkline dynamique** :
  - Défilement en temps réel (4 mises à jour par seconde).
  - Ligne de repère cible (60 / 120 FPS).
  - Dégradé vert néon sous la courbe et point lumineux sur la trame active.

### 🎨 Design & Ergonomie
- **Extérieur 100% transparent** : Aucune bordure parasite, aucune ombre carrée.
- **Déplacement libre** : Déplacez le widget n'importe où sur l'écran par simple glisser-déposer.
- **Licence MIT** : Code source 100% permissif et ouvert.

---

## 🔒 Autorisation macOS (Enregistrement de l'écran)

Pour que macOS autorise le widget à calculer les FPS exacts du jeu sans bridage système :
1. Lancez l'application.
2. Si `Perm. requise` apparaît sur la ligne FPS, cliquez dessus ou accédez à :  
   **Réglages Système > Confidentialité et sécurité > Enregistrement de l'écran**.
3. Cochez l'autorisation pour votre application / terminal. Cette étape ne se fait qu'une seule fois.

---

## 🚀 Compilation & Lancement

```bash
dotnet run --project MacTemp/MacTemp.csproj
```

---

## 📜 Licence

Ce projet est sous licence [MIT](LICENSE).
