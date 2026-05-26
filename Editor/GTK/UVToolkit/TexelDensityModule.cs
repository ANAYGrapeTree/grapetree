using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace GTK.UVToolkit
{
    public class TexelDensityModule
    {
        private GameObject _target;
        private Mesh _mesh;
        private MeshFilter _mf;
        private SkinnedMeshRenderer _smr;
        private bool _hasAnalyzed;

        private float _targetDensity = 10.24f;
        private int _texSize = 1024;
        private bool _useWorldScale = true;

        private Dictionary<int, float> _triDensities;
        private float _minDensity, _maxDensity, _avgDensity, _medianDensity;
        private Mesh _heatMesh;
        private Material _heatMat;

        public float AverageDensity => _avgDensity;
        public float MinDensity => _minDensity;
        public float MaxDensity => _maxDensity;
        public int TriCount => _triDensities?.Count ?? 0;

        public void SetTargetMesh(Mesh mesh) { _mesh = mesh; _hasAnalyzed = false; _heatMesh = null; }

        public void SetTarget(GameObject obj)
        {
            if (obj == _target) return;
            _target = obj; _hasAnalyzed = false; _heatMesh = null;
            if (obj == null) { _mf = null; _smr = null; _mesh = null; return; }
            _mf = obj.GetComponent<MeshFilter>();
            _smr = obj.GetComponent<SkinnedMeshRenderer>();
            _mesh = _mf != null ? _mf.sharedMesh : _smr != null ? _smr.sharedMesh : null;
        }

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Texel Density", EditorStyles.boldLabel);

            if (_target == null) { EditorGUILayout.HelpBox("Select a GameObject with a mesh.", MessageType.Info); return; }
            if (_mesh == null) { EditorGUILayout.HelpBox("No valid mesh.", MessageType.Warning); return; }
            if (!_mesh.isReadable) { EditorGUILayout.HelpBox("Mesh not readable.", MessageType.Error); return; }
            if (_mesh.uv == null || _mesh.uv.Length == 0) { EditorGUILayout.HelpBox("No UV data.", MessageType.Warning); return; }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _targetDensity = EditorGUILayout.FloatField("Target Density (px/cm)", _targetDensity);
            _texSize = EditorGUILayout.IntPopup("Texture Size", _texSize,
                new[] { "256", "512", "1024", "2048", "4096" }, new[] { 256, 512, 1024, 2048, 4096 });
            _useWorldScale = EditorGUILayout.Toggle("Use World Scale", _useWorldScale);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Analyze", GUILayout.Height(28)))
                Analyze(_texSize, _useWorldScale);

            if (_hasAnalyzed)
            {
                DrawStats();
                DrawDistribution();
                DrawUnify();
                DrawHeatmapToggle();
            }
        }

        public void Analyze(int texSize, bool useWorldScale)
        {
            if (_mesh == null) return;
            var uv = _mesh.uv;
            var tris = _mesh.triangles;
            var verts = _mesh.vertices;
            var l2w = _target != null ? _target.transform.localToWorldMatrix : Matrix4x4.identity;

            int triCount = tris.Length / 3;
            _triDensities = new Dictionary<int, float>();
            float total = 0;
            _minDensity = float.MaxValue;
            _maxDensity = 0;

            for (int t = 0; t < triCount; t++)
            {
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];

                var v0 = useWorldScale ? l2w.MultiplyPoint3x4(verts[i0]) : verts[i0];
                var v1 = useWorldScale ? l2w.MultiplyPoint3x4(verts[i1]) : verts[i1];
                var v2 = useWorldScale ? l2w.MultiplyPoint3x4(verts[i2]) : verts[i2];

                float area3D = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                if (area3D < 0.000001f) area3D = 0.000001f;

                var uv0 = uv[i0]; var uv1 = uv[i1]; var uv2 = uv[i2];
                float areaUV = Mathf.Abs(
                    (uv1.x - uv0.x) * (uv2.y - uv0.y) - (uv2.x - uv0.x) * (uv1.y - uv0.y)) * 0.5f;
                if (areaUV < 0.000001f) areaUV = 0.000001f;

                float td = texSize * Mathf.Sqrt(areaUV / area3D);
                _triDensities[t] = td;
                total += td;
                if (td < _minDensity) _minDensity = td;
                if (td > _maxDensity) _maxDensity = td;
            }

            _avgDensity = total / triCount;
            var sorted = _triDensities.Values.OrderBy(v => v).ToArray();
            _medianDensity = sorted[triCount / 2];
            _hasAnalyzed = true;
            Debug.Log($"TD analysis: avg={_avgDensity:F2} med={_medianDensity:F2} range=[{_minDensity:F2}-{_maxDensity:F2}]");
        }

        private void DrawStats()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Average: {_avgDensity:F2} px/cm");
            EditorGUILayout.LabelField($"Median:  {_medianDensity:F2} px/cm");
            EditorGUILayout.LabelField($"Min:     {_minDensity:F2} px/cm");
            EditorGUILayout.LabelField($"Max:     {_maxDensity:F2} px/cm");

            float dev = (_maxDensity - _minDensity) / Mathf.Max(_avgDensity, 0.01f) * 100;
            EditorGUILayout.LabelField($"Deviation: {dev:F1}%");
            EditorGUILayout.LabelField($"Rating: {dev switch { < 20 => "Excellent", < 50 => "Good", < 100 => "Fair", _ => "Poor" }}");
            EditorGUILayout.EndVertical();
        }

        private void DrawDistribution()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Distribution", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int bins = 10;
            float range = Mathf.Max(_maxDensity - _minDensity, 0.01f);
            var binCounts = new int[bins];
            foreach (var td in _triDensities.Values)
            {
                int b = Mathf.Clamp(Mathf.FloorToInt((td - _minDensity) / range * bins), 0, bins - 1);
                binCounts[b]++;
            }

            int maxBin = Mathf.Max(1, binCounts.Max());
            for (int i = 0; i < bins; i++)
            {
                float start = _minDensity + range * i / bins;
                float end = _minDensity + range * (i + 1) / bins;
                int barLen = Mathf.RoundToInt((float)binCounts[i] / maxBin * 10);
                EditorGUILayout.LabelField($"  {start:F1}-{end:F1}: {new string('█', barLen)} {binCounts[i]}");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawUnify()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Unify", EditorStyles.boldLabel);
            float suggested = Mathf.Round(_avgDensity * 100) / 100f;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Suggested: {suggested:F2} px/cm");
            _targetDensity = EditorGUILayout.FloatField(_targetDensity, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Unify Texel Density", GUILayout.Height(26)))
                Unify(_targetDensity);
        }

        private void Unify(float target)
        {
            if (!_hasAnalyzed || _mesh == null) return;
            var uv = _mesh.uv;
            var tris = _mesh.triangles;
            var newUV = new Vector2[uv.Length];
            System.Array.Copy(uv, newUV, uv.Length);

            var accum = new Vector2[uv.Length];
            var counts = new int[uv.Length];

            foreach (var kvp in _triDensities)
            {
                int t = kvp.Key;
                float current = kvp.Value;
                if (Mathf.Abs(current - target) / target < 0.05f) continue;

                float scale = current / target;
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                var center = (newUV[i0] + newUV[i1] + newUV[i2]) / 3f;

                foreach (int idx in new[] { i0, i1, i2 })
                {
                    var offset = newUV[idx] - center;
                    accum[idx] += center + offset / scale - newUV[idx];
                    counts[idx]++;
                }
            }

            for (int i = 0; i < newUV.Length; i++)
                if (counts[i] > 0) newUV[i] += accum[i] / counts[i];

            var newMesh = Object.Instantiate(_mesh);
            newMesh.uv = newUV;
            newMesh.name = _mesh.name + "_UnifiedTD";

            Undo.RecordObject(_target, "Unify TD");
            if (_mf != null) _mf.sharedMesh = newMesh;
            else if (_smr != null) _smr.sharedMesh = newMesh;

            _mesh = newMesh;
            Analyze(_texSize, _useWorldScale);
        }

        private void DrawHeatmapToggle()
        {
            EditorGUILayout.Space(2);
            bool show = EditorGUILayout.Toggle("Show Heatmap (Scene)", _heatMesh != null);
            if (show && _heatMesh == null) BuildHeatmap();
            else if (!show && _heatMesh != null) { Object.DestroyImmediate(_heatMesh); _heatMesh = null; }
        }

        private void BuildHeatmap()
        {
            if (_mesh == null || !_hasAnalyzed) return;
            var verts = _mesh.vertices;
            var tris = _mesh.triangles;
            var allVerts = new List<Vector3>();
            var allTris = new List<int>();
            var allColors = new List<Color>();

            float range = Mathf.Max(_maxDensity - _minDensity, 0.01f);
            for (int t = 0; t < tris.Length / 3; t++)
            {
                int idx = allVerts.Count;
                float td = _triDensities.ContainsKey(t) ? _triDensities[t] : _avgDensity;
                float norm = (td - _minDensity) / range;
                var c = UVToolkitUtility.EvaluateHeatColor(norm);

                for (int i = 0; i < 3; i++)
                { allVerts.Add(verts[tris[t * 3 + i]]); allColors.Add(c); }
                allTris.Add(idx); allTris.Add(idx + 1); allTris.Add(idx + 2);
            }

            _heatMesh = new Mesh();
            _heatMesh.SetVertices(allVerts);
            _heatMesh.SetTriangles(allTris, 0);
            _heatMesh.SetColors(allColors);
            _heatMesh.RecalculateBounds();
            _heatMesh.RecalculateNormals();
        }

        public void DrawSceneOverlay()
        {
            if (_heatMesh == null || !_hasAnalyzed || _target == null) return;
            if (Event.current.type != EventType.Repaint) return;

            if (_heatMat == null)
            {
                _heatMat = new Material(Shader.Find("Hidden/Internal-Colored"))
                    { hideFlags = HideFlags.HideAndDontSave };
                _heatMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _heatMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _heatMat.SetInt("_ZWrite", 0);
            }
            _heatMat.SetPass(0);

            GL.PushMatrix();
            GL.MultMatrix(_target.transform.localToWorldMatrix);
            Graphics.DrawMeshNow(_heatMesh, Matrix4x4.identity);
            GL.PopMatrix();
        }
    }
}
