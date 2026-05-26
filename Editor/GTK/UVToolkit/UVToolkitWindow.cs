using UnityEngine;
using UnityEditor;

namespace GTK.UVToolkit
{
    public class UVToolkitWindow : EditorWindow
    {
        private enum Tab { UVUnwrap, Checker, Overlap, TexelDensity }

        private Tab _currentTab;
        private readonly string[] _tabLabels = { "UV Unwrap", "Checker", "Overlap", "Texel Density" };

        private UVUnwrapModule _unwrap;
        private CheckerModule _checker;
        private OverlapDetectionModule _overlap;
        private TexelDensityModule _texel;
        private GameObject _selected;

        [MenuItem("Tools/GTK/UV Toolkit #U")]
        private static void Open()
        {
            var win = GetWindow<UVToolkitWindow>(false, "UV Toolkit");
            win.minSize = new Vector2(400, 500);
            win.Show();
        }

        private void OnEnable()
        {
            _unwrap = new UVUnwrapModule();
            _checker = new CheckerModule();
            _overlap = new OverlapDetectionModule();
            _texel = new TexelDensityModule();
            UpdateSelection();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSelectionChange() { UpdateSelection(); Repaint(); }

        private void UpdateSelection()
        {
            _selected = Selection.activeGameObject;
            _unwrap?.SetTarget(_selected);
            _checker?.SetTarget(_selected);
            _overlap?.SetTarget(_selected);
            _texel?.SetTarget(_selected);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();
            EditorGUILayout.Space(4);
            DrawModuleGUI();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UV Toolkit", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Grapetree Toolkit — UV Tools for Technical Artists", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                bool sel = (int)_currentTab == i;
                GUI.backgroundColor = sel ? new Color(0.3f, 0.6f, 1f) : Color.gray;
                if (GUILayout.Button(_tabLabels[i], GUILayout.Height(24))) _currentTab = (Tab)i;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleGUI()
        {
            switch (_currentTab)
            {
                case Tab.UVUnwrap: _unwrap?.DrawGUI(); break;
                case Tab.Checker: _checker?.DrawGUI(); break;
                case Tab.Overlap: _overlap?.DrawGUI(); break;
                case Tab.TexelDensity: _texel?.DrawGUI(); break;
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Selected: {_selected?.name ?? "—"}", EditorStyles.centeredGreyMiniLabel);
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (_currentTab == Tab.Overlap) _overlap?.DrawSceneOverlay();
            else if (_currentTab == Tab.TexelDensity) _texel?.DrawSceneOverlay();
        }
    }
}
