using UnityEngine;
using UnityEditor;

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

        /// <summary>Möller–Trumbore ray-triangle intersection.</summary>
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

        /// <summary>Generate a vertex-color preview shader asset.</summary>
        public static Shader GetOrCreatePreviewShader()
        {
            const string path = "Assets/GTK/VertexColorPreview.shader";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader != null) return shader;

            string code =
                "Shader \"Hidden/GTK/VertexColorPreview\"\n"
                + "{\n"
                + "    SubShader\n"
                + "    {\n"
                + "        Tags { \"RenderType\"=\"Opaque\" }\n"
                + "        Pass\n"
                + "        {\n"
                + "            HLSLPROGRAM\n"
                + "            #pragma vertex vert\n"
                + "            #pragma fragment frag\n"
                + "            #include \"UnityCG.cginc\"\n"
                + "            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR0; };\n"
                + "            v2f vert(appdata_full v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; return o; }\n"
                + "            fixed4 frag(v2f i):SV_Target { return i.color; }\n"
                + "            ENDHLSL\n"
                + "        }\n"
                + "    }\n"
                + "}\n";

            System.IO.Directory.CreateDirectory(Application.dataPath + "/GTK");
            System.IO.File.WriteAllText(path, code);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }
    }
}
