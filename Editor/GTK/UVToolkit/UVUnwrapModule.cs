using UnityEngine;
using UnityEditor;

namespace GTK.UVToolkit
{
    public class UVUnwrapModule
    {
        private GameObject _target;
        private MeshFilter _mf;
        private SkinnedMeshRenderer _smr;
        private Mesh _originalMesh;

        private float _angleError = 8f;
        private float _areaError = 15f;
        private float _hardAngle = 88f;
        private float _packMargin = 0.01f;
        private int _selectedChannel;

        private static readonly string[] ChannelLabels = { "UV0", "UV1 (UV2)", "UV2 (UV3)", "UV3 (UV4)", "UV4 (UV5)" };

        public void SetTarget(GameObject obj)
        {
            _target = obj;
            if (obj == null) { _mf = null; _smr = null; _originalMesh = null; return; }
            _mf = obj.GetComponent<MeshFilter>();
            _smr = obj.GetComponent<SkinnedMeshRenderer>();
            _originalMesh = _mf != null ? _mf.sharedMesh : _smr != null ? _smr.sharedMesh : null;
        }

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            if (_target == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with MeshFilter or SkinnedMeshRenderer.", MessageType.Info);
                return;
            }
            if (_originalMesh == null)
            {
                EditorGUILayout.HelpBox("No valid mesh found on the selected object.", MessageType.Warning);
                return;
            }
            if (!_originalMesh.isReadable)
            {
                EditorGUILayout.HelpBox("Mesh is not readable. Enable Read/Write in import settings.", MessageType.Error);
                return;
            }

            DrawUVChannelStatus();
            DrawUnwrapParams();
            DrawActions();
        }

        private void DrawUVChannelStatus()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("UV Channels", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            for (int i = 0; i < 5; i++)
            {
                var uv = GetUV(i);
                bool hasData = uv != null && uv.Length == _originalMesh.vertexCount;
                EditorGUILayout.LabelField($"  {ChannelLabels[i]}: {(hasData ? $"✓ {uv.Length} verts" : "✗ empty")}");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawUnwrapParams()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Unwrap Parameters", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _angleError = EditorGUILayout.Slider("Angle Error (°)", _angleError, 1f, 75f);
            _areaError = EditorGUILayout.Slider("Area Error (%)", _areaError, 1f, 75f);
            _hardAngle = EditorGUILayout.Slider("Hard Angle (°)", _hardAngle, 30f, 180f);
            _packMargin = EditorGUILayout.Slider("Pack Margin", _packMargin, 0.001f, 0.1f);
            EditorGUILayout.EndVertical();
            _selectedChannel = EditorGUILayout.Popup("Target Channel", _selectedChannel, ChannelLabels);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Unwrap", GUILayout.Height(28))) AutoUnwrap();
            if (GUILayout.Button("Unwrap UV2 (Lightmap)", GUILayout.Height(28))) UnwrapUV2();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Reset to Original UV", GUILayout.Height(24))) ResetUV();
        }

        private void AutoUnwrap()
        {
            var param = new UnwrapParam
            {
                angleError = Mathf.Deg2Rad * _angleError,
                areaError = _areaError / 100f,
                hardAngle = _hardAngle,
                packMargin = _packMargin
            };
            var newMesh = Object.Instantiate(_originalMesh);
            newMesh.name = _originalMesh.name + "_Unwrapped";
            Unwrapping.GeneratePerTriangleUV(newMesh, param);
            ApplyMesh(newMesh);
            Debug.Log($"Auto UV unwrap completed on {_target.name}");
        }

        private void UnwrapUV2()
        {
            var param = new UnwrapParam
            {
                angleError = Mathf.Deg2Rad * _angleError,
                areaError = _areaError / 100f,
                hardAngle = _hardAngle,
                packMargin = _packMargin
            };
            var newMesh = Object.Instantiate(_originalMesh);
            newMesh.name = _originalMesh.name + "_UV2";
            Unwrapping.GenerateSecondaryUVSet(newMesh, param);
            ApplyMesh(newMesh);
            Debug.Log($"UV2 (lightmap) unwrap completed on {_target.name}");
        }

        private void ResetUV()
        {
            if (_originalMesh == null) return;
            if (!EditorUtility.DisplayDialog("Reset UV",
                $"Restore {_target.name} to its original UV layout?", "Reset", "Cancel"))
                return;
            Undo.RecordObject(_target, "Reset UV");
            if (_mf != null) _mf.sharedMesh = _originalMesh;
            else if (_smr != null) _smr.sharedMesh = _originalMesh;
            Debug.Log($"UV reset on {_target.name}");
        }

        private void ApplyMesh(Mesh mesh)
        {
            Undo.RecordObject(_target, "Apply Unwrap");
            if (_mf != null) _mf.sharedMesh = mesh;
            else if (_smr != null) _smr.sharedMesh = mesh;
        }

        private UnityEngine.Vector2[] GetUV(int channel)
        {
            if (_originalMesh == null) return null;
            return channel switch
            {
                0 => _originalMesh.uv,
                1 => _originalMesh.uv2,
                2 => _originalMesh.uv3,
                3 => _originalMesh.uv4,
                4 => _originalMesh.uv5,
                _ => null
            };
        }
    }
}
