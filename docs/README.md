# 🍒 CherryHud — Official Website & Landing Page

Modern, responsive, and lightweight landing page for **CherryHud** (the native gaming performance overlay for macOS).

---

## ✨ Website Features

1. **Automatic Latest Release Detection:**
   * Dynamically queries the GitHub Releases API (`https://api.github.com/repos/Matricard/CherryHud/releases`).
   * Fetches the latest release tag (e.g., `v1.0.0`) and links the download buttons directly to the `.dmg` installer for Apple Silicon (`arm64`).
   * Displays live file size information with a fallback to the repository releases page if the GitHub API rate limit is reached.
2. **Modern Gaming Aesthetics:**
   * Dark theme inspired by CherryHud (obsidian black, ruby red `#FF2A55`, and emerald mint `#00E676`).
   * Glassmorphism and typography using *Plus Jakarta Sans* and *JetBrains Mono*.
   * In-game overlay showcase using `assets/preview.png`.
3. **Zero Build Dependencies:**
   * Pure static HTML powered by Tailwind CSS CDN.
   * Can be served instantly anywhere without Node.js, npm, or bundlers.

---

## 🚀 Local Preview

To preview the website locally on your Mac, double-click `index.html` or run:

```bash
open docs/index.html
```

---

## 🌐 Deploy to GitHub Pages

1. Navigate to your GitHub repository: [https://github.com/Matricard/CherryHud](https://github.com/Matricard/CherryHud)
2. Go to **Settings > Pages**.
3. Under **Build and deployment**:
   * **Source**: `Deploy from a branch`
   * **Branch**: `main`
   * **Folder**: `/docs`
4. Click **Save**.
5. Your website will be live in under a minute at:  
   👉 **`https://matricard.github.io/CherryHud/`**

### Custom Domain (e.g., `cherryhud.app`)
In **Settings > Pages**, enter your custom domain into the **Custom domain** field and check **Enforce HTTPS**.
