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
        A
    }

    public class VertexPaintWindow : EditorWindow
    {
        [MenuItem("Tools/GTK/Vertex Paint")]
        private static void ShowWindow()
        {
            var w = GetWindow<VertexPaintWindow>(false, "Vertex Paint");
            w.minSize = new Vector2(320, 240);
            w.Show();
        }

        // ─── Brush state ────────────────────────────────────────────────
        [SerializeField] private PaintChannel _paintChannel = PaintChannel.RGBA;
        [SerializeField] private Color _brushColor = new Color(1f, 0f, 0f, 0f);
        [SerializeField] private float _brushSize = 0.25f;
        [SerializeField] private float _brushFalloff = 0.5f;
        [SerializeField] private float _brushOpacity = 1f;
        [SerializeField] private float _channelValue = 1f;

        // ─── Paint state ────────────────────────────────────────────────
        private bool _isPainting;
        private bool _isEditing;
        private bool _isPreview;
        private bool _recordUndo;
        private Mesh _targetMesh;
        private Material _previewMat;
        private Material[] _originalMaterials;

        // ─── Brush modifier state ───────────────────────────────────────
        private bool _resizing;
        private bool _adjustingOpacity;
        private bool _adjustingFalloff;
        private Vector2 _lastMousePos;

        // ─── Selection tracking ─────────────────────────────────────────
        private GameObject _lastTarget;
        private string _status = "Ready.";

        // ─── Log ────────────────────────────────────────────────────────
        private List<string> _log = new List<string>();
        private Vector2 _logScroll;
        private bool _logExpanded;

        // ─── Constants ──────────────────────────────────────────────────
        private static readonly string[] ChannelLabels = { "RGBA", "R", "G", "B", "A" };
        private static readonly int[] ChannelValues = { 0, 1, 2, 3, 4 };

        // ─── Lifecycle ──────────────────────────────────────────────────
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Log("Vertex Paint ready.");
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DisablePreview();
        }

        // ─── Window GUI ─────────────────────────────────────────────────
        private void OnGUI()
        {
            DrawTargetSection();
            EditorGUILayout.Space(4);
            DrawBrushSection();
            EditorGUILayout.Space(4);
            DrawSettingsSection();
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
            bool valid = ValidateTarget(go);

            if (!valid)
            {
                EditorGUILayout.HelpBox("Select a GameObject with a MeshFilter or SkinnedMeshRenderer.", MessageType.Info);
                if (_lastTarget != null)
                {
                    _lastTarget = null;
                    _targetMesh = null;
                    _isEditing = false;
                    DisablePreview();
                }
                return;
            }

            if (go != _lastTarget)
            {
                _lastTarget = go;
                _targetMesh = FindMesh(go);
                _isEditing = false;
                _isPreview = false;
                DisablePreview();
                Log($"Target: {go.name} ({(_targetMesh != null ? _targetMesh.vertexCount + " verts" : "no mesh")})");
            }

            EditorGUILayout.LabelField(go.name, EditorStyles.boldLabel);
            if (_targetMesh != null)
                EditorGUILayout.LabelField($"Mesh: {_targetMesh.name}  |  {_targetMesh.vertexCount} verts",
                    EditorStyles.miniLabel);
        }

        private void DrawBrushSection()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

            // Channel
            int ch = (int)_paintChannel;
            ch = EditorGUILayout.IntPopup("Channel", ch, ChannelLabels, ChannelValues);
            _paintChannel = (PaintChannel)ch;

            // Color (RGBA mode) or Value (single channel mode)
            if (_paintChannel == PaintChannel.RGBA)
            {
                _brushColor = EditorGUILayout.ColorField("Color", _brushColor);
            }
            else
            {
                _channelValue = EditorGUILayout.Slider("Value", _channelValue, 0f, 1f);
            }

            // Size
            EditorGUILayout.MinMaxSlider($"Size: {_brushSize:F2}", ref _brushSize, ref _brushSize, 0.01f, 5f);
            _brushSize = Mathf.Max(_brushSize, 0.01f);

            // Falloff
            _brushFalloff = EditorGUILayout.Slider("Falloff", _brushFalloff, 0f, 1f);

            // Opacity
            _brushOpacity = EditorGUILayout.Slider("Opacity", _brushOpacity, 0f, 1f);
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Paint: Left Mouse  |  Size: Ctrl+Mouse X  |  Opacity: Shift+Mouse X  |  Falloff: Ctrl+Shift+X",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            _isEditing = GUILayout.Toggle(_isEditing,
                EditorGUIUtility.IconContent("EditCollider"), "Button",
                GUILayout.Width(36), GUILayout.Height(28));
            if (GUILayout.Button(new GUIContent("Paint", "Enable scene-view painting (C)"),
                    GUILayout.Width(60), GUILayout.Height(28)))
                _isEditing = !_isEditing;

            GUILayout.Space(8);

            _isPreview = GUILayout.Toggle(_isPreview,
                EditorGUIUtility.IconContent("VisibilityOn"), "Button",
                GUILayout.Width(36), GUILayout.Height(28));
            if (GUILayout.Button(new GUIContent("Preview", "Toggle vertex-color preview"),
                    GUILayout.Width(60), GUILayout.Height(28)))
            {
                _isPreview = !_isPreview;
                if (_isPreview) EnablePreview();
                else DisablePreview();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusSection()
        {
            var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            r = EditorGUI.IndentedRect(r);
            EditorGUI.HelpBox(r, _status, MessageType.None);
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

        // ─── Scene View ─────────────────────────────────────────────────
        private void OnSceneGUI(SceneView sv)
        {
            if (!_isEditing || _targetMesh == null || _lastTarget == null)
                return;

            var evt = Event.current;
            _lastMousePos = evt.mousePosition;

            // Prevent selection while painting
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            // Ray-mesh intersection
            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            RaycastHit hit;
            bool isHit = VertexPaintUtility.RaycastMesh(ray, _targetMesh,
                _lastTarget.transform.localToWorldMatrix, out hit);

            // Draw brush disc
            if (isHit || _resizing)
            {
                float alpha = _brushOpacity;
                Color discColor = _paintChannel == PaintChannel.RGBA
                    ? new Color(_brushColor.r, _brushColor.g, _brushColor.b, alpha)
                    : new Color(_channelValue, 0f, 0f, alpha);

                Handles.color = discColor;
                Handles.DrawSolidDisc(hit.point, hit.normal, _brushSize);
                Handles.color = Color.white;
                Handles.DrawWireDisc(hit.point, hit.normal, _brushSize);
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                Handles.DrawWireDisc(hit.point, hit.normal, _brushSize * _brushFalloff);
            }

            // Process input
            ProcessSceneInput(evt, isHit, hit);

            if (evt.type == EventType.Repaint)
                Repaint();
        }

        private void ProcessSceneInput(Event evt, bool isHit, RaycastHit hit)
        {
            // Modifier: Ctrl+drag = resize brush
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

            // Modifier: Shift+drag = opacity
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

            // Modifier: Ctrl+Shift+drag = falloff
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

            // Painting: left mouse drag
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
                        Undo.RegisterCompleteObjectUndo(_targetMesh, "Vertex Paint");
                        _recordUndo = false;
                    }
                    PaintAt(hit.point, hit.normal);
                    evt.Use();
                    return;
                }

                if (evt.type == EventType.MouseUp)
                {
                    _isPainting = false;
                    _status = "Ready.";
                    evt.Use();
                    return;
                }
            }
        }

        // ─── Painting ───────────────────────────────────────────────────
        private void PaintAt(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_targetMesh == null) return;

            var verts = _targetMesh.vertices;
            var colors = _targetMesh.colors;

            if (colors == null || colors.Length == 0)
            {
                colors = new Color[verts.Length];
            }

            float falloffPow = Mathf.Clamp01(1f - _brushFalloff);
            var localToWorld = _lastTarget.transform.localToWorldMatrix;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 worldPos = localToWorld.MultiplyPoint(verts[i]);
                float dist = (worldPos - hitPoint).magnitude;

                if (dist > _brushSize) continue;

                float falloff = VertexPaintUtility.LinearFalloff(dist, _brushSize);
                falloff = Mathf.Pow(falloff, falloffPow) * _brushOpacity;

                if (_paintChannel == PaintChannel.RGBA)
                {
                    colors[i] = VertexPaintUtility.BlendColor(colors[i], _brushColor, falloff);
                }
                else
                {
                    int ch = (int)_paintChannel - 1; // RGBA=0,R=1→0,G=2→1,B=3→2,A=4→3
                    colors[i] = VertexPaintUtility.BlendChannel(colors[i], _channelValue, falloff, ch);
                }
            }

            _targetMesh.colors = colors;
            _targetMesh.UploadMeshData(false);
        }

        // ─── Preview ─────────────────────────────────────────────────────
        private void EnablePreview()
        {
            if (_lastTarget == null || _targetMesh == null) return;

            var shader = VertexPaintUtility.GetOrCreatePreviewShader();
            if (shader == null) { Log("Preview shader not found."); return; }

            if (_lastTarget.TryGetComponent<MeshRenderer>(out MeshRenderer mr))
            {
                _originalMaterials = mr.sharedMaterials;
                _previewMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                mr.sharedMaterials = System.Array.ConvertAll(_originalMaterials, _ => _previewMat);
                Log("Preview enabled.");
            }
            else if (_lastTarget.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr))
            {
                _originalMaterials = smr.sharedMaterials;
                _previewMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                smr.sharedMaterials = System.Array.ConvertAll(_originalMaterials, _ => _previewMat);
                Log("Preview enabled.");
            }
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
        private static bool ValidateTarget(GameObject go)
        {
            return go != null
                && (go.TryGetComponent<MeshFilter>(out _) && go.TryGetComponent<MeshRenderer>(out _)
                    || go.TryGetComponent<SkinnedMeshRenderer>(out _));
        }

        private static Mesh FindMesh(GameObject go)
        {
            if (go.TryGetComponent<MeshFilter>(out MeshFilter mf))
                return mf.sharedMesh;
            if (go.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr))
                return smr.sharedMesh;
            return null;
        }

        private void Log(string msg)
        {
            _log.Add($"[{System.DateTime.Now:HH:mm:ss}] {msg}");
            _logScroll.y = float.MaxValue;
            _status = msg;
            Repaint();
        }
    }
}
