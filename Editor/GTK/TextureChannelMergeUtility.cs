using UnityEngine;
using UnityEditor;
using System.IO;

namespace GTK
{
    public enum SaveFormat
    {
        PNG,
        JPG,
        TGA
    }

    [System.Serializable]
    public struct ChannelSource
    {
        public Texture2D texture;
        /// <summary>0=R, 1=G, 2=B, 3=A</summary>
        public int sourceChannel;
        /// <summary>Used when texture is null.</summary>
        public float defaultColor;
    }

    /// <summary>
    /// Core logic for texture channel merging and swizzling.
    /// Pipeline-agnostic — only uses UnityEngine core APIs.
    /// </summary>
    public static class TextureChannelMergeUtility
    {
        // ─── Public API ────────────────────────────────────────────────

        public static bool IsProjectLinear()
        {
            return PlayerSettings.colorSpace == ColorSpace.Linear;
        }

        /// <summary>Save an arbitrary Texture2D to disk with chosen format.</summary>
        public static void SaveTexture(Texture2D tex, string savePath, SaveFormat format, int jpgQuality)
        {
            byte[] data = Encode(tex, format, jpgQuality);
            string dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(savePath, data);
            AssetDatabase.Refresh();
        }

        /// <summary>Merge multiple sources, encode, and save to disk.</summary>
        public static void MergeAndSave(ChannelSource[] sources, bool processInLinear,
            string savePath, SaveFormat format, int jpgQuality)
        {
            var preview = MergePreview(sources, processInLinear);
            try
            {
                SaveTexture(preview, savePath, format, jpgQuality);
            }
            finally
            {
                Object.DestroyImmediate(preview);
            }
        }

        /// <summary>Merge sources into a temporary Texture2D (for preview or save).</summary>
        public static Texture2D MergePreview(ChannelSource[] sources, bool processInLinear)
        {
            int outWidth, outHeight;
            var srcPixels = PrepareSources(sources, out outWidth, out outHeight);

            var outPixels = new Color[outWidth * outHeight];
            bool projectIsLinear = IsProjectLinear();
            bool[] isSRGB = GetSRGBFlags(sources);
            bool needLinearConversion = processInLinear && projectIsLinear;

            for (int y = 0; y < outHeight; y++)
            {
                for (int x = 0; x < outWidth; x++)
                {
                    int idx = y * outWidth + x;
                    var pixel = new Color(
                        GetChannelValue(sources, srcPixels, 0, x, y, outWidth, outHeight, needLinearConversion, isSRGB[0]),
                        GetChannelValue(sources, srcPixels, 1, x, y, outWidth, outHeight, needLinearConversion, isSRGB[1]),
                        GetChannelValue(sources, srcPixels, 2, x, y, outWidth, outHeight, needLinearConversion, isSRGB[2]),
                        GetChannelValue(sources, srcPixels, 3, x, y, outWidth, outHeight, needLinearConversion, isSRGB[3])
                    );

                    // If linear processing, convert output back to gamma for display/save
                    outPixels[idx] = processInLinear ? pixel.gamma : pixel;
                }
            }

            var output = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false, false);
            output.SetPixels(outPixels);
            output.Apply();
            return output;
        }

        /// <summary>Swizzle channels of a single texture (rearrange R/G/B/A).</summary>
        /// <param name="channelMap">Length-4 array: for each output channel (R,G,B,A), which source channel index.</param>
        public static Texture2D SwizzleChannels(Texture2D source, int[] channelMap, bool processInLinear)
        {
            var readable = EnsureReadable(source);
            var pixels = readable.GetPixels();

            bool projectIsLinear = IsProjectLinear();
            bool isSRGB = IsSRGB(source);
            bool needLinearConversion = processInLinear && projectIsLinear;

            var outPixels = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                outPixels[i] = new Color(
                    GetMappedValue(p, channelMap[0], needLinearConversion, isSRGB),
                    GetMappedValue(p, channelMap[1], needLinearConversion, isSRGB),
                    GetMappedValue(p, channelMap[2], needLinearConversion, isSRGB),
                    GetMappedValue(p, channelMap[3], needLinearConversion, isSRGB)
                );
                if (processInLinear)
                    outPixels[i] = outPixels[i].gamma;
            }

