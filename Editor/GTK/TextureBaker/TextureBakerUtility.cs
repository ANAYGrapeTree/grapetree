using UnityEngine;
using System;

namespace GTK.TextureBaker
{
    public static class TextureBakerUtility
    {
        /// <summary>Heightmap to normal map (Sobel filter).</summary>
        public static Texture2D HeightToNormal(Texture2D heightmap, float strength)
        {
            int w = heightmap.width, h = heightmap.height;
            var readable = EnsureReadable(heightmap);
            var hPixels = readable.GetPixels();
            var nPixels = new Color[w * h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float tl = Sample(hPixels, w, h, x - 1, y + 1).r;
                    float t = Sample(hPixels, w, h, x, y + 1).r;
                    float tr = Sample(hPixels, w, h, x + 1, y + 1).r;
                    float l = Sample(hPixels, w, h, x - 1, y).r;
                    float r = Sample(hPixels, w, h, x + 1, y).r;
                    float bl = Sample(hPixels, w, h, x - 1, y - 1).r;
                    float b = Sample(hPixels, w, h, x, y - 1).r;
                    float br = Sample(hPixels, w, h, x + 1, y - 1).r;

                    float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dy = (bl + 2f * b + br) - (tl + 2f * t + tr);
                    dx *= strength;
                    dy *= strength;

                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    n = n * 0.5f + new Vector3(0.5f, 0.5f, 0.5f);
                    nPixels[y * w + x] = new Color(n.x, n.y, n.z, 1f);
                }

            if (readable != heightmap) UnityEngine.Object.DestroyImmediate(readable);

            var result = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            result.SetPixels(nPixels);
            result.Apply();
            return result;
        }

        /// <summary>Normal map to heightmap (integrate from normal deviation).</summary>
        public static Texture2D NormalToHeight(Texture2D normalMap)
        {
            int w = normalMap.width, h = normalMap.height;
            var readable = EnsureReadable(normalMap);
            var pixels = readable.GetPixels();
            var height = new float[w * h];

            // Simple integration: accumulate Y-component deviation
            for (int y = 0; y < h; y++)
            {
                float row = 0f;
                for (int x = 0; x < w; x++)
                {
                    var n = pixels[y * w + x];
                    float ny = (n.g * 2f) - 1f;
                    row += 1f - ny;
                    height[y * w + x] = row;
                }
            }

            // Normalize
            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in height) { if (v < min) min = v; if (v > max) max = v; }
            float range = Mathf.Max(max - min, 0.001f);

            var hPixels = new Color[w * h];
            for (int i = 0; i < height.Length; i++)
            {
                float v = (height[i] - min) / range;
                hPixels[i] = new Color(v, v, v, 1f);
            }

            if (readable != normalMap) UnityEngine.Object.DestroyImmediate(readable);

            var result = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            result.SetPixels(hPixels);
            result.Apply();
            return result;
        }

        /// <summary>Convert normal map between OpenGL (Y-up) and DirectX (Y-down).</summary>
        public static Texture2D ConvertNormalFormat(Texture2D normalMap, bool toDX)
        {
            int w = normalMap.width, h = normalMap.height;
            var readable = EnsureReadable(normalMap);
            var pixels = readable.GetPixels();
            var outPixels = new Color[w * h];

            for (int i = 0; i < pixels.Length; i++)
            {
                float g = toDX ? Mathf.Clamp01(2f * (0.5f - pixels[i].g)) : 1f - 2f * (0.5f - pixels[i].g);
                outPixels[i] = new Color(pixels[i].r, g, pixels[i].b, pixels[i].a);
            }

            if (readable != normalMap) UnityEngine.Object.DestroyImmediate(readable);

            var result = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            result.SetPixels(outPixels);
            result.Apply();
            return result;
        }

        /// <summary>Estimate AO from normal map (brightness of upward-facing areas).</summary>
        public static Texture2D AOFromNormal(Texture2D normalMap, float strength)
        {
            int w = normalMap.width, h = normalMap.height;
            var readable = EnsureReadable(normalMap);
            var pixels = readable.GetPixels();
            var aoPixels = new Color[w * h];

            for (int i = 0; i < pixels.Length; i++)
            {
                float nz = (pixels[i].b * 2f) - 1f;
                float ao = Mathf.Clamp01(nz * strength + (1f - strength) * 0.5f);
                aoPixels[i] = new Color(ao, ao, ao, 1f);
            }

            if (readable != normalMap) UnityEngine.Object.DestroyImmediate(readable);

            var result = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            result.SetPixels(aoPixels);
            result.Apply();
            return result;
        }

        /// <summary>Dilate AO texture (expand bright regions).</summary>
        public static Texture2D AODilate(Texture2D aoMap, int iterations)
        {
            int w = aoMap.width, h = aoMap.height;
            var readable = EnsureReadable(aoMap);
            var pixels = (Color[])readable.GetPixels().Clone();

            for (int iter = 0; iter < iterations; iter++)
            {
                var copy = (Color[])pixels.Clone();
                for (int y = 1; y < h - 1; y++)
                    for (int x = 1; x < w - 1; x++)
                    {
                        int idx = y * w + x;
                        float maxVal = pixels[idx].r;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                maxVal = Mathf.Max(maxVal, pixels[(y + dy) * w + (x + dx)].r);
                        copy[idx] = new Color(maxVal, maxVal, maxVal, 1f);
                    }
                pixels = copy;
            }

            if (readable != aoMap) UnityEngine.Object.DestroyImmediate(readable);

            var result = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        private static float Sample(Color[] pixels, int w, int h, int x, int y)
        {
            return pixels[Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1)].r;
        }

        private static Texture2D EnsureReadable(Texture2D tex)
        {
            if (tex.isReadable) return tex;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }
    }
}
