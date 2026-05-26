using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace GTK.UVToolkit
{
    public class OverlapDetectionModule
    {
        private GameObject _target;
        private Mesh _mesh;
        private MeshFilter _mf;
        private SkinnedMeshRenderer _smr;

        private float _threshold = 0.001f;
        private List<OverlapResult> _results;
        private bool _hasChecked;
        private int _totalTris;
        private Color _highlightColor = Color.red;

        public int OverlapCount => _results?.Count ?? 0;

        public void SetTargetMesh(Mesh mesh) { _mesh = mesh; _results = null; _hasChecked = false; }

        private struct OverlapResult
        {
            public int triA, triB;
            public float overlapArea;
        }

        public void SetTarget(GameObject obj)
        {
            if (obj == _target) return;
            _target = obj; _results = null; _hasChecked = false;
            if (obj == null) { _mf = null; _smr = null; _mesh = null; return; }
            _mf = obj.GetComponent<MeshFilter>();
            _smr = obj.GetComponent<SkinnedMeshRenderer>();
            _mesh = _mf != null ? _mf.sharedMesh : _smr != null ? _smr.sharedMesh : null;
        }

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("UV Overlap Detection", EditorStyles.boldLabel);

            if (_target == null) { EditorGUILayout.HelpBox("Select a GameObject with a mesh.", MessageType.Info); return; }
            if (_mesh == null) { EditorGUILayout.HelpBox("No valid mesh found.", MessageType.Warning); return; }
            if (!_mesh.isReadable) { EditorGUILayout.HelpBox("Mesh not readable. Enable Read/Write in import settings.", MessageType.Error); return; }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Mesh Info", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"  Vertices: {_mesh.vertexCount}");
            EditorGUILayout.LabelField($"  Triangles: {_mesh.triangles.Length / 3}");
            EditorGUILayout.LabelField($"  UV: {(_mesh.uv?.Length > 0 ? "✓" : "✗")}");
            EditorGUILayout.EndVertical();

            _threshold = EditorGUILayout.Slider("Overlap Threshold", _threshold, 0.0001f, 0.01f);

            if (GUILayout.Button("Detect Overlaps", GUILayout.Height(28)))
                DetectOverlaps(_threshold);

            if (_hasChecked) DrawResults();
        }

        public void DetectOverlaps(float threshold)
        {
            if (_mesh == null || _mesh.uv == null || _mesh.uv.Length == 0) return;

            var uv = _mesh.uv;
            var tris = _mesh.triangles;
            _totalTris = tris.Length / 3;
            _results = new List<OverlapResult>();

            float gridSize = 0.1f;
            var grid = new Dictionary<Vector2Int, List<int>>();

            for (int t = 0; t < _totalTris; t++)
            {
                var u0 = uv[tris[t * 3]];
                var u1 = uv[tris[t * 3 + 1]];
                var u2 = uv[tris[t * 3 + 2]];

                float minU = Mathf.Min(u0.x, u1.x, u2.x);
                float maxU = Mathf.Max(u0.x, u1.x, u2.x);
                float minV = Mathf.Min(u0.y, u1.y, u2.y);
                float maxV = Mathf.Max(u0.y, u1.y, u2.y);

                int gxMin = Mathf.FloorToInt(minU / gridSize);
                int gxMax = Mathf.FloorToInt(maxU / gridSize);
                int gyMin = Mathf.FloorToInt(minV / gridSize);
                int gyMax = Mathf.FloorToInt(maxV / gridSize);

                for (int gx = gxMin; gx <= gxMax; gx++)
                    for (int gy = gyMin; gy <= gyMax; gy++)
                    {
                        var key = new Vector2Int(gx, gy);
                        if (!grid.ContainsKey(key)) grid[key] = new List<int>();
                        grid[key].Add(t);
                    }
            }

            var checkedPairs = new HashSet<(int, int)>();
            foreach (var cell in grid.Values)
            {
                if (cell.Count < 2) continue;
                for (int i = 0; i < cell.Count; i++)
                    for (int j = i + 1; j < cell.Count; j++)
                    {
                        int a = cell[i], b = cell[j];
                        if (a == b) continue;
                        var pair = a < b ? (a, b) : (b, a);
                        if (!checkedPairs.Add(pair)) continue;

                        var uvA = new[] { uv[tris[a * 3]], uv[tris[a * 3 + 1]], uv[tris[a * 3 + 2]] };
                        var uvB = new[] { uv[tris[b * 3]], uv[tris[b * 3 + 1]], uv[tris[b * 3 + 2]] };

                        float area = UVToolkitUtility.SutherlandHodgmanArea(uvA, uvB);
                        if (area > threshold)
                            _results.Add(new OverlapResult { triA = a, triB = b, overlapArea = area });
                    }
            }

            _hasChecked = true;
            Debug.Log($"Overlap detection complete: {_results.Count} overlaps in {_totalTris} triangles");
            SceneView.RepaintAll();
        }

        private void DrawResults()
        {
            EditorGUILayout.Space(4);
            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("No UV overlaps detected.", MessageType.Info);
                return;
            }

            float ratio = (float)_results.Count / _totalTris * 100f;
            EditorGUILayout.LabelField($"Found {_results.Count} overlaps in {_totalTris} triangles ({ratio:F1}%)");

            for (int i = 0; i < Mathf.Min(10, _results.Count); i++)
                EditorGUILayout.LabelField($"  #{i + 1}: Tri {_results[i].triA} ↔ {_results[i].triB}  area={_results[i].overlapArea:F6}");

            if (_results.Count > 10)
                EditorGUILayout.LabelField($"  ... and {_results.Count - 10} more");

            if (GUILayout.Button("Copy Report to Clipboard", GUILayout.Height(22)))
            {
                var report = $"UV Overlap Report - {_target?.name}\n{_results.Count} overlaps / {_totalTris} triangles\n\n" +
                    string.Join("\n", _results.Select((r, i) => $"{i + 1}. Tri{r.triA}↔{r.triB} area={r.overlapArea:F6}"));
                EditorGUIUtility.systemCopyBuffer = report;
            }

            _highlightColor = EditorGUILayout.ColorField("Highlight Color", _highlightColor);
        }

        public void DrawSceneOverlay()
        {
            if (!_hasChecked || _results == null || _results.Count == 0 || _mesh == null || _target == null)
                return;
            if (Event.current.type != EventType.Repaint) return;

            var verts = _mesh.vertices;
            var tris = _mesh.triangles;
            var overlapTris = new HashSet<int>();
            foreach (var r in _results) { overlapTris.Add(r.triA); overlapTris.Add(r.triB); }

            Handles.color = _highlightColor;
            foreach (int t in overlapTris)
            {
                var v0 = _target.transform.TransformPoint(verts[tris[t * 3]]);
                var v1 = _target.transform.TransformPoint(verts[tris[t * 3 + 1]]);
                var v2 = _target.transform.TransformPoint(verts[tris[t * 3 + 2]]);
                Handles.DrawLine(v0, v1);
                Handles.DrawLine(v1, v2);
                Handles.DrawLine(v2, v0);
            }
        }
    }
}