            if (readable != source)
                Object.DestroyImmediate(readable);

            var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            output.SetPixels(outPixels);
            output.Apply();
            return output;
        }

        /// <summary>Get file extension for a format (includes leading dot, lowercase).</summary>
        public static string GetExtension(SaveFormat format)
        {
            switch (format)
            {
                case SaveFormat.PNG: return ".png";
                case SaveFormat.JPG: return ".jpg";
                case SaveFormat.TGA: return ".tga";
                default: return ".png";
            }
        }

        // ─── Encoding ──────────────────────────────────────────────────

        private static byte[] Encode(Texture2D tex, SaveFormat format, int jpgQuality)
        {
            switch (format)
            {
                case SaveFormat.PNG: return tex.EncodeToPNG();
                case SaveFormat.JPG: return tex.EncodeToJPG(jpgQuality);
                case SaveFormat.TGA: return tex.EncodeToTGA();
                default: return tex.EncodeToPNG();
            }
        }

        // ─── Pixel Preparation ─────────────────────────────────────────

        private static Color[][] PrepareSources(ChannelSource[] sources, out int outWidth, out int outHeight)
        {
            int w = int.MaxValue, h = int.MaxValue;
            var srcPixels = new Color[4][];

            for (int c = 0; c < 4; c++)
            {
                var tex = sources[c].texture;
                if (tex == null)
                {
                    srcPixels[c] = null;
                    continue;
                }

                var readable = EnsureReadable(tex);
                srcPixels[c] = readable.GetPixels();
                if (readable != tex)
                    Object.DestroyImmediate(readable);

                w = Mathf.Min(w, tex.width);
                h = Mathf.Min(h, tex.height);
            }

            outWidth = w == int.MaxValue ? 1 : w;
            outHeight = h == int.MaxValue ? 1 : h;
            return srcPixels;
        }

        private static bool[] GetSRGBFlags(ChannelSource[] sources)
        {
            var flags = new bool[4];
            for (int c = 0; c < 4; c++)
            {
                var tex = sources[c].texture;
                flags[c] = tex != null && IsSRGB(tex);
            }
            return flags;
        }

        private static float GetChannelValue(
            ChannelSource[] sources, Color[][] srcPixels,
            int channel, int x, int y,
            int outWidth, int outHeight,
            bool needLinearConversion, bool isSRGB)
        {
            var src = sources[channel];
            if (src.texture == null || srcPixels[channel] == null)
                return src.defaultColor;

            int texW = src.texture.width;
            int texH = src.texture.height;

            int sx = (int)((float)x / outWidth * texW);
            int sy = (int)((float)y / outHeight * texH);
            sx = Mathf.Clamp(sx, 0, texW - 1);
            sy = Mathf.Clamp(sy, 0, texH - 1);

            float val = srcPixels[channel][sy * texW + sx][src.sourceChannel];

            if (needLinearConversion && isSRGB)
                val = Mathf.GammaToLinearSpace(val);

            return val;
        }

        private static float GetMappedValue(Color pixel, int sourceChannel, bool needLinearConversion, bool isSRGB)
        {
            float val = pixel[sourceChannel];
            if (needLinearConversion && isSRGB)
                val = Mathf.GammaToLinearSpace(val);
            return val;
        }

        // ─── Texture Readability ───────────────────────────────────────

        private static Texture2D EnsureReadable(Texture2D tex)
        {
            if (tex.isReadable)
                return tex;

            var rt = RenderTexture.GetTemporary(
                tex.width, tex.height, 0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB);

            Graphics.Blit(tex, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var copy = new Texture2D(tex.width, tex.height, tex.format, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private static bool IsSRGB(Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path))
                return true; // assume sRGB for procedural textures
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer == null || importer.sRGBTexture;
        }
    }
}
