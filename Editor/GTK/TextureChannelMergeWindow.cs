using UnityEngine;
using UnityEditor;

namespace GTK
{
    public class TextureChannelMergeWindow : EditorWindow
    {
        private const string WindowTitle = "Texture Channel Merge";

        [SerializeField] private ChannelSource[] _sources = new ChannelSource[4];
        [SerializeField] private bool _processInLinear;
        [SerializeField] private string _fileName = "MergedTexture.png";

        private bool _hasSizeWarning;
        private string _status = "";

        /// <summary> Channel labels for display. </summary>
        private static readonly string[] ChannelLabels = { "R", "G", "B", "A" };
        /// <summary> Options for the source-channel dropdown. </summary>
        private static readonly string[] SourceChannelOptions = { "R", "G", "B", "A" };
        /// <summary> Default color values per channel (used when no texture assigned). </summary>
        private static readonly float[] DefaultColors = { 0.0f, 0.0f, 0.0f, 1.0f };

        [MenuItem("Tools/GTK/Texture Channel Merge")]
        private static void ShowWindow()
        {
            var w = GetWindow<TextureChannelMergeWindow>(false, WindowTitle);
            w.minSize = new Vector2(480, 340);
            w.Show();
        }

        private void OnEnable()
        {
            // Init defaults
            for (int i = 0; i < 4; i++)
            {
                if (_sources[i].sourceChannel == 0 && _sources[i].defaultColor == 0)
                {
                    _sources[i].defaultColor = DefaultColors[i];
                }
            }
            _processInLinear = TextureChannelMergeUtility.IsProjectLinear();
            _hasSizeWarning = false;
            _status = "";
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            DrawChannelMapping();

            EditorGUILayout.Space(8);
            DrawSettings();

            EditorGUILayout.Space(8);
            DrawSaveSection();

            // Status bar
            EditorGUILayout.Space(4);
            var statusRect = EditorGUILayout.GetControlRect(false, 18);
            statusRect = EditorGUI.IndentedRect(statusRect);
            EditorGUI.HelpBox(statusRect, _status,
                _hasSizeWarning ? MessageType.Warning : MessageType.Info);
        }

        private void DrawChannelMapping()
        {
            EditorGUILayout.LabelField("Output Channel Mapping", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            for (int i = 0; i < 4; i++)
            {
                Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2);

                // Channel label
                float labelW = 18;
                Rect labelRect = new Rect(rect.x, rect.y, labelW, rect.height);
                EditorGUI.LabelField(labelRect, ChannelLabels[i], EditorStyles.boldLabel);

                // Texture slot
                float texW = rect.width - labelW - 90 - 8;
                Rect texRect = new Rect(rect.x + labelW + 2, rect.y, texW, rect.height);
                _sources[i].texture = (Texture2D)EditorGUI.ObjectField(texRect, _sources[i].texture, typeof(Texture2D), false);

                // Source channel dropdown (enabled only when texture is assigned)
                float chanW = 54;
                Rect chanRect = new Rect(texRect.xMax + 4, rect.y, chanW, rect.height);
                EditorGUI.BeginDisabledGroup(_sources[i].texture == null);
                _sources[i].sourceChannel = EditorGUI.Popup(chanRect, _sources[i].sourceChannel, SourceChannelOptions);
                EditorGUI.EndDisabledGroup();

                // Default value slider (enabled only when texture is NOT assigned)
                float defW = rect.width - labelW - texW - chanW - 14;
                Rect defRect = new Rect(chanRect.xMax + 4, rect.y, defW, rect.height);
                EditorGUI.BeginDisabledGroup(_sources[i].texture != null);
                float v = EditorGUI.Slider(defRect, _sources[i].defaultColor, 0f, 1f);
                if (_sources[i].texture == null)
                    _sources[i].defaultColor = v;
                EditorGUI.EndDisabledGroup();

                // Label for default slider
                if (_sources[i].texture == null)
                {
                    Rect labelDef = new Rect(defRect.x, defRect.y, 12, defRect.height);
                    EditorGUI.LabelField(labelDef, "", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _processInLinear = EditorGUILayout.ToggleLeft(
                new GUIContent("Process in Linear Space",
                    "When enabled: convert sRGB inputs to linear, output is converted back to gamma for PNG. " +
                    "Auto-toggled based on project color space."),
                _processInLinear);
        }

        private void DrawSaveSection()
        {
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _fileName = EditorGUILayout.TextField("File Name", _fileName);
            if (!_fileName.EndsWith(".png"))
                _fileName = _fileName.TrimEnd('.') + ".png";

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Merge && Save", GUILayout.Height(30)))
            {
                ExecuteMergeAndSave();
            }
        }

        private void ExecuteMergeAndSave()
        {
            _hasSizeWarning = false;
            _status = "";

            // Validate: at least one channel has a texture
            bool hasAnyTexture = false;
            for (int i = 0; i < 4; i++)
            {
                if (_sources[i].texture != null)
                {
                    hasAnyTexture = true;
                    break;
                }
            }
            if (!hasAnyTexture)
            {
                _status = "Assign at least one source texture.";
                return;
            }

            // Check for size differences and find min size
            int minW = int.MaxValue, minH = int.MaxValue;
            int maxW = 0, maxH = 0;
            for (int i = 0; i < 4; i++)
            {
                if (_sources[i].texture != null)
                {
                    int tw = _sources[i].texture.width;
                    int th = _sources[i].texture.height;
                    minW = Mathf.Min(minW, tw);
                    minH = Mathf.Min(minH, th);
                    maxW = Mathf.Max(maxW, tw);
                    maxH = Mathf.Max(maxH, th);
                }
            }

            if (minW != maxW || minH != maxH)
            {
                _hasSizeWarning = true;
                _status = $"Source textures have different sizes. Output will be downscaled to {minW}×{minH}.";
            }

            // Save file dialog
            string savePath = EditorUtility.SaveFilePanel(
                "Save Merged Texture",
                "Assets",
                _fileName,
                "png");

            if (string.IsNullOrEmpty(savePath))
            {
                _status = "Save cancelled.";
                return;
            }

            // Ensure .png extension
            if (!savePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                savePath += ".png";

            EditorUtility.DisplayProgressBar("Texture Channel Merge", "Merging...", 0f);
            try
            {
                TextureChannelMergeUtility.MergeAndSave(_sources, _processInLinear, savePath);
                _status = $"Saved to: {savePath}";
            }
            catch (System.Exception ex)
            {
                _status = $"Error: {ex.Message}";
                Debug.LogError($"Texture Channel Merge failed: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
