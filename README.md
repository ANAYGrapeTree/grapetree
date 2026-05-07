# 🍇 Grapetree Toolkit (GTK)

> Unity Editor tools for Technical Artists — modular, lightweight, pipeline-agnostic.

GTK is an open-source collection of Unity Editor utilities built for Technical Artists.
Each tool lives under `Tools > GTK` and targets a specific pain point in asset preparation and scene workflow.

> [**中文文档**](README.zh-CN.md) — 查看 GTK 的中文介绍。

---

## ✨ Tools

| Tool | Menu Path | Description |
|------|-----------|-------------|
| **Texture Channel Merge** | `Tools > GTK > Texture Channel Merge` | Merge/swizzle textures — remap RGBA channels from multiple sources into one output. Formats: PNG/JPG/TGA. |
| **Vertex Paint** | `Tools > GTK > Vertex Paint` | Scene-view vertex color brush. Paint on a mesh copy, export as new asset or write back to original. Channels: RGBA/R/G/B/A/Smooth. |

---

## 📦 Installation

### Via UPM git URL

```
Window → Package Manager → + → Add package from git URL
```

```
https://github.com/ANAYGrapeTree/grapetree.git
```

### Local development

```bash
git clone git@github.com:ANAYGrapeTree/grapetree.git  Packages/com.grapetree.gtk
```

Or add a `file:` reference in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.grapetree.gtk": "file:../grapetree"
  }
}
```

---

## 🧩 Structure

```
com.grapetree.gtk/
├── Editor/GTK/
│   ├── GTK.Editor.asmdef
│   ├── TextureChannelMerge/
│   │   ├── TextureChannelMergeWindow.cs
│   │   └── TextureChannelMergeUtility.cs
│   └── VertexPaint/
│       ├── VertexPaintWindow.cs
│       └── VertexPaintUtility.cs
├── Shaders/
│   └── VertexColorPreview.shader
├── Documentation/
│   └── README.md
├── package.json
└── README.md
```

---

## 🧵 Requirements

- **Unity 2022.3** or newer
- **Tuanjie Engine** 2022.3+ compatible
- Works with Built-in, URP, and HDRP

---

## 📄 License

MIT © [ANAYGrapeTree](https://github.com/ANAYGrapeTree)
