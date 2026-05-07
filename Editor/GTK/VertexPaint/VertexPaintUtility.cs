using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace GTK.VertexPaint
{
    public static class VertexPaintUtility
    {
        /// <summary>Linear falloff: 1 at center, 0 at brush edge.</summary>
        public static float LinearFalloff(float distance, float brushSize)
        {
            return Mathf.Clamp01(1f - distance / brushSize);
        }

        /// <summary>Blend channel with target value.</summary>
        public static Color BlendChannel(Color current, float targetValue, float falloff, int channel)
        {
            float v = Mathf.Lerp(current[channel], targetValue, falloff);
            current[channel] = v;
            return current;
        }

        /// <summary>Blend entire color.</summary>
        public static Color BlendColor(Color current, Color target, float falloff)
        {
            return Color.Lerp(current, target, falloff);
        }

        /// <summary>Fill all vertices with a color or channel value.</summary>
        public static void FloodColors(Color[] colors, Color brushColor, PaintChannel channel, float value)
        {
            if (channel == PaintChannel.RGBA)
            {
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = brushColor;
            }
            else if (channel == PaintChannel.Smooth)
            {
                return;
            }
            else
            {
                int ch = (int)channel - 1;
                for (int i = 0; i < colors.Length; i++)
                    colors[i][ch] = value;
            }
        }

        /// <summary>Build vertex adjacency from triangles.</summary>
        public static List<int>[] BuildAdjacency(int[] triangles, int vertexCount)
        {
            var adj = new List<int>[vertexCount];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int i0 = triangles[t];
                int i1 = triangles[t + 1];
                int i2 = triangles[t + 2];

                AddEdge(adj, i0, i1);
                AddEdge(adj, i0, i2);
                AddEdge(adj, i1, i2);
            }
            return adj;
        }

        private static void AddEdge(List<int>[] adj, int a, int b)
        {
            if (adj[a] == null) adj[a] = new List<int>(4);
            if (!adj[a].Contains(b)) adj[a].Add(b);
            if (adj[b] == null) adj[b] = new List<int>(4);
            if (!adj[b].Contains(a)) adj[b].Add(a);
        }

        /// <summary>Load the VertexColorPreview shader shipped with the package.</summary>
        public static Shader GetOrCreatePreviewShader()
        {
            var shader = Shader.Find("Hidden/GTK/VertexColorPreview");
            if (shader != null) return shader;

            // Fallback: search by name in AssetDatabase
            var guids = AssetDatabase.FindAssets("VertexColorPreview t:Shader");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        /// <summary>Ray-mesh intersection using Möller–Trumbore algorithm.</summary>
        public static bool RaycastMesh(Ray ray, Mesh mesh, Matrix4x4 localToWorld, out RaycastHit hit)
        {
            hit = new RaycastHit();
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            float minDist = float.MaxValue;
            bool hitFound = false;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 v0 = localToWorld.MultiplyPoint(verts[tris[i]]);
                Vector3 v1 = localToWorld.MultiplyPoint(verts[tris[i + 1]]);
                Vector3 v2 = localToWorld.MultiplyPoint(verts[tris[i + 2]]);

                float d;
                Vector2 bary;
                if (RayTriangle(ray, v0, v1, v2, out d, out bary) && d > 0f && d < minDist)
                {
                    minDist = d;
                    hit.point = ray.GetPoint(d);
                    hit.normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
                    hit.distance = d;
                    hit.barycentricCoordinate = bary;
                    hitFound = true;
                }
            }
            return hitFound;
        }

        private static bool RayTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2,
            out float dist, out Vector2 bary)
        {
            dist = 0f;
            bary = Vector2.zero;

            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;
            Vector3 p = Vector3.Cross(ray.direction, e2);
            float det = Vector3.Dot(e1, p);

            if (Mathf.Abs(det) < 1e-6f) return false;
            float invDet = 1f / det;

            Vector3 tVec = ray.origin - v0;
            float u = Vector3.Dot(tVec, p) * invDet;
            if (u < 0f || u > 1f) return false;

            Vector3 q = Vector3.Cross(tVec, e1);
            float v = Vector3.Dot(ray.direction, q) * invDet;
            if (v < 0f || u + v > 1f) return false;

            float t = Vector3.Dot(e2, q) * invDet;
            if (t < 0f) return false;

            dist = t;
            bary = new Vector2(u, v);
            return true;
        }
    }
}
