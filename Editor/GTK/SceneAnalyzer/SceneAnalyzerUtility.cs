using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace GTK.SceneAnalyzer
{
    public struct SceneObjectStats
    {
        public GameObject gameObject;
        public string name;
        public int triangleCount;
        public int vertexCount;
        public int materialCount;
        public long textureMemoryBytes;
        public int lightmapCount;
    }

    public static class SceneAnalyzerUtility
    {
        public static List<SceneObjectStats> AnalyzeScene()
        {
            var results = new List<SceneObjectStats>();
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            foreach (var renderer in allRenderers)
            {
                if (!renderer.gameObject.scene.isLoaded) continue;

                var stats = new SceneObjectStats
                {
                    gameObject = renderer.gameObject,
                    name = renderer.gameObject.name,
                    materialCount = renderer.sharedMaterials?.Length ?? 0
                };

                // Mesh stats
                int triCount = 0, vertCount = 0;
                if (renderer is MeshFilter mf && mf.sharedMesh != null)
                {
                    triCount = mf.sharedMesh.triangles.Length / 3;
                    vertCount = mf.sharedMesh.vertexCount;
                }
                else if (renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    triCount = smr.sharedMesh.triangles.Length / 3;
                    vertCount = smr.sharedMesh.vertexCount;
                }
                else if (renderer is ParticleSystemRenderer)
                {
                    continue; // skip particle systems
                }

                stats.triangleCount = triCount;
                stats.vertexCount = vertCount;

                // Texture memory
                long texMem = 0;
                if (renderer.sharedMaterials != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null || mat.mainTexture == null) continue;
                        int w = mat.mainTexture.width;
                        int h = mat.mainTexture.height;
                        texMem += w * h * 4; // approximate RGBA32
                    }
                }
                stats.textureMemoryBytes = texMem;

                results.Add(stats);
            }

            return results.OrderByDescending(r => r.triangleCount).ToList();
        }

        public static Color GetHeatColor(float value, float min, float max)
        {
            float t = Mathf.Clamp01((value - min) / Mathf.Max(max - min, 0.001f));
            if (t < 0.5f) return Color.Lerp(Color.green, Color.yellow, t * 2f);
            return Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
        }
    }
}
