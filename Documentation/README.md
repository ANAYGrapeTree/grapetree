# Grapetree Toolkit (GTK) 使用文档

> **Package:** `com.grapetree.gtk`  
> **Version:** 0.2.1  
> **Unity:** 2022.3+ (兼容团结引擎)

---

## 安装

### 通过 UPM Git URL

```
Window → Package Manager → + → Add package from git URL
```

```
https://github.com/ANAYGrapeTree/grapetree.git
```

### 本地开发

将仓库克隆到项目 `Packages/` 目录下：

```bash
git clone git@github.com:ANAYGrapeTree/grapetree.git Packages/com.grapetree.gtk
```

或者用 `file:` 引用编辑 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.grapetree.gtk": "file:../grapetree"
  }
}
```

---

## 工具清单

### Texture Channel Merge

**菜单路径：** `Tools > GTK > Texture Channel Merge`

纹理通道合并工具，将多个源纹理的 RGBA 通道重新映射到一张输出纹理。

#### Merge 模式

- 为 R / G / B / A 各通道分别指定源纹理和通道来源
- 未指定纹理的通道使用 Default Value（浮点数输入）
- 源纹理尺寸不一致时自动按最小尺寸下采样

#### Swizzle 模式

- 单一源纹理，每通道独立选择操作
- 支持的操作：`0`（常量黑）、`1`（常量白）、`gray`（0.5）、`custom`（用户值）、`R/G/B/A`（取源通道）、`inv R/G/B/A`（取反）

#### 格式支持

| 格式 | 扩展名 | 说明 |
|------|--------|------|
| PNG | `.png` | 默认，支持透明通道 |
| JPG | `.jpg` | 可调 Quality (1-100) |
| TGA | `.tga` | Truevision Targa |

#### 操作流程

1. 选择 Merge 或 Swizzle 模式
2. 配置通道映射
3. 点击 **Generate Preview** 预览
4. 确认结果后点击 **Merge & Save** → 选择保存路径

---

### Vertex Paint

**菜单路径：** `Tools > GTK > Vertex Paint`

场景视图中直接绘制顶点颜色的画笔工具，工作在 Mesh 的副本上，不修改原始 Mesh 文件。

#### 基础操作

1. 在 Hierarchy 或 Scene 中选中目标 GameObject（需有 MeshFilter 或 SkinnedMeshRenderer）
2. 打开 **Tools > GTK > Vertex Paint**
3. 点击 **Start Paint** 进入绘制模式
4. 在 Scene View 中用 **左键拖拽** 绘制顶点颜色
5. 点击 **Stop Painting** 退出

#### 笔刷参数

| 参数 | 范围 | 调节方式 |
|------|------|---------|
| Channel | RGBA / R / G / B / A / Smooth | 下拉选择 |
| Color | RGBA 颜色 (Channel=RGBA) | Color Picker |
| Value | 0-1 (单通道模式) | 滑块 |
| Size | min ~ max | 滑块 + 两端输入框自定义上下限 |
| Falloff | 0-1 | 滑块 |
| Opacity | 0-1 | 滑块 |

#### 通道模式

| 模式 | 行为 |
|------|------|
| **RGBA** | 用选取的颜色（Brush Color）涂抹全部四个通道 |
| **R/G/B/A** | 只涂抹指定通道，强度由 Value 控制 |
| **Smooth** | 对顶点颜色做 Laplacian 平滑，基于邻接顶点颜色加权平均 |

#### 快捷键（在 Scene View 中）

| 操作 | 快捷键 |
|------|--------|
| 绘制 | `Left Mouse` 拖拽 |
| 调整笔刷大小 | `Ctrl` + 横向拖拽 |
| 调整不透明度 | `Shift` + 横向拖拽 |
| 调整 Falloff | `Ctrl+Shift` + 横向拖拽 |

#### 按钮

| 按钮 | 功能 |
|------|------|
| **Save As...** | 将工作网格另存为新 `.asset` 文件，Renderer 自动指向新网格 |
| **Save Orig** | 将顶点颜色写回原始 Mesh，随后销毁工作副本（Renderer 恢复指向原始 Mesh） |
| **Fill** | 用当前颜色/值填充全部顶点 |
| **Start Paint** | 创建 Mesh 工作副本，进入绘制模式 |
| **Start Preview** | 替换材质为顶点颜色专用预览 Shader |

#### 工作副本机制

- 点击 **Start Paint** 时，自动创建原始 Mesh 的 `Instantiate` 副本（`HideFlags.DontSave`）
- 所有绘制操作只修改这个副本
- 关闭窗口后副本**保留**在 GameObject 上，重新打开窗口可继续绘制
- Script Domain Reload 后自动检测并恢复原始 Mesh
- 完成后使用 **Save As...** 或 **Save Orig** 持久化

---

## 目录结构

```
com.grapetree.gtk/
├── package.json
├── README.md
├── Documentation/
│   └── README.md                  ← 本文档
├── Shaders/
│   └── VertexColorPreview.shader  ← 顶点颜色预览 Shader
├── Editor/
│   └── GTK/
│       ├── GTK.Editor.asmdef
│       ├── TextureChannelMerge/
│       │   ├── TextureChannelMergeWindow.cs
│       │   └── TextureChannelMergeUtility.cs
│       └── VertexPaint/
│           ├── VertexPaintWindow.cs
│           └── VertexPaintUtility.cs
```

---

## 渲染管线兼容性

GTK 工具仅使用 Unity 核心 API（`UnityEngine` + `UnityEditor`），不依赖特定渲染管线。

| 工具 | Built-in | URP | HDRP |
|------|----------|-----|------|
| Texture Channel Merge | ✅ | ✅ | ✅ |
| Vertex Paint | ✅ | ✅ | ✅ |
| Vertex Paint 预览 Shader | ✅ | ✅ | ✅ |
