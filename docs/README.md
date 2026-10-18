# 🍒 CherryHud — Site Web Officiel & Landing Page

Landing page moderne, responsive et légère pour **CherryHud** (l'overlay de performance gaming natif pour macOS).

---

## ✨ Fonctionnalités du site

1. **Téléchargement automatique de la dernière version :**
   * Interroge dynamiquement l'API GitHub (`https://api.github.com/repos/Matricard/CherryHud/releases`).
   * Détecte le dernier tag (ex: `v0.9.1`) et lie directement les boutons de téléchargement au fichier `.dmg` pour Apple Silicon (`arm64`).
   * Affiche la taille du fichier en temps réel et dispose d'un repli automatique sans faille en cas de limite d'API.
2. **Design Gaming Moderne :**
   * Thème sombre inspiré de CherryHud (noir obsidienne, néon rubis `#FF2A55`, vert émeraude `#00E676`).
   * Glassmorphism, typographie soignée (*Plus Jakarta Sans* et *JetBrains Mono*).
   * Showcase de l'interface en jeu avec `assets/preview.png`.
3. **Zéro dépendance de compilation :**
   * Fichier HTML statique pur utilisant Tailwind CSS.
   * Peut être hébergé instantanément n'importe où sans Node.js ni bundler.

---

## 🚀 Tester en local

Pour prévisualiser le site sur votre Mac, double-cliquez simplement sur `index.html` ou tapez dans votre terminal :

```bash
open /Users/math/Repos/CherryHud-Web/index.html
```

---

## 🌐 Activer sur GitHub Pages (depuis ce dépôt)

1. Allez sur votre dépôt GitHub : `https://github.com/Matricard/CherryHud`
2. Rendez-vous dans **Settings > Pages**.
3. Sous **Build and deployment** :
   * **Source** : `Deploy from a branch`
   * **Branch** : `main`
   * **Folder** : `/docs`
4. Cliquez sur **Save**.
5. Votre site sera automatiquement en ligne en moins d'une minute à l'adresse :  
   👉 **`https://matricard.github.io/CherryHud/`**

### Domaine personnalisé (ex: `cherryhud.app`)
Dans les paramètres **Settings > Pages**, renseignez votre domaine personnalisé dans le champ **Custom domain** et cochez **Enforce HTTPS**.
