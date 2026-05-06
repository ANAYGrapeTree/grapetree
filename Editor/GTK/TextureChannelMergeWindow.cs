using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GTK
{
    public class TextureChannelMergeWindow : EditorWindow
    {
        private const string WindowTitle = "Texture Channel Merge";

        // ─── Mode ──────────────────────────────────────────────────────
        private enum ToolMode { Merge, Swizzle }
        [SerializeField] private ToolMode _mode = ToolMode.Merge;

        // ─── Merge state ───────────────────────────────────────────────
        [SerializeField] private ChannelSource[] _sources = new ChannelSource[4];
        [SerializeField] private bool _processInLinear;

        // ─── Swizzle state ─────────────────────────────────────────────
        [SerializeField] private Texture2D _swizzleSource;
        [SerializeField] private SwizzleOp[] _swizzleOps = { SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.RGBA };
        [SerializeField] private float[] _swizzleCustoms = { 0f, 0f, 0f, 0f };

        // ─── Save state ────────────────────────────────────────────────
        [SerializeField] private SaveFormat _saveFormat = SaveFormat.PNG;
        [SerializeField][Range(1, 100)] private int _jpgQuality = 85;
        [SerializeField] private string _fileName = "MergedTexture";

        // ─── Preview ──────────────────────────────────────────────────
        private Texture2D _previewTex;
        private bool _previewValid;

        // ─── Log ───────────────────────────────────────────────────────
        private List<string> _logEntries = new List<string>();
        private Vector2 _logScroll;
        private bool _logExpanded;

        // ─── Constants ─────────────────────────────────────────────────
        private static readonly string[] ChanLbl = { "R", "G", "B", "A" };
        private static readonly float[] DefColor = { 0f, 0f, 0f, 1f };
        private enum ChanIdx { R, G, B, A }

        private static readonly SaveFormat[] FmtVals = { SaveFormat.PNG, SaveFormat.JPG, SaveFormat.TGA };
        private static readonly string[] FmtLabels = { "PNG", "JPG", "TGA" };

        private static readonly SwizzleOp[] SwizzleOpVals =
        {
            SwizzleOp.Zero, SwizzleOp.One, SwizzleOp.Gray,
            SwizzleOp.Custom, SwizzleOp.RGBA, SwizzleOp.ReverseRGBA
        };
        private static readonly string[] SwizzleLabels =
            { "0", "1", "gray", "custom", "RGBA", "reverse RGBA" };

        // ─── Window lifecycle ──────────────────────────────────────────

        [MenuItem("Tools/GTK/Texture Channel Merge")]
        private static void ShowWindow()
        {
            var w = GetWindow<TextureChannelMergeWindow>(false, WindowTitle);
            w.minSize = new Vector2(400, 420);
            w.Show();
        }

        private void OnEnable()
        {
            for (int i = 0; i < 4; i++)
                if (_sources[i].sourceChannel == 0 && _sources[i].defaultColor == 0 && i < DefColor.Length)
                    _sources[i].defaultColor = DefColor[i];
            _processInLinear = TextureChannelMergeUtility.IsProjectLinear();
            AppendLog("Ready.");
        }

        private void OnDisable() => DestroyPreview();

        // ─── Main GUI ──────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawModeToggle();
            EditorGUILayout.Space(4);

            switch (_mode)
            {
                case ToolMode.Merge:   DrawMergeLayout(); break;
                case ToolMode.Swizzle: DrawSwizzleLayout(); break;
            }

            EditorGUILayout.Space(6);
            DrawSettings();
            EditorGUILayout.Space(6);
            DrawPreviewSection();
            EditorGUILayout.Space(6);
            DrawSaveSection();
            EditorGUILayout.Space(4);
            DrawLogSection();
        }

        // ─── Mode Toggle ───────────────────────────────────────────────
        private void DrawModeToggle()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            _mode = (ToolMode)GUILayout.Toolbar((int)_mode, new[] { "Merge", "Swizzle" },
                GUILayout.Width(240), GUILayout.Height(22));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════════
        //  MERGE LAYOUT — 4 columns (R G B A) vertically stacked
        // ═══════════════════════════════════════════════════════════════

        private void DrawMergeLayout()
        {
            EditorGUILayout.LabelField("Source Channels", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            float colW = (position.width - 44f) / 4f;
            float texSize = Mathf.Min(colW - 6, 80f);

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < 4; i++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(colW));

                // Label
                EditorGUILayout.LabelField(ChanLbl[i], EditorStyles.boldLabel, GUILayout.Width(colW));

                // Square texture field
                var texRect = EditorGUILayout.GetControlRect(false, texSize, GUILayout.Width(texSize));
                _sources[i].texture = (Texture2D)EditorGUI.ObjectField(
                    texRect, _sources[i].texture, typeof(Texture2D), false);

                // Channel popup
                EditorGUI.BeginDisabledGroup(_sources[i].texture == null);
                _sources[i].sourceChannel = EditorGUILayout.IntPopup(" ", _sources[i].sourceChannel,
                    new[] { "R", "G", "B", "A" }, new[] { 0, 1, 2, 3 });
                EditorGUI.EndDisabledGroup();

                // Float input (instead of slider)
                EditorGUI.BeginDisabledGroup(_sources[i].texture != null);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(" ", GUILayout.Width(8));
                float v = EditorGUILayout.FloatField(_sources[i].defaultColor, GUILayout.Width(colW - 12));
                if (_sources[i].texture == null)
                    _sources[i].defaultColor = Mathf.Clamp01(v);
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
                if (i < 3) GUILayout.Space(4);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════════
        //  SWIZZLE LAYOUT — source field + 4 columns of ops
        // ═══════════════════════════════════════════════════════════════

        private void DrawSwizzleLayout()
        {
            EditorGUILayout.LabelField("Source Texture", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Square texture field
            float texSize = 80f;
            var texRect = EditorGUILayout.GetControlRect(false, texSize, GUILayout.Width(texSize));
            _swizzleSource = (Texture2D)EditorGUI.ObjectField(
                texRect, _swizzleSource, typeof(Texture2D), false);

            if (_swizzleSource == null)
            {
                EditorGUILayout.HelpBox("Select a source texture to swizzle.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Channel Operations", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            float colW = (position.width - 44f) / 4f;

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < 4; i++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(colW));

                // Label
                EditorGUILayout.LabelField(ChanLbl[i], EditorStyles.boldLabel, GUILayout.Width(colW));

                // SwizzleOp popup
                int si = (int)_swizzleOps[i];
                si = EditorGUILayout.IntPopup(" ", si, SwizzleLabels, new[] { 0, 1, 2, 3, 4, 5 });
                _swizzleOps[i] = (SwizzleOp)si;

                // Custom float input (only if Custom selected)
                if (_swizzleOps[i] == SwizzleOp.Custom)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(" ", GUILayout.Width(8));
                    float cv = EditorGUILayout.FloatField(_swizzleCustoms[i], GUILayout.Width(colW - 12));
                    _swizzleCustoms[i] = Mathf.Clamp01(cv);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    // Placeholder to keep alignment
                    GUILayout.Space(EditorGUIUtility.singleLineHeight + 2);
                }

                EditorGUILayout.EndVertical();
                if (i < 3) GUILayout.Space(4);
            }

            EditorGUILayout.EndHorizontal();

            // Presets
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Presets", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Identity", EditorStyles.miniButton))
                ApplySwizzlePreset(SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.RGBA);
            if (GUILayout.Button("Reverse", EditorStyles.miniButton))
                ApplySwizzlePreset(SwizzleOp.ReverseRGBA, SwizzleOp.ReverseRGBA, SwizzleOp.ReverseRGBA, SwizzleOp.ReverseRGBA);
            if (GUILayout.Button("Clear Alpha", EditorStyles.miniButton))
                ApplySwizzlePreset(SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.Zero);
            if (GUILayout.Button("Alpha→1", EditorStyles.miniButton))
                ApplySwizzlePreset(SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.RGBA, SwizzleOp.One);
            if (GUILayout.Button("Grayscale", EditorStyles.miniButton))
                ApplySwizzlePreset(SwizzleOp.Gray, SwizzleOp.Gray, SwizzleOp.Gray, SwizzleOp.Gray);
            EditorGUILayout.EndHorizontal();
        }

        private void ApplySwizzlePreset(SwizzleOp r, SwizzleOp g, SwizzleOp b, SwizzleOp a)
        {
            _swizzleOps[0] = r;
            _swizzleOps[1] = g;
            _swizzleOps[2] = b;
            _swizzleOps[3] = a;
            _previewValid = false;
            AppendLog($"Applied swizzle preset: {r}/{g}/{b}/{a}");
        }

        // ═══════════════════════════════════════════════════════════════
        //  SETTINGS
        // ═══════════════════════════════════════════════════════════════

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _processInLinear = EditorGUILayout.ToggleLeft(
                new GUIContent("Process in Linear Space",
                    "sRGB inputs convert to linear, output converts back to gamma."),
                _processInLinear);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Format");
            int fmtIdx = System.Array.IndexOf(FmtVals, _saveFormat);
            if (fmtIdx < 0) fmtIdx = 0;
            fmtIdx = EditorGUILayout.Popup(fmtIdx, FmtLabels, GUILayout.Width(80));
            _saveFormat = FmtVals[fmtIdx];

            if (_saveFormat == SaveFormat.JPG)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.PrefixLabel("Quality");
                _jpgQuality = EditorGUILayout.IntSlider(_jpgQuality, 1, 100, GUILayout.Width(160));
            }
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PREVIEW
        // ═══════════════════════════════════════════════════════════════

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();

            float previewSize = Mathf.Min(position.width * 0.38f, 256f);
            var previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(false));

            if (_previewTex != null && _previewValid)
            {
                EditorGUI.DrawPreviewTexture(previewRect, _previewTex, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.LabelField(previewRect, "(no preview)", EditorStyles.centeredGreyMiniLabel);
                if (Event.current.type == EventType.Repaint)
                    EditorStyles.helpBox.Draw(previewRect, false, false, false, false);
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width - previewSize - 70));
            if (_previewTex != null && _previewValid)
            {
                EditorGUILayout.LabelField($"Size: {_previewTex.width} \u00d7 {_previewTex.height}");
                EditorGUILayout.LabelField($"Format: {_saveFormat}");
                if (_saveFormat == SaveFormat.JPG)
                    EditorGUILayout.LabelField($"Quality: {_jpgQuality}");
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            if (GUILayout.Button(_previewValid ? "Refresh Preview" : "Generate Preview", GUILayout.Height(24)))
                GeneratePreview();

            if (!_previewValid && _previewTex != null)
                EditorGUILayout.LabelField("Preview outdated.", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void GeneratePreview()
        {
            DestroyPreview();
            try
            {
                switch (_mode)
                {
                    case ToolMode.Merge:
                        if (!HasAnyTexture())
                        {
                            AppendLog("Preview cancelled: assign at least one source texture.");
                            return;
                        }
                        _previewTex = TextureChannelMergeUtility.MergePreview(_sources, _processInLinear);
                        break;

                    case ToolMode.Swizzle:
                        if (_swizzleSource == null)
                        {
                            AppendLog("Preview cancelled: select a source texture.");
                            return;
                        }
                        _previewTex = TextureChannelMergeUtility.SwizzleChannels(
                            _swizzleSource, _swizzleOps, _swizzleCustoms, _processInLinear);
                        break;
                }
                _previewValid = true;
                AppendLog($"Preview generated: {_previewTex.width}\u00d7{_previewTex.height}");
            }
            catch (System.Exception ex)
            {
                _previewValid = false;
                AppendLog($"Preview failed: {ex.Message}");
                Debug.LogError($"Texture preview failed: {ex}");
            }
        }

        private void DestroyPreview()
        {
            if (_previewTex != null)
            {
                Object.DestroyImmediate(_previewTex);
                _previewTex = null;
            }
            _previewValid = false;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SAVE
        // ═══════════════════════════════════════════════════════════════

        private void DrawSaveSection()
        {
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("File Name");
            string ext = TextureChannelMergeUtility.GetExtension(_saveFormat);
            string display = EditorGUILayout.TextField(_fileName + ext);
            if (display.EndsWith(ext))
                _fileName = display.Substring(0, display.Length - ext.Length);
            else
                _fileName = display;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Merge && Save", GUILayout.Height(30)))
                ExecuteSave();
        }

        private void ExecuteSave()
        {
            if (!_previewValid) GeneratePreview();
            if (!_previewValid || _previewTex == null)
            {
                AppendLog("Save cancelled: no valid preview.");
                return;
            }

            string ext = TextureChannelMergeUtility.GetExtension(_saveFormat);
            string savePath = EditorUtility.SaveFilePanel(
                "Save Merged Texture",
                "Assets",
                _fileName + ext,
                _saveFormat.ToString().ToLowerInvariant());

            if (string.IsNullOrEmpty(savePath))
            {
                AppendLog("Save cancelled by user.");
                return;
            }

            EditorUtility.DisplayProgressBar(WindowTitle, "Saving...", 0.5f);
            try
            {
                TextureChannelMergeUtility.SaveTexture(_previewTex, savePath, _saveFormat, _jpgQuality);
                AppendLog($"Saved: {savePath}");
            }
            catch (System.Exception ex)
            {
                AppendLog($"Save failed: {ex.Message}");
                Debug.LogError($"Texture save failed: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool HasAnyTexture()
        {
            for (int i = 0; i < 4; i++)
                if (_sources[i].texture != null) return true;
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        //  LOG
        // ═══════════════════════════════════════════════════════════════

        private void DrawLogSection()
        {
            _logExpanded = EditorGUILayout.Foldout(_logExpanded, "Log", true, EditorStyles.foldout);
            if (_logExpanded)
            {
                _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(80));
                EditorGUILayout.TextArea(string.Join("\n", _logEntries), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    _logEntries.Clear();
                    Repaint();
                }
            }
            else if (_logEntries.Count > 0)
            {
                var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                r = EditorGUI.IndentedRect(r);
                EditorGUI.HelpBox(r, _logEntries[_logEntries.Count - 1], MessageType.Info);
            }
        }

        private void AppendLog(string message)
        {
            _logEntries.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            _logScroll.y = float.MaxValue;
            Repaint();
        }
    }
}
