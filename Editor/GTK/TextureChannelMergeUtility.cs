using UnityEngine;
using UnityEditor;
using System.IO;

namespace GTK
{
    /// <summary>
    /// Configuration for one output channel (R/G/B/A).
    /// </summary>
    [System.Serializable]
    public struct ChannelSource
    {
        public Texture2D texture;
        /// <summary>0=R, 1=G, 2=B, 3=A</summary>
        public int sourceChannel;
        /// <summary>Default value used when texture is null.</summary>
        public float defaultColor;
    }

    public static class TextureChannelMergeUtility
    {
        /// <summary>
        /// Merge up to 4 source channels into a single Texture2D,
        /// then encode and write to disk as PNG.
        /// </summary>
        /// <param name="sources">Array of 4 ChannelSource (index = output channel).</param>
        /// <param name="processInLinear">
        /// If true: convert sRGB inputs to linear, output linear→gamma for PNG.
        /// </param>
        /// <param name="savePath">Full file path to write .png (must end in .png).</param>
        public static void MergeAndSave(ChannelSource[] sources, bool processInLinear, string savePath)
        {
            int outWidth, outHeight;
            var srcPixels = PrepareSources(sources, out outWidth, out outHeight);

            // Build output pixel buffer
            var outPixels = new Color[outWidth * outHeight];
            bool projectIsLinear = IsProjectLinear();
            bool[] isSRGB = new bool[4];

            for (int c = 0; c < 4; c++)
            {
                Texture2D tex = sources[c].texture;
                if (tex != null)
                {
                    string path = AssetDatabase.GetAssetPath(tex);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    isSRGB[c] = importer != null && importer.sRGBTexture;
                }
                else
                {
                    isSRGB[c] = false;
                }
            }

            bool needLinearConversion = processInLinear && projectIsLinear;

            for (int y = 0; y < outHeight; y++)
            {
                for (int x = 0; x < outWidth; x++)
                {
                    int idx = y * outWidth + x;
                    var pixel = new Color(
                        GetChannelValue(sources, srcPixels, 0, x, y, outWidth, outHeight,
                            needLinearConversion, isSRGB[0]),
                        GetChannelValue(sources, srcPixels, 1, x, y, outWidth, outHeight,
                            needLinearConversion, isSRGB[1]),
                        GetChannelValue(sources, srcPixels, 2, x, y, outWidth, outHeight,
                            needLinearConversion, isSRGB[2]),
                        GetChannelValue(sources, srcPixels, 3, x, y, outWidth, outHeight,
                            needLinearConversion, isSRGB[3])
                    );

                    // If processing linear, convert output back to gamma for PNG
                    outPixels[idx] = processInLinear ? pixel.gamma : pixel;
                }
            }

            // Write to texture and save
            var outputTex = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false, false);
            outputTex.SetPixels(outPixels);
            byte[] pngData = outputTex.EncodeToPNG();
            DestroyImmediate(outputTex);

            string dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(savePath, pngData);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Load source textures respecting Read/Write enabled state.
        /// If Read/Write is disabled, create a readable copy via temporary render-texture blit.
        /// Returns the pixel data and sets outWidth/outHeight to the smallest dimensions.
        /// </summary>
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

                // Ensure readable
                var readable = EnsureReadable(tex);
                srcPixels[c] = readable.GetPixels();

                if (readable != tex)
                    DestroyImmediate(readable);

                w = Mathf.Min(w, tex.width);
                h = Mathf.Min(h, tex.height);
            }

            outWidth = w == int.MaxValue ? 1 : w;
            outHeight = h == int.MaxValue ? 1 : h;
            return srcPixels;
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

            // Nearest-neighbour downscale if source is larger than output
            int sx = (int)((float)x / outWidth * texW);
            int sy = (int)((float)y / outHeight * texH);
            sx = Mathf.Clamp(sx, 0, texW - 1);
            sy = Mathf.Clamp(sy, 0, texH - 1);

            int srcIdx = sy * texW + sx;
            float val = srcPixels[channel][srcIdx][src.sourceChannel];

            // If processing in linear and texture is sRGB, convert gamma→linear
            if (needLinearConversion && isSRGB)
                val = Mathf.GammaToLinearSpace(val);

            return val;
        }

        /// <summary>
        /// Create a readable copy of a texture if it doesn't have Read/Write enabled.
        /// </summary>
        private static Texture2D EnsureReadable(Texture2D tex)
        {
            if (tex.isReadable)
                return tex;

            // Blit to readable RenderTexture, then read back
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
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

        public static bool IsProjectLinear()
        {
            return PlayerSettings.colorSpace == ColorSpace.Linear;
        }
    }
}
