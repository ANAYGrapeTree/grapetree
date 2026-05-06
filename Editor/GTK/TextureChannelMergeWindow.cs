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
        [SerializeField] private SwizzleOp[] _swizzleOps = { SwizzleOp.SourceR, SwizzleOp.SourceG, SwizzleOp.SourceB, SwizzleOp.SourceA };
        [SerializeField] private float[] _swizzleCustoms = { 0f, 0f, 0f, 0f };

        // ─── Save state ────────────────────────────────────────────────
        [SerializeField] private SaveFormat _saveFormat = SaveFormat.PNG;
        [SerializeField][Range(1, 100)] private int _jpgQuality = 85;

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
        private static readonly SaveFormat[] FmtVals = { SaveFormat.PNG, SaveFormat.JPG, SaveFormat.TGA };

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
        //  MERGE LAYOUT — manual rects for exact square control
        // ═══════════════════════════════════════════════════════════════

        private void DrawMergeLayout()
        {
            EditorGUILayout.LabelField("Source Channels", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            float colW = (position.width - 64f) / 4f;
            float texSize = Mathf.Min(colW - 6, 80f);
            float spacing = 4f;
            float rowH = EditorGUIUtility.singleLineHeight;
            float totalH = rowH + spacing + texSize + spacing + rowH + spacing + rowH + 2;
            var area = EditorGUILayout.GetControlRect(false, totalH);
            area = EditorGUI.IndentedRect(area);

            for (int i = 0; i < 4; i++)
            {
                float x = area.x + i * (colW + spacing);
                float cy = area.y;

                // Label
                EditorGUI.LabelField(new Rect(x, cy, colW, rowH), ChanLbl[i], EditorStyles.boldLabel);
                cy += rowH + spacing;

                // Square texture field (centered in column)
                float ox = x + (colW - texSize) * 0.5f;
                _sources[i].texture = (Texture2D)EditorGUI.ObjectField(
                    new Rect(ox, cy, texSize, texSize), _sources[i].texture, typeof(Texture2D), false);
                cy += texSize + spacing;

                // Channel popup
                EditorGUI.BeginDisabledGroup(_sources[i].texture == null);
                _sources[i].sourceChannel = EditorGUI.Popup(
                    new Rect(x, cy, colW, rowH), _sources[i].sourceChannel, ChanLbl, EditorStyles.popup);
                EditorGUI.EndDisabledGroup();
                cy += rowH + spacing;

                // Float input (replaces slider)
                EditorGUI.BeginDisabledGroup(_sources[i].texture != null);
                float v = EditorGUI.FloatField(
                    new Rect(x, cy, colW, rowH), _sources[i].defaultColor, EditorStyles.numberField);
                if (_sources[i].texture == null)
                    _sources[i].defaultColor = Mathf.Clamp01(v);
                EditorGUI.EndDisabledGroup();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  SWIZZLE LAYOUT — source field (square) + 4 columns
        // ═══════════════════════════════════════════════════════════════

        private void DrawSwizzleLayout()
        {
            EditorGUILayout.LabelField("Source Texture", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Square source texture field
            float texSize = 80f;
            var texArea = EditorGUILayout.GetControlRect(false, texSize + 4);
            texArea = EditorGUI.IndentedRect(texArea);
            _swizzleSource = (Texture2D)EditorGUI.ObjectField(
                new Rect(texArea.x, texArea.y, texSize, texSize), _swizzleSource, typeof(Texture2D), false);
            texArea.y += texSize + 4;

            if (_swizzleSource == null)
            {
                EditorGUILayout.HelpBox("Select a source texture to swizzle.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Channel Operations", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            float colW = (position.width - 64f) / 4f;
            float spacing = 4f;
            float rowH = EditorGUIUtility.singleLineHeight;
            float totalH = rowH + spacing + rowH + spacing + rowH;
            var cArea = EditorGUILayout.GetControlRect(false, totalH);
            cArea = EditorGUI.IndentedRect(cArea);

            for (int i = 0; i < 4; i++)
            {
                float x = cArea.x + i * (colW + spacing);
                float cy = cArea.y;

                // Label
                EditorGUI.LabelField(new Rect(x, cy, colW, rowH), ChanLbl[i], EditorStyles.boldLabel);
                cy += rowH + spacing;

                // SwizzleOp popup
                int idx = (int)_swizzleOps[i];
                idx = EditorGUI.Popup(new Rect(x, cy, colW, rowH), idx,
                    new[] { "0", "1", "gray", "custom", "R", "G", "B", "A", "inv R", "inv G", "inv B", "inv A" },
                    EditorStyles.popup);
                _swizzleOps[i] = (SwizzleOp)idx;
                cy += rowH + spacing;

                // Custom float input or placeholder
                if (_swizzleOps[i] == SwizzleOp.Custom)
                {
                    float cv = EditorGUI.FloatField(new Rect(x, cy, colW, rowH),
                        _swizzleCustoms[i], EditorStyles.numberField);
                    _swizzleCustoms[i] = Mathf.Clamp01(cv);
                }
            }
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
            var fmtR = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight);
            int fi = System.Array.IndexOf(FmtVals, _saveFormat);
            fi = EditorGUI.Popup(fmtR, fi, new[] { "PNG", "JPG", "TGA" }, EditorStyles.popup);
            if (fi >= 0) _saveFormat = FmtVals[fi];

            if (_saveFormat == SaveFormat.JPG)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.PrefixLabel("Quality");
                var jsr = GUILayoutUtility.GetRect(160f, EditorGUIUtility.singleLineHeight);
                _jpgQuality = EditorGUI.IntSlider(jsr, _jpgQuality, 1, 100);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PREVIEW  — centered with info on the right
        // ═══════════════════════════════════════════════════════════════

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            float previewSize = Mathf.Min(position.width * 0.35f, 200f);
            float infoW = position.width - previewSize - 80f;
            float rowH = EditorGUIUtility.singleLineHeight;
            float sectionH = Mathf.Max(previewSize, rowH * 3 + 30);

            var area = EditorGUILayout.GetControlRect(false, sectionH);
            area = EditorGUI.IndentedRect(area);

            // Preview on left (centered vertically)
            float py = area.y + (sectionH - previewSize) * 0.5f;
            var previewRect = new Rect(area.x, py, previewSize, previewSize);

            if (_previewTex != null && _previewValid)
                EditorGUI.DrawPreviewTexture(previewRect, _previewTex, null, ScaleMode.ScaleToFit);
            else
            {
                EditorGUI.LabelField(previewRect, "(no preview)", EditorStyles.centeredGreyMiniLabel);
                if (Event.current.type == EventType.Repaint)
                    EditorStyles.helpBox.Draw(previewRect, false, false, false, false);
            }

            // Info on right
            float ix = previewRect.xMax + 10;
            float iy = area.y;

            if (_previewTex != null && _previewValid)
            {
                EditorGUI.LabelField(new Rect(ix, iy, infoW, rowH), $"Size: {_previewTex.width}\u00d7{_previewTex.height}");
                iy += rowH + 2;
                EditorGUI.LabelField(new Rect(ix, iy, infoW, rowH), $"Format: {_saveFormat}");
                iy += rowH + 2;
                if (_saveFormat == SaveFormat.JPG)
                {
                    EditorGUI.LabelField(new Rect(ix, iy, infoW, rowH), $"Quality: {_jpgQuality}");
                    iy += rowH + 2;
                }
            }

            // Preview button
            float btnY = area.y + sectionH - 24f;
            if (GUI.Button(new Rect(ix, btnY, infoW, 24f),
                _previewValid ? "Refresh Preview" : "Generate Preview"))
                GeneratePreview();

            if (!_previewValid && _previewTex != null)
            {
                btnY -= rowH + 2;
                EditorGUI.LabelField(new Rect(ix, btnY, infoW, rowH), "Preview outdated.", EditorStyles.miniLabel);
            }
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
        //  SAVE  — no File Name field (dialog handles it)
        // ═══════════════════════════════════════════════════════════════

        private void DrawSaveSection()
        {
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
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
                "Save Merged Texture", "Assets",
                "MergedTexture" + ext,
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
