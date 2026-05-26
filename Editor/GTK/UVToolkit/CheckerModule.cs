using UnityEngine;
using UnityEditor;
using System.IO;

namespace GTK.UVToolkit
{
    public class CheckerModule
    {
        private GameObject _target;
        private Renderer _renderer;
        private Material[] _originalMaterials;
        private Texture2D _checkerTex;
        private bool _isPreviewing;

        private int _gridSize = 8;
        private int _texSize = 1024;
        private Color _colorA = Color.white;
        private Color _colorB = Color.black;

        public void SetTarget(GameObject obj)
        {
            if (obj == _target) return;
            ExitPreview();
            _target = obj;
            _renderer = obj != null ? obj.GetComponent<Renderer>() : null;
        }

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Checker Preview", EditorStyles.boldLabel);

            if (_target == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with a Renderer.", MessageType.Info);
                return;
            }
            if (_renderer == null)
            {
                EditorGUILayout.HelpBox("No Renderer component found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _gridSize = EditorGUILayout.IntSlider("Grid Density", _gridSize, 2, 64);
            _texSize = EditorGUILayout.IntPopup("Texture Size", _texSize,
                new[] { "256", "512", "1024", "2048" }, new[] { 256, 512, 1024, 2048 });
            _colorA = EditorGUILayout.ColorField("Color A", _colorA);
            _colorB = EditorGUILayout.ColorField("Color B", _colorB);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPreviewing ? "Preview Active" : "Apply Checker", GUILayout.Height(28)))
                if (!_isPreviewing) ApplyChecker();
            if (_isPreviewing && GUILayout.Button("Exit Preview", GUILayout.Height(28)))
                ExitPreview();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Save Checker to Assets", GUILayout.Height(24)))
                SaveChecker();

            if (_isPreviewing)
                EditorGUILayout.HelpBox("Checker applied. Exit preview to restore original materials.", MessageType.Info);
        }

        public void ApplyChecker()
        {
            if (_renderer == null) return;
            _originalMaterials = _renderer.sharedMaterials;
            if (_checkerTex != null) Object.DestroyImmediate(_checkerTex);
            _checkerTex = GenerateCheckerTexture(_texSize, _gridSize, _colorA, _colorB);

            var mats = new Material[_originalMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { mainTexture = _checkerTex, hideFlags = HideFlags.DontSave };
            }
            _renderer.sharedMaterials = mats;
            _isPreviewing = true;
            SceneView.RepaintAll();
        }

        public void ExitPreview()
        {
            if (!_isPreviewing || _renderer == null) return;
            if (_originalMaterials != null) _renderer.sharedMaterials = _originalMaterials;
            if (_checkerTex != null) { Object.DestroyImmediate(_checkerTex); _checkerTex = null; }
            _originalMaterials = null;
            _isPreviewing = false;
            SceneView.RepaintAll();
        }

        public static Texture2D GenerateCheckerTexture(int texSize, int gridSize, Color c1, Color c2)
        {
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;

            int cellSize = Mathf.Max(texSize / gridSize, 1);
            var pixels = new Color[texSize * texSize];
            for (int y = 0; y < texSize; y++)
                for (int x = 0; x < texSize; x++)
                {
                    int cx = x / cellSize, cy = y / cellSize;
                    pixels[y * texSize + x] = ((cx + cy) & 1) == 0 ? c1 : c2;
                }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void SaveChecker()
        {
            if (_checkerTex == null) _checkerTex = GenerateCheckerTexture(_texSize, _gridSize, _colorA, _colorB);
            string path = EditorUtility.SaveFilePanelInProject("Save Checker", "CheckerTexture", "png", "");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllBytes(path, _checkerTex.EncodeToPNG());
            AssetDatabase.Refresh();

            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Point;
                imp.SaveAndReimport();
            }
        }
    }
}
