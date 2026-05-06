# 🍇 Grapetree Toolkit (GTK)

> Unity Editor tools for Technical Artists — modular, lightweight, pipeline-friendly.

**GTK** (short for Grapetree Toolkit) is an open-source collection of Unity Editor utilities built for Technical Artists who work in real-time rendering pipelines. Each tool lives in its own namespace under `GTK` and targets a specific pain point in the asset preparation workflow.

---

## ✨ Features

| Tool | Status | Description |
|------|--------|-------------|
| **Texture Channel Merge** | ✅ Planned | Merge multiple source textures into a single texture's RGBA channels. |
| *(more coming)* | 📋 | Mesh tools, material batching, pipeline validation, etc. |

---

## 📦 Installation

### Via Unity Package Manager (UPM)

1. Open Unity → **Window** → **Package Manager**
2. Click **+** → **Add package from git URL**
3. Paste:

   ```
   https://github.com/ANAYGrapeTree/grapetree.git
   ```

### Or clone manually

```bash
git clone https://github.com/ANAYGrapeTree/grapetree.git
```

Then copy the `com.grapetree.gtk` folder into your project's `Packages/` directory.

---

## 🛠 Tools

### Texture Channel Merge

**Menu:** `Tools > GTK > Texture Channel Merge`

Select up to 4 source textures and assign each to a target channel (R, G, B, A) of the output texture. Supports:

- Source → target channel remapping (e.g., TextureA.R → Output.G)
- Single-channel grayscale or multi-channel sources
- PNG / EXR export to project Assets

---

## 🧩 Structure

```
com.grapetree.gtk/
├── Editor/              # Editor-only scripts and windows
│   └── GTK/             # Tool implementations
│       ├── TextureChannelMergeWindow.cs
│       └── TextureChannelMergeUtility.cs
├── Runtime/             # Runtime components (if any)
│   └── GTK/
├── Documentation/       # Docs and usage guides
├── package.json         # UPM package manifest
└── README.md
```

---

## 🧵 Requirements

- **Unity 2022.3** or newer
- **团结引擎 (Tuanjie)** 2022.3+ also compatible
- Works with Built-in, URP, and HDRP

---

## 📄 License

MIT © [ANAYGrapeTree](https://github.com/ANAYGrapeTree)

---

*Made for Technical Artists, by a Technical Artist.*
