using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GTK
{
    public class TextureChannelMergeWindow : EditorWindow
    {
        private const string WindowTitle = "Texture Channel Merge";
        private const float ChannelRowHeight = 52f;
        private const float PreviewMaxSize = 256f;

        // ─── Mode ──────────────────────────────────────────────────────

        private enum ToolMode { Merge, Swizzle }
        [SerializeField] private ToolMode _mode = ToolMode.Merge;

        // ─── Merge state ───────────────────────────────────────────────

        [SerializeField] private ChannelSource[] _sources = new ChannelSource[4];
        [SerializeField] private bool _processInLinear;

        // ─── Swizzle state ─────────────────────────────────────────────

        [SerializeField] private Texture2D _swizzleSource;
        [SerializeField] private int[] _swizzleMap = { 0, 1, 2, 3 };

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

        private static readonly string[] ChannelLabels = { "R", "G", "B", "A" };
        private static readonly string[] SourceChannelOptions = { "R", "G", "B", "A" };
        private static readonly float[] DefaultColors = { 0.0f, 0.0f, 0.0f, 1.0f };

        private static readonly string[] FormatLabels = { "PNG", "JPG", "TGA" };
        private static readonly SaveFormat[] FormatValues = { SaveFormat.PNG, SaveFormat.JPG, SaveFormat.TGA };

        private static readonly string[] SwizzlePresetLabels = { "Swap RG", "Alpha\u2192Red", "Red\u2192Alpha", "Identity" };
        private static readonly int[][] SwizzlePresets =
        {
            new[] { 1, 0, 2, 3 }, // Swap RG
            new[] { 3, 1, 2, 3 }, // Alpha→Red (A copied to R, A stays)
            new[] { 0, 1, 2, 0 }, // Red→Alpha
            new[] { 0, 1, 2, 3 }, // Identity
        };

        // ─── Window lifecycle ──────────────────────────────────────────

        [MenuItem("Tools/GTK/Texture Channel Merge")]
        private static void ShowWindow()
        {
            var w = GetWindow<TextureChannelMergeWindow>(false, WindowTitle);
            w.minSize = new Vector2(520, 480);
            w.Show();
        }

        private void OnEnable()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_sources[i].sourceChannel == 0 && _sources[i].defaultColor == 0 && i < DefaultColors.Length)
                    _sources[i].defaultColor = DefaultColors[i];
            }
            _processInLinear = TextureChannelMergeUtility.IsProjectLinear();
            AppendLog("Ready.");
        }

        private void OnDisable()
        {
            DestroyPreview();
        }

        // ─── Main GUI ──────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawModeToggle();
            EditorGUILayout.Space(4);

            switch (_mode)
            {
                case ToolMode.Merge:
                    DrawChannelMapping();
                    break;
                case ToolMode.Swizzle:
                    DrawSwizzleSection();
                    break;
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

        // ─── Merge: Channel Mapping ────────────────────────────────────

        private void DrawChannelMapping()
        {
            EditorGUILayout.LabelField("Source Channels", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            for (int i = 0; i < 4; i++)
            {
                var rect = EditorGUILayout.GetControlRect(false, ChannelRowHeight);
                rect = EditorGUI.IndentedRect(rect);
                DrawChannelRow(rect, i);
            }
        }

        private void DrawChannelRow(Rect rect, int index)
        {
            float labelW = 20;
            float texW = 110;
            float chanW = 56;
            float spacing = 4;
            float sliderLeft = labelW + texW + spacing + chanW + spacing * 2;

            // Channel label
            var labelRect = new Rect(rect.x, rect.y + (rect.height - EditorGUIUtility.singleLineHeight) * 0.5f, labelW, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, ChannelLabels[index], EditorStyles.boldLabel);

            // Texture field (tall enough for mini preview)
            var texRect = new Rect(labelRect.xMax + spacing, rect.y + 2, texW, rect.height - 4);
            _sources[index].texture = (Texture2D)EditorGUI.ObjectField(texRect, _sources[index].texture, typeof(Texture2D), false);

            // Source channel dropdown
            EditorGUI.BeginDisabledGroup(_sources[index].texture == null);
            var chanRect = new Rect(texRect.xMax + spacing, rect.y + (rect.height - EditorGUIUtility.singleLineHeight) * 0.5f, chanW, EditorGUIUtility.singleLineHeight);
            _sources[index].sourceChannel = EditorGUI.Popup(chanRect, _sources[index].sourceChannel, SourceChannelOptions);
            EditorGUI.EndDisabledGroup();

            // Default value slider (when no texture)
            EditorGUI.BeginDisabledGroup(_sources[index].texture != null);
            float sliderW = rect.xMax - sliderLeft - spacing;
            var sliderRect = new Rect(sliderLeft, rect.y + (rect.height - EditorGUIUtility.singleLineHeight) * 0.5f, sliderW, EditorGUIUtility.singleLineHeight);
            float labelOffset = 8;
            var defaultLabelRect = new Rect(sliderRect.x, sliderRect.y, labelOffset, sliderRect.height);
            EditorGUI.LabelField(defaultLabelRect, "", EditorStyles.miniLabel);
            float v = EditorGUI.Slider(sliderRect, _sources[index].defaultColor, 0f, 1f);
            if (_sources[index].texture == null)
                _sources[index].defaultColor = v;
            EditorGUI.EndDisabledGroup();
        }

        // ─── Swizzle Section ───────────────────────────────────────────

        private void DrawSwizzleSection()
        {
            EditorGUILayout.LabelField("Source Texture", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _swizzleSource = (Texture2D)EditorGUILayout.ObjectField(
                _swizzleSource, typeof(Texture2D), false,
                GUILayout.Height(EditorGUIUtility.singleLineHeight * 2));

            if (_swizzleSource == null)
            {
                EditorGUILayout.HelpBox("Select a source texture to swizzle.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Channel Mapping", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Table header
            var headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float colW = (headerRect.width - 60) / 4f;
            float xOff = headerRect.x + 60;
            EditorGUI.LabelField(new Rect(headerRect.x, headerRect.y, 56, headerRect.height), "Output", EditorStyles.boldLabel);
            for (int c = 0; c < 4; c++)
                EditorGUI.LabelField(new Rect(xOff + c * colW, headerRect.y, colW, headerRect.height), ChannelLabels[c], EditorStyles.boldLabel);

            // 4 rows of radio-style popups
            for (int row = 0; row < 4; row++)
            {
                var rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2);
                EditorGUI.LabelField(new Rect(rowRect.x, rowRect.y, 56, rowRect.height),
                    ChannelLabels[row], EditorStyles.boldLabel);

                for (int c = 0; c < 4; c++)
                {
                    var cellRect = new Rect(xOff + c * colW, rowRect.y, colW, rowRect.height);
                    bool selected = _swizzleMap[row] == c;
                    bool newSelected = EditorGUI.Toggle(cellRect, selected);
                    if (newSelected && !selected)
                        _swizzleMap[row] = c;
                }
            }

            EditorGUILayout.Space(4);

            // Preset buttons
            EditorGUILayout.LabelField("Presets", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int p = 0; p < SwizzlePresetLabels.Length; p++)
            {
                if (GUILayout.Button(SwizzlePresetLabels[p], EditorStyles.miniButton))
                {
                    for (int c = 0; c < 4; c++)
                        _swizzleMap[c] = SwizzlePresets[p][c];
                    AppendLog($"Applied swizzle preset: {SwizzlePresetLabels[p]}");
                    _previewValid = false;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─── Settings ──────────────────────────────────────────────────

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Linear toggle
            _processInLinear = EditorGUILayout.ToggleLeft(
                new GUIContent("Process in Linear Space",
                    "sRGB inputs convert to linear, output converts back to gamma.\nAuto-defaults to match project color space."),
                _processInLinear);

            // Format + Quality row
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel("Format");
            int fmtIndex = System.Array.IndexOf(FormatValues, _saveFormat);
            if (fmtIndex < 0) fmtIndex = 0;
            fmtIndex = EditorGUILayout.Popup(fmtIndex, FormatLabels, GUILayout.Width(80));
            _saveFormat = FormatValues[fmtIndex];

            if (_saveFormat == SaveFormat.JPG)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.PrefixLabel("Quality");
                _jpgQuality = EditorGUILayout.IntSlider(_jpgQuality, 1, 100, GUILayout.Width(160));
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─── Preview ───────────────────────────────────────────────────

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();

            // Preview thumbnail
            float previewSize = Mathf.Min(position.width * 0.38f, PreviewMaxSize);
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

            // Info text
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

            // Refresh button
            if (GUILayout.Button(_previewValid ? "Refresh Preview" : "Generate Preview",
                GUILayout.Height(24)))
            {
                GeneratePreview();
            }

            // Warning if stale
            if (!_previewValid && _previewTex != null)
            {
                EditorGUILayout.LabelField("Inputs changed — preview outdated.",
                    EditorStyles.miniLabel);
            }

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
                        if (!ValidateMergeInputs())
                        {
                            AppendLog("Preview cancelled: missing source textures.");
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
                            _swizzleSource, _swizzleMap, _processInLinear);
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

        // ─── Save ──────────────────────────────────────────────────────

        private void DrawSaveSection()
        {
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("File Name");

            string ext = TextureChannelMergeUtility.GetExtension(_saveFormat);
            string displayName = EditorGUILayout.TextField(_fileName + ext);
            if (displayName.EndsWith(ext))
                _fileName = displayName.Substring(0, displayName.Length - ext.Length);
            else
                _fileName = displayName;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Merge && Save", GUILayout.Height(30)))
            {
                ExecuteSave();
            }
        }

        private void ExecuteSave()
        {
            // Use current preview if valid, otherwise regenerate on save
            if (!_previewValid)
                GeneratePreview();

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

        // ─── Validation ────────────────────────────────────────────────

        private bool ValidateMergeInputs()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_sources[i].texture != null)
                    return true;
            }
            return false;
        }

        // ─── Log ───────────────────────────────────────────────────────

        private void DrawLogSection()
        {
            _logExpanded = EditorGUILayout.Foldout(_logExpanded, "Log", true, EditorStyles.foldout);
            if (_logExpanded)
            {
                _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(80));

                string fullLog = string.Join("\n", _logEntries);
                EditorGUILayout.TextArea(fullLog, GUILayout.ExpandHeight(true));

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    _logEntries.Clear();
                    Repaint();
                }
            }
            else
            {
                // Single-line status
                if (_logEntries.Count > 0)
                {
                    var statusRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    statusRect = EditorGUI.IndentedRect(statusRect);
                    EditorGUI.HelpBox(statusRect, _logEntries[_logEntries.Count - 1], MessageType.Info);
                }
            }
        }

        private void AppendLog(string message)
        {
            _logEntries.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            _logScroll.y = float.MaxValue; // auto-scroll to bottom
            Repaint();
        }
    }
}
