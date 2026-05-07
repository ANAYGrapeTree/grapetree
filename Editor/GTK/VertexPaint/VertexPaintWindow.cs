using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GTK.VertexPaint
{
    public enum PaintChannel
    {
        RGBA,
        R,
        G,
        B,
        A,
        Smooth
    }

    public class VertexPaintWindow : EditorWindow
    {
        [MenuItem("Tools/GTK/Vertex Paint")]
        private static void ShowWindow()
        {
            var w = GetWindow<VertexPaintWindow>(false, "Vertex Paint");
            w.minSize = new Vector2(320, 300);
            w.Show();
        }

        // ─── Brush state ────────────────────────────────────────────────
        [SerializeField] private PaintChannel _paintChannel = PaintChannel.RGBA;
        [SerializeField] private Color _brushColor = new Color(1f, 0f, 0f, 0f);
        [SerializeField] private float _brushSize = 0.25f;
        [SerializeField] private float _brushFalloff = 0.5f;
        [SerializeField] private float _brushOpacity = 1f;
        [SerializeField] private float _channelValue = 1f;

        // ─── Painting state ─────────────────────────────────────────────
        private bool _isPainting;
        private bool _isEditing;
        private bool _isPreview;
        private bool _recordUndo;

        // ─── Mesh references ────────────────────────────────────────────
        private Mesh _sourceMesh;      // original asset mesh (read-only)
        private Mesh _workingMesh;     // instance copy (painted on)
        private bool _hasUnsavedChanges;

        // ─── Preview ────────────────────────────────────────────────────
        private Material _previewMat;
        private Material[] _originalMaterials;

        // ─── Smooth cache ───────────────────────────────────────────────
        private List<int>[] _adjacency;
        private Color[] _smoothBuffer; // pre-allocated, reused

        // ─── Input state ────────────────────────────────────────────────
        private bool _resizing;
        private bool _adjustingOpacity;
        private bool _adjustingFalloff;

        // ─── Selection ──────────────────────────────────────────────────
        private GameObject _lastTarget;
        private string _status = "Ready.";

        // ─── Log ────────────────────────────────────────────────────────
        private List<string> _log = new List<string>(32);
        private Vector2 _logScroll;
        private bool _logExpanded;

        // ─── Constants ──────────────────────────────────────────────────
        private static readonly string[] ChannelLabels = { "RGBA", "R", "G", "B", "A", "Smooth" };
        private static readonly int[] ChannelValues = { 0, 1, 2, 3, 4, 5 };

        // ─── Lifecycle ──────────────────────────────────────────────────
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Log("Vertex Paint ready.");
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DiscardWorkingMesh();
            DisablePreview();
        }

        // ─── Window GUI ─────────────────────────────────────────────────
        private void OnGUI()
        {
            DrawTargetSection();
            EditorGUILayout.Space(4);
            DrawBrushSection();
            EditorGUILayout.Space(4);
            DrawShortcutsSection();
            EditorGUILayout.Space(4);
            DrawActionsSection();
            EditorGUILayout.Space(2);
            DrawStatusSection();
            EditorGUILayout.Space(2);
            DrawLogSection();
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

            var go = Selection.activeGameObject;
            bool valid = go != null
                && (go.TryGetComponent<MeshFilter>(out _) || go.TryGetComponent<SkinnedMeshRenderer>(out _));

            if (!valid)
            {
                EditorGUILayout.HelpBox(
                    "Select a GameObject with MeshFilter or SkinnedMeshRenderer.", MessageType.Info);
                ClearTarget();
                return;
            }

            if (go != _lastTarget)
                SetTarget(go);

            EditorGUILayout.LabelField(go.name, EditorStyles.boldLabel);
            if (_sourceMesh != null)
            {
                string icon = _hasUnsavedChanges ? " \u25cf" : "";
                EditorGUILayout.LabelField(
                    $"{_sourceMesh.name}  |  {_sourceMesh.vertexCount} verts{icon}",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawBrushSection()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

            int ch = (int)_paintChannel;
            ch = EditorGUILayout.IntPopup("Channel", ch, ChannelLabels, ChannelValues);
            _paintChannel = (PaintChannel)ch;

            if (_paintChannel == PaintChannel.RGBA)
                _brushColor = EditorGUILayout.ColorField("Color", _brushColor);
            else if (_paintChannel != PaintChannel.Smooth)
                _channelValue = EditorGUILayout.Slider("Value", _channelValue, 0f, 1f);

            EditorGUILayout.MinMaxSlider($"Size: {_brushSize:F2}", ref _brushSize, ref _brushSize, 0.01f, 5f);
            _brushSize = Mathf.Max(_brushSize, 0.01f);
            _brushFalloff = EditorGUILayout.Slider("Falloff", _brushFalloff, 0f, 1f);
            _brushOpacity = EditorGUILayout.Slider("Opacity", _brushOpacity, 0f, 1f);
        }

        private void DrawShortcutsSection()
        {
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Paint: Left Mouse  |  Size: Ctrl+X  |  Opacity: Shift+X  |  Falloff: Ctrl+Shift+X",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawActionsSection()
        {
            // Action buttons row
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _workingMesh != null && _hasUnsavedChanges;
            if (GUILayout.Button(new GUIContent(" Save", "Write vertex colors back to the original mesh asset"),
                    GUILayout.Height(28)))
                SaveMesh();
            GUI.enabled = true;

            if (GUILayout.Button(new GUIContent(" Fill", "Apply current color to all vertices"),
                    GUILayout.Height(28)))
                ExecuteFlood();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Paint / Preview buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var paintIcon = EditorGUIUtility.IconContent("EditCollider").image;
            string paintLabel = _isEditing ? "  Stop Painting" : "  Start Paint";
            if (GUILayout.Button(new GUIContent(paintLabel, paintIcon, "Toggle painting (C)"),
                    GUILayout.Width(140), GUILayout.Height(28)))
                TogglePaint();

            GUILayout.Space(8);

            var previewIcon = EditorGUIUtility.IconContent("VisibilityOn").image;
            string previewLabel = _isPreview ? "  Stop Preview" : "  Start Preview";
            if (GUILayout.Button(new GUIContent(previewLabel, previewIcon, "Toggle vertex-color preview"),
                    GUILayout.Width(140), GUILayout.Height(28)))
                TogglePreview();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Status line
            string mode = "";
            if (_isEditing) mode += "\u25cf Painting";
            if (_isEditing && _isPreview) mode += "  |  ";
            if (_isPreview) mode += "\u25cf Previewing";
            if (mode.Length > 0)
                EditorGUILayout.LabelField(mode, EditorStyles.boldLabel);
            else
                EditorGUILayout.LabelField("Idle", EditorStyles.miniLabel);
        }

        private void DrawStatusSection()
        {
            var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            r = EditorGUI.IndentedRect(r);
            MessageType type = _hasUnsavedChanges ? MessageType.Warning : MessageType.None;
            EditorGUI.HelpBox(r, _status, type);
        }

        private void DrawLogSection()
        {
            _logExpanded = EditorGUILayout.Foldout(_logExpanded, "Log", true, EditorStyles.foldout);
            if (_logExpanded)
            {
                _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(60));
                EditorGUILayout.TextArea(string.Join("\n", _log), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    _log.Clear();
                    Repaint();
                }
            }
        }

        // ─── Target Management ──────────────────────────────────────────
        private void SetTarget(GameObject go)
        {
            DiscardWorkingMesh();
            _lastTarget = go;

            if (go.TryGetComponent<MeshFilter>(out MeshFilter mf))
                _sourceMesh = mf.sharedMesh;
            else if (go.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr))
                _sourceMesh = smr.sharedMesh;

            _isEditing = false;
            _isPreview = false;
            _adjacency = null;
            _smoothBuffer = null;
            _hasUnsavedChanges = false;

            if (_sourceMesh != null)
            {
                _smoothBuffer = new Color[_sourceMesh.vertexCount];
                Log($"Target: {go.name}  ({_sourceMesh.vertexCount} verts)");
            }
        }

        private void ClearTarget()
        {
            if (_lastTarget != null)
            {
                DiscardWorkingMesh();
                _lastTarget = null;
                _sourceMesh = null;
                _isEditing = false;
                _isPreview = false;
                _adjacency = null;
                _smoothBuffer = null;
                _hasUnsavedChanges = false;
            }
        }

        // ─── Paint / Preview Toggles ────────────────────────────────────
        private void TogglePaint()
        {
            _isEditing = !_isEditing;
            if (_isEditing)
            {
                EnsureWorkingMesh();
                if (_workingMesh != null)
                    Log("Painting mode on.");
                else
                    _isEditing = false;
            }
            else
            {
                Log("Painting mode off.");
            }
        }

        private void TogglePreview()
        {
            if (_isPreview)
            {
                _isPreview = false;
                DisablePreview();
                Log("Preview off.");
            }
            else
            {
                _isPreview = true;
                EnablePreview();
            }
        }

        // ─── Working Mesh ───────────────────────────────────────────────
        private void EnsureWorkingMesh()
        {
            if (_workingMesh != null || _sourceMesh == null || _lastTarget == null)
                return;

            _workingMesh = Object.Instantiate(_sourceMesh);
            _workingMesh.hideFlags = HideFlags.DontSave;
            _workingMesh.name = _sourceMesh.name + " (working)";

            if (_lastTarget.TryGetComponent<MeshFilter>(out MeshFilter mf))
                mf.mesh = _workingMesh;
            else if (_lastTarget.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr))
                smr.sharedMesh = _workingMesh;

            _hasUnsavedChanges = true;
            Log("Created working mesh copy. Paint to modify, then Save to persist.");
        }

        private void DiscardWorkingMesh()
        {
            if (_workingMesh == null) return;

            if (_lastTarget != null)
            {
                if (_lastTarget.TryGetComponent<MeshFilter>(out MeshFilter mf))
                    mf.sharedMesh = _sourceMesh;
                else if (_lastTarget.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr))
                    smr.sharedMesh = _sourceMesh;
            }

            Object.DestroyImmediate(_workingMesh);
            _workingMesh = null;
            _hasUnsavedChanges = false;
        }

        private void SaveMesh()
        {
            if (_sourceMesh == null || _workingMesh == null) return;

            _sourceMesh.colors = _workingMesh.colors;
            _sourceMesh.UploadMeshData(false);
            EditorUtility.SetDirty(_sourceMesh);
            AssetDatabase.SaveAssets();

            _hasUnsavedChanges = false;
            Log($"Saved vertex colors to {_sourceMesh.name}.");
        }

        // ─── Flood ──────────────────────────────────────────────────────
        private void ExecuteFlood()
        {
            Mesh target = _workingMesh ?? _sourceMesh;
            if (target == null) return;

            EnsureWorkingMesh();
            if (_workingMesh == null) return;

            Undo.RegisterCompleteObjectUndo(_workingMesh, "Vertex Flood");
            var colors = _workingMesh.colors;
            if (colors == null || colors.Length == 0)
                colors = new Color[_workingMesh.vertexCount];

            VertexPaintUtility.FloodColors(colors, _brushColor, _paintChannel, _channelValue);

            _workingMesh.colors = colors;
            _workingMesh.UploadMeshData(false);
            _hasUnsavedChanges = true;
            Log($"Flooded {_workingMesh.vertexCount} vertices.");
        }

        // ─── Scene View ─────────────────────────────────────────────────
        private void OnSceneGUI(SceneView sv)
        {
            if (!_isEditing || _workingMesh == null || _lastTarget == null)
                return;

            var evt = Event.current;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            RaycastHit hit;
            bool isHit = VertexPaintUtility.RaycastMesh(ray, _workingMesh,
                _lastTarget.transform.localToWorldMatrix, out hit);

            if (isHit || _resizing)
            {
                DrawBrushDisc(hit);
            }

            ProcessSceneInput(evt, isHit, hit);

            if (evt.type == EventType.Repaint)
                Repaint();
        }

        private void DrawBrushDisc(RaycastHit hit)
        {
            float alpha = _brushOpacity;
            Color discColor;

            if (_paintChannel == PaintChannel.RGBA)
                discColor = new Color(_brushColor.r, _brushColor.g, _brushColor.b, alpha);
            else if (_paintChannel == PaintChannel.Smooth)
                discColor = new Color(0f, 0.5f, 1f, alpha * 0.5f);
            else
                discColor = new Color(_channelValue, 0f, 0f, alpha);

            Handles.color = discColor;
            Handles.DrawSolidDisc(hit.point, hit.normal, _brushSize);
            Handles.color = Color.white;
            Handles.DrawWireDisc(hit.point, hit.normal, _brushSize);
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawWireDisc(hit.point, hit.normal, _brushSize * _brushFalloff);
        }

        private void ProcessSceneInput(Event evt, bool isHit, RaycastHit hit)
        {
            // Ctrl+drag = brush size
            if (evt.control && !evt.shift && evt.button == 0)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    _brushSize += evt.delta.x * 0.005f;
                    _brushSize = Mathf.Clamp(_brushSize, 0.01f, 5f);
                    _resizing = true;
                    _status = $"Size: {_brushSize:F2}";
                }
                if (evt.type == EventType.MouseUp) _resizing = false;
                evt.Use();
                return;
            }

            // Shift+drag = opacity
            if (evt.shift && !evt.control && evt.button == 0)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    _brushOpacity += evt.delta.x * 0.002f;
                    _brushOpacity = Mathf.Clamp01(_brushOpacity);
                    _adjustingOpacity = true;
                    _status = $"Opacity: {_brushOpacity:F2}";
                }
                if (evt.type == EventType.MouseUp) _adjustingOpacity = false;
                evt.Use();
                return;
            }

            // Ctrl+Shift+drag = falloff
            if (evt.control && evt.shift && evt.button == 0)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    _brushFalloff += evt.delta.x * 0.002f;
                    _brushFalloff = Mathf.Clamp(_brushFalloff, 0f, 1f);
                    _adjustingFalloff = true;
                    _status = $"Falloff: {_brushFalloff:F2}";
                }
                if (evt.type == EventType.MouseUp) _adjustingFalloff = false;
                evt.Use();
                return;
            }

            // Left mouse = paint
            if (!evt.control && !evt.shift && !evt.alt && evt.button == 0)
            {
                if (evt.type == EventType.MouseDown)
                {
                    _isPainting = true;
                    _recordUndo = true;
                    _status = "Painting...";
                    evt.Use();
                    return;
                }

                if (evt.type == EventType.MouseDrag && _isPainting && isHit)
                {
                    if (_recordUndo)
                    {
                        Undo.RegisterCompleteObjectUndo(_workingMesh, "Vertex Paint");
                        _recordUndo = false;
                    }

                    if (_paintChannel == PaintChannel.Smooth)
                        PaintSmooth(hit.point);
                    else
                        PaintColor(hit.point);

                    evt.Use();
                    return;
                }

                if (evt.type == EventType.MouseUp)
                {
                    _isPainting = false;
                    _status = _hasUnsavedChanges ? "Modified." : "Ready.";
                    evt.Use();
                    return;
                }
            }
        }

        // ─── Paint: Color / Channel ─────────────────────────────────────
        private void PaintColor(Vector3 hitPoint)
        {
            var verts = _workingMesh.vertices;
            var colors = _workingMesh.colors;
            if (colors == null || colors.Length == 0)
                colors = new Color[verts.Length];

            float falloffPow = Mathf.Clamp01(1f - _brushFalloff);
            var localToWorld = _lastTarget.transform.localToWorldMatrix;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 wp = localToWorld.MultiplyPoint(verts[i]);
                float dist = (wp - hitPoint).magnitude;
                if (dist > _brushSize) continue;

                float falloff = VertexPaintUtility.LinearFalloff(dist, _brushSize);
                falloff = Mathf.Pow(falloff, falloffPow) * _brushOpacity;

                if (_paintChannel == PaintChannel.RGBA)
                    colors[i] = VertexPaintUtility.BlendColor(colors[i], _brushColor, falloff);
                else
                {
                    int ch = (int)_paintChannel - 1;
                    colors[i] = VertexPaintUtility.BlendChannel(colors[i], _channelValue, falloff, ch);
                }
            }

            _workingMesh.colors = colors;
            _workingMesh.UploadMeshData(false);
            _hasUnsavedChanges = true;
        }

        // ─── Paint: Smooth ──────────────────────────────────────────────
        private void PaintSmooth(Vector3 hitPoint)
        {
            var verts = _workingMesh.vertices;
            var colors = _workingMesh.colors;
            if (colors == null || colors.Length == 0)
                colors = new Color[verts.Length];

            // Lazy-build adjacency
            if (_adjacency == null)
                _adjacency = VertexPaintUtility.BuildAdjacency(
                    _workingMesh.triangles, verts.Length);

            // Ensure smooth buffer is sized correctly
            if (_smoothBuffer == null || _smoothBuffer.Length != colors.Length)
                _smoothBuffer = new Color[colors.Length];
            System.Array.Copy(colors, _smoothBuffer, colors.Length);

            float falloffPow = Mathf.Clamp01(1f - _brushFalloff);
            var localToWorld = _lastTarget.transform.localToWorldMatrix;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 wp = localToWorld.MultiplyPoint(verts[i]);
                float dist = (wp - hitPoint).magnitude;
                if (dist > _brushSize) continue;

                float falloff = VertexPaintUtility.LinearFalloff(dist, _brushSize);
                falloff = Mathf.Pow(falloff, falloffPow) * _brushOpacity;

                var sum = colors[i];
                int count = 1;
                if (_adjacency[i] != null)
                {
                    foreach (int n in _adjacency[i])
                    {
                        sum += colors[n];
                        count++;
                    }
                }

                _smoothBuffer[i] = Color.Lerp(colors[i], sum / count, falloff);
            }

            System.Array.Copy(_smoothBuffer, colors, colors.Length);
            _workingMesh.colors = colors;
            _workingMesh.UploadMeshData(false);
            _hasUnsavedChanges = true;
        }

        // ─── Preview ─────────────────────────────────────────────────────
        private void EnablePreview()
        {
            if (_lastTarget == null || _sourceMesh == null) return;

            var shader = VertexPaintUtility.GetOrCreatePreviewShader();
            if (shader == null) { Log("Preview shader not found."); return; }

            if (_lastTarget.TryGetComponent<MeshRenderer>(out MeshRenderer mr))
            {
                _originalMaterials = mr.sharedMaterials;
                _previewMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                mr.sharedMaterials = System.Array.ConvertAll(_originalMaterials, _ => _previewMat);
            }
            else if (_lastTarget.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr))
            {
                _originalMaterials = smr.sharedMaterials;
                _previewMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                smr.sharedMaterials = System.Array.ConvertAll(_originalMaterials, _ => _previewMat);
            }
            Log("Preview on.");
        }

        private void DisablePreview()
        {
            if (_lastTarget == null) return;

            if (_lastTarget.TryGetComponent<MeshRenderer>(out MeshRenderer mr) && _originalMaterials != null)
                mr.sharedMaterials = _originalMaterials;
            else if (_lastTarget.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr) && _originalMaterials != null)
                smr.sharedMaterials = _originalMaterials;

            if (_previewMat != null)
            {
                Object.DestroyImmediate(_previewMat);
                _previewMat = null;
            }
            _originalMaterials = null;
            _isPreview = false;
        }

        // ─── Helpers ────────────────────────────────────────────────────
        private void Log(string msg)
        {
            _log.Add($"[{System.DateTime.Now:HH:mm:ss}] {msg}");
            _logScroll.y = float.MaxValue;
            _status = msg;
            Repaint();
        }
    }
}
