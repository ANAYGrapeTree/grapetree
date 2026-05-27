using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GTK.AssetTracker
{
    public struct TextureRefInfo
    {
        public string texturePath;
        public string textureName;
        public int textureSize;
        public List<string> referencedBy;
        public int referenceCount;
    }

    public static class AssetTrackerUtility
    {
        /// <summary>Find all texture assets in the project.</summary>
        public static string[] GetAllTexturePaths()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D");
            return guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }

        /// <summary>Build a map of texture GUID → texture info by scanning all material assets.</summary>
        public static Dictionary<string, TextureRefInfo> BuildReferenceMap(string[] searchFolders = null)
        {
            var texGuids = AssetDatabase.FindAssets("t:Texture2D", searchFolders);
            var texPaths = texGuids.ToDictionary(g => g, g => AssetDatabase.GUIDToAssetPath(g));

            // Initialize map
            var map = new Dictionary<string, TextureRefInfo>();
            foreach (var guid in texGuids)
            {
                var path = texPaths[guid];
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                map[guid] = new TextureRefInfo
                {
                    texturePath = path,
                    textureName = Path.GetFileNameWithoutExtension(path),
                    textureSize = tex != null ? tex.width * tex.height : 0,
                    referencedBy = new List<string>()
                };
            }

            // Scan all materials
            var matGuids = AssetDatabase.FindAssets("t:Material", searchFolders);
            foreach (var matGuid in matGuids)
            {
                var matPath = AssetDatabase.GUIDToAssetPath(matGuid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                // Check all texture property names
                var propNames = mat.GetTexturePropertyNames();
                foreach (var propName in propNames)
                {
                    var tex = mat.GetTexture(propName);
                    if (tex == null) continue;

                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(tex, out string texGuid, out long _))
                    {
                        if (map.ContainsKey(texGuid))
                        {
                            var info = map[texGuid];
                            if (!info.referencedBy.Contains(matPath))
                            {
                                info.referencedBy.Add(matPath);
                                map[texGuid] = info;
                            }
                        }
                    }
                }
            }

            // Populate reference count from material references
            var keys = new List<string>(map.Keys);
            foreach (var guid in keys)
            {
                var info = map[guid];
                info.referenceCount = info.referencedBy.Count;
                map[guid] = info;
            }

            return map;
        }

        /// <summary>Find textures with zero material references.</summary>
        public static List<TextureRefInfo> FindUnusedTextures(string[] searchFolders = null)
        {
            var map = BuildReferenceMap(searchFolders);
            return map.Values.Where(v => v.referenceCount == 0)
                .OrderByDescending(v => v.textureSize).ToList();
        }

        /// <summary>Trace which materials reference a specific texture.</summary>
        public static TextureRefInfo TraceTexture(string texturePath)
        {
            var map = BuildReferenceMap();
            var guid = AssetDatabase.AssetPathToGUID(texturePath);
            return map.ContainsKey(guid) ? map[guid] : default;
        }

        /// <summary>Generate a full reference report sorted by reference count (ascending).</summary>
        public static List<TextureRefInfo> GenerateReport(string[] searchFolders = null)
        {
            var map = BuildReferenceMap(searchFolders);
            return map.Values.OrderBy(v => v.referenceCount).ToList();
        }
    }
}
