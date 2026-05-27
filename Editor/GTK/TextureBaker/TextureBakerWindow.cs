using UnityEngine;
using UnityEditor;

namespace GTK.TextureBaker
{
    public class TextureBakerWindow : EditorWindow
    {
        private enum ToolTab { NormalOps, AO, Convert }

        [SerializeField] private ToolTab _tab;

        // Height→Normal
        [SerializeField] private Texture2D _heightSource;
        [SerializeField] private float _normalStrength = 4f;

        // Normal→Height
        [SerializeField] private Texture2D _normalToHeightSrc;

        // AO
        [SerializeField] private Texture2D _aoNormalSrc;
        [SerializeField] private float _aoStrength = 1f;
        [SerializeField] private Texture2D _aoDilateSrc;
        [SerializeField] private int _aoDilateIterations = 3;

        // Format
        [SerializeField] private Texture2D _convertSource;
        [SerializeField] private bool _convertToDX = true;

        // Preview
        private Texture2D _previewTex;

        [MenuItem("Tools/GTK/Texture Baker")]
        private static void ShowWindow()
        {
            var w = GetWindow<TextureBakerWindow>(false, "Texture Baker");
            w.minSize = new Vector2(400, 500);
            w.Show();
        }

        private void OnDisable()
        {
            if (_previewTex != null) { Object.DestroyImmediate(_previewTex); _previewTex = null; }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Texture Baker", EditorStyles.largeLabel);
            EditorGUILayout.Space(4);

            _tab = (ToolTab)GUILayout.Toolbar((int)_tab, new[] { "Normal Ops", "AO", "Convert" }, GUILayout.Height(22));
            EditorGUILayout.Space(6);

            switch (_tab)
            {
                case ToolTab.NormalOps: DrawNormalTab(); break;
                case ToolTab.AO: DrawAOTab(); break;
                case ToolTab.Convert: DrawConvertTab(); break;
            }
        }

        private void DrawNormalTab()
        {
            EditorGUILayout.LabelField("Height → Normal", EditorStyles.boldLabel);
            _heightSource = (Texture2D)EditorGUILayout.ObjectField("Heightmap", _heightSource, typeof(Texture2D), false);
            _normalStrength = EditorGUILayout.Slider("Strength", _normalStrength, 0.1f, 20f);
            if (GUILayout.Button("Generate Normal", GUILayout.Height(24)) && _heightSource != null)
                Execute(() => TextureBakerUtility.HeightToNormal(_heightSource, _normalStrength));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Normal → Height", EditorStyles.boldLabel);
            _normalToHeightSrc = (Texture2D)EditorGUILayout.ObjectField("Normal Map", _normalToHeightSrc, typeof(Texture2D), false);
            if (GUILayout.Button("Extract Height", GUILayout.Height(24)) && _normalToHeightSrc != null)
                Execute(() => TextureBakerUtility.NormalToHeight(_normalToHeightSrc));
        }

        private void DrawAOTab()
        {
            EditorGUILayout.LabelField("AO from Normal", EditorStyles.boldLabel);
            _aoNormalSrc = (Texture2D)EditorGUILayout.ObjectField("Normal Map", _aoNormalSrc, typeof(Texture2D), false);
            _aoStrength = EditorGUILayout.Slider("Strength", _aoStrength, 0f, 3f);
            if (GUILayout.Button("Generate AO", GUILayout.Height(24)) && _aoNormalSrc != null)
                Execute(() => TextureBakerUtility.AOFromNormal(_aoNormalSrc, _aoStrength));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("AO Dilate", EditorStyles.boldLabel);
            _aoDilateSrc = (Texture2D)EditorGUILayout.ObjectField("AO Map", _aoDilateSrc, typeof(Texture2D), false);
            _aoDilateIterations = EditorGUILayout.IntSlider("Iterations", _aoDilateIterations, 1, 20);
            if (GUILayout.Button("Dilate AO", GUILayout.Height(24)) && _aoDilateSrc != null)
                Execute(() => TextureBakerUtility.AODilate(_aoDilateSrc, _aoDilateIterations));
        }

        private void DrawConvertTab()
        {
            EditorGUILayout.LabelField("Normal Format", EditorStyles.boldLabel);
            _convertSource = (Texture2D)EditorGUILayout.ObjectField("Normal Map", _convertSource, typeof(Texture2D), false);
            _convertToDX = EditorGUILayout.Toggle("Convert to DirectX (Y-down)", _convertToDX);
            string label = _convertToDX ? "Convert GL → DX" : "Convert DX → GL";
            if (GUILayout.Button(label, GUILayout.Height(24)) && _convertSource != null)
                Execute(() => TextureBakerUtility.ConvertNormalFormat(_convertSource, _convertToDX));
        }

        private void Execute(Func<Texture2D> action)
        {
            EditorUtility.DisplayProgressBar("Texture Baker", "Processing...", 0.5f);
            try
            {
                if (_previewTex != null) { Object.DestroyImmediate(_previewTex); _previewTex = null; }
                _previewTex = action();
                Debug.Log($"Texture Baker: {_previewTex.width}x{_previewTex.height} result ready.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Texture Baker failed: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
