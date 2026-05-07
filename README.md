# 🍇 Grapetree Toolkit (GTK)

> Unity Technical Artist 工具集 — 模块化、轻量、管线无关。

GTK 是一个面向 Technical Artist 的开源 Unity Editor 工具集。
所有工具位于 `Tools > GTK` 菜单下，专注于资源制作和场景工作流中的具体痛点。

---

## 🔧 工具

| 工具 | 菜单位置 | 功能 |
|------|----------|------|
| **Texture Channel Merge** | `Tools > GTK > Texture Channel Merge` | 纹理通道合并/重排 — 将多个源纹理的 RGBA 通道重新映射到一张输出图。支持 PNG/JPG/TGA。 |
| **Vertex Paint** | `Tools > GTK > Vertex Paint` | 场景视图顶点颜色笔刷。在 Mesh 副本上绘制，可另存为新资产或写回原 Mesh。通道：RGBA/R/G/B/A/Smooth。 |

---

## 📦 安装

### 通过 UPM git URL

```
Window → Package Manager → + → Add package from git URL
```

```
https://github.com/ANAYGrapeTree/grapetree.git
```

### 本地开发

```bash
git clone git@github.com:ANAYGrapeTree/grapetree.git  Packages/com.grapetree.gtk
```

或在 `Packages/manifest.json` 中添加 `file:` 引用：

```json
{
  "dependencies": {
    "com.grapetree.gtk": "file:../grapetree"
  }
}
```

---

## 🧩 目录结构

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

## 📋 要求

- **Unity 2022.3** 或更新
- **团结引擎** 2022.3+ 兼容
- 支持 Built-in、URP、HDRP

---

## 📄 许可

MIT © [ANAYGrapeTree](https://github.com/ANAYGrapeTree)

---

# 🍇 Grapetree Toolkit (GTK)

> Unity Editor tools for Technical Artists — modular, lightweight, pipeline-agnostic.

GTK is an open-source collection of Unity Editor utilities built for Technical Artists.
Each tool lives under `Tools > GTK` and targets a specific pain point in asset preparation and scene workflow.

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
