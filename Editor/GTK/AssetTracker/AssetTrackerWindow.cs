using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace GTK.AssetTracker
{
    public class AssetTrackerWindow : EditorWindow
    {
        [SerializeField] private Vector2 _scrollPos;
        [SerializeField] private string _tracePath = "";
        [SerializeField] private string _searchFilter = "";

        private List<TextureRefInfo> _results;
        private bool _hasResults;

        private enum Tab { Unused, Trace, Report }
        [SerializeField] private Tab _tab;

        [MenuItem("Tools/GTK/Asset Tracker")]
        private static void ShowWindow()
        {
            var w = GetWindow<AssetTrackerWindow>(false, "Asset Tracker");
            w.minSize = new Vector2(500, 400);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Asset Reference Tracker", EditorStyles.largeLabel);
            EditorGUILayout.Space(4);

            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Unused Textures", "Trace Texture", "Full Report" }, GUILayout.Height(22));
            EditorGUILayout.Space(6);

            _searchFilter = EditorGUILayout.TextField("Search Folder Filter", _searchFilter);
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case Tab.Unused: DrawUnusedTab(); break;
                case Tab.Trace: DrawTraceTab(); break;
                case Tab.Report: DrawReportTab(); break;
            }
        }

        private string[] GetSearchFolders()
        {
            return string.IsNullOrEmpty(_searchFilter) ? null : new[] { _searchFilter };
        }

        private void DrawUnusedTab()
        {
            if (GUILayout.Button("Scan Unused Textures", GUILayout.Height(26)))
            {
                _results = AssetTrackerUtility.FindUnusedTextures(GetSearchFolders());
                _hasResults = true;
            }

            if (!_hasResults) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Found {_results.Count} unused textures", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var r in _results)
            {
                EditorGUILayout.BeginHorizontal();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(r.texturePath);
                EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(200));
                EditorGUILayout.LabelField($"{(r.textureSize / 1024f):F1}K px", GUILayout.Width(80));
                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(r.texturePath));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Export Unused List (JSON)", GUILayout.Height(24)))
                ExportResults(_results);
        }

        private void DrawTraceTab()
        {
            EditorGUILayout.LabelField("Select a texture to trace its material references:", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);

            var tex = (Texture2D)EditorGUILayout.ObjectField("Texture", null, typeof(Texture2D), false);
            if (tex != null)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                var info = AssetTrackerUtility.TraceTexture(path);
                _tracePath = path;

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField($"Texture: {info.textureName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Path: {info.texturePath}");
                EditorGUILayout.LabelField($"Referenced by {info.referenceCount} materials:");

                if (info.referencedBy != null)
                {
                    foreach (var matPath in info.referencedBy)
                    {
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        EditorGUILayout.ObjectField(mat, typeof(Material), false);
                    }
                }
                if (info.referenceCount == 0)
                    EditorGUILayout.HelpBox("This texture is not referenced by any material.", MessageType.Warning);
            }
        }

        private void DrawReportTab()
        {
            if (GUILayout.Button("Generate Report", GUILayout.Height(26)))
            {
                _results = AssetTrackerUtility.GenerateReport(GetSearchFolders());
                _hasResults = true;
            }

            if (!_hasResults) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Total: {_results.Count} textures", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var r in _results)
            {
                EditorGUILayout.BeginHorizontal();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(r.texturePath);
                EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(180));
                EditorGUILayout.LabelField($"Refs: {r.referenceCount}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{(r.textureSize / 1024f):F1}K px", GUILayout.Width(80));
                if (r.referenceCount == 0)
                    EditorGUILayout.LabelField("UNUSED", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Export Report (JSON)", GUILayout.Height(24)))
                ExportResults(_results);
        }

        private void ExportResults(List<TextureRefInfo> results)
        {
            string path = EditorUtility.SaveFilePanel("Export Report", "Assets", "texture_report.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            using (var w = new StreamWriter(path))
            {
                w.WriteLine("[");
                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    w.WriteLine($"  {{\"name\":\"{r.textureName}\",\"path\":\"{r.texturePath}\",\"refs\":{r.referenceCount},\"size\":{r.textureSize}}}");
                    if (i < results.Count - 1) w.Write(",");
                    w.WriteLine();
                }
                w.WriteLine("]");
            }
            AssetDatabase.Refresh();
            Debug.Log($"Report exported to {path}");
        }
    }
}
