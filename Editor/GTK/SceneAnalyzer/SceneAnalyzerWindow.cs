using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GTK.SceneAnalyzer
{
    public class SceneAnalyzerWindow : EditorWindow
    {
        private enum Metric { Triangles, Vertices, Materials, TextureMemory }
        [SerializeField] private Metric _metric = Metric.Triangles;
        [SerializeField] private bool _showHeatmap = true;
        [SerializeField] private float _warningThreshold = 0.7f;

        private List<SceneObjectStats> _stats;
        private Vector2 _scrollPos;
        private bool _hasAnalyzed;

        private readonly string[] _metricLabels = { "Triangles", "Vertices", "Materials", "Texture Memory" };

        [MenuItem("Tools/GTK/Scene Analyzer")]
        private static void ShowWindow()
        {
            var w = GetWindow<SceneAnalyzerWindow>(false, "Scene Analyzer");
            w.minSize = new Vector2(400, 400);
            w.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Analyzer", EditorStyles.largeLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            _metric = (Metric)GUILayout.Toolbar((int)_metric, _metricLabels, GUILayout.Height(22));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Analyze Scene", GUILayout.Height(28)))
            {
                _stats = SceneAnalyzerUtility.AnalyzeScene();
                _hasAnalyzed = true;
                SceneView.RepaintAll();
            }

            if (!_hasAnalyzed)
            {
                EditorGUILayout.HelpBox("Click 'Analyze Scene' to scan all renderers in the scene.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Found {_stats.Count} renderers", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _showHeatmap = EditorGUILayout.Toggle("Scene Heatmap Overlay", _showHeatmap);
            _warningThreshold = EditorGUILayout.Slider("Warning Threshold", _warningThreshold, 0.5f, 0.95f);

            EditorGUILayout.Space(4);
            DrawStatsTable();
        }

        private float GetMetricValue(SceneObjectStats s)
        {
            return _metric switch
            {
                Metric.Triangles => s.triangleCount,
                Metric.Vertices => s.vertexCount,
                Metric.Materials => s.materialCount,
                Metric.TextureMemory => s.textureMemoryBytes,
                _ => s.triangleCount
            };
        }

        private string FormatMetric(float val)
        {
            return _metric switch
            {
                Metric.TextureMemory => EditorUtility.FormatBytes((long)val),
                _ => val.ToString("N0")
            };
        }

        private void DrawStatsTable()
        {
            float maxVal = 0f;
            foreach (var s in _stats) maxVal = Mathf.Max(maxVal, GetMetricValue(s));
            if (maxVal <= 0f) maxVal = 1f;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var s in _stats)
            {
                float val = GetMetricValue(s);
                float norm = val / maxVal;
                Color c = SceneAnalyzerUtility.GetHeatColor(norm, 0f, 1f);

                var rect = EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Min(norm, 1f), rect.height), new Color(c.r, c.g, c.b, 0.15f));

                EditorGUILayout.LabelField(s.name, GUILayout.Width(180));
                EditorGUILayout.LabelField(FormatMetric(val), GUILayout.Width(100));

                if (norm > _warningThreshold)
                    EditorGUILayout.LabelField("!", GUILayout.Width(16));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!_showHeatmap || !_hasAnalyzed || _stats == null) return;
            if (Event.current.type != EventType.Repaint) return;

            float maxVal = 0f;
            foreach (var s in _stats) maxVal = Mathf.Max(maxVal, GetMetricValue(s));
            if (maxVal <= 0f) return;

            foreach (var s in _stats)
            {
                if (s.gameObject == null) continue;
                float val = GetMetricValue(s);
                float norm = val / maxVal;
                Color c = SceneAnalyzerUtility.GetHeatColor(norm, 0f, 1f);
                c.a = 0.3f;

                var bounds = CalculateBounds(s.gameObject);
                if (bounds.extents == Vector3.zero) continue;

                Handles.color = c;
                Handles.DrawSolidRectangleWithOutline(
                    GetCornerPoints(bounds), c, Color.white);
            }

            Handles.BeginGUI();
            var r = SceneView.currentDrawingSceneView.camera.pixelRect;
            GUI.Label(new Rect(r.width - 200, 5, 195, 20),
                $"Scene Analyzer: {_stats.Count} objects", EditorStyles.boldLabel);
            Handles.EndGUI();
        }

        private Bounds CalculateBounds(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.one * 0.1f);
            if (go.TryGetComponent<MeshFilter>(out MeshFilter mf) && mf.sharedMesh != null)
                bounds = mf.sharedMesh.bounds;
            else if (go.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr) && smr.sharedMesh != null)
                bounds = smr.sharedMesh.bounds;
            bounds.center = go.transform.TransformPoint(bounds.center);
            bounds.size = Vector3.Scale(bounds.size, go.transform.lossyScale);
            return bounds;
        }

        private Vector3[] GetCornerPoints(Bounds b)
        {
            var c = b.center;
            var e = b.extents;
            return new Vector3[]
            {
                c + new Vector3(-e.x, -e.y, 0), c + new Vector3(e.x, -e.y, 0),
                c + new Vector3(e.x, e.y, 0), c + new Vector3(-e.x, e.y, 0)
            };
        }
    }
}
