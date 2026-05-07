using UnityEngine;
using NUnit.Framework;
using GTK;

namespace GTK.Tests
{
    public class TextureChannelMergeTests
    {
        private const int TexSize = 4;

        private static Texture2D MakeTestTexture(Color[] pixels, int size = TexSize)
        {
            // Use RGBAFloat to avoid 8-bit quantization in test textures
            var tex = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Color[] MakePixels(float r, float g, float b, float a)
        {
            var cols = new Color[TexSize * TexSize];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(r, g, b, a);
            return cols;
        }

        // ─── SwizzleChannels ─────────────────────────────────────────────

        [Test]
        public void SwizzleChannels_Identity_OutputMatchesInput()
        {
            var src = MakeTestTexture(MakePixels(0.3f, 0.6f, 0.9f, 1.0f));
            var ops = new[] { SwizzleOp.SourceR, SwizzleOp.SourceG, SwizzleOp.SourceB, SwizzleOp.SourceA };
            var result = TextureChannelMergeUtility.SwizzleChannels(src, ops, new[] { 0f, 0f, 0f, 0f }, false);

            var pixels = result.GetPixels();
            foreach (var p in pixels)
            {
                Assert.AreEqual(0.3f, p.r, 0.01f);
                Assert.AreEqual(0.6f, p.g, 0.01f);
                Assert.AreEqual(0.9f, p.b, 0.01f);
                Assert.AreEqual(1.0f, p.a, 0.01f);
            }
        }

        [Test]
        public void SwizzleChannels_SwapRG_ChannelsSwapped()
        {
            var src = MakeTestTexture(MakePixels(0.2f, 0.8f, 0.5f, 1.0f));
            var ops = new[] { SwizzleOp.SourceG, SwizzleOp.SourceR, SwizzleOp.SourceB, SwizzleOp.SourceA };
            var result = TextureChannelMergeUtility.SwizzleChannels(src, ops, new[] { 0f, 0f, 0f, 0f }, false);

            var pixels = result.GetPixels();
            foreach (var p in pixels)
            {
                Assert.AreEqual(0.8f, p.r, 0.01f);
                Assert.AreEqual(0.2f, p.g, 0.01f);
            }
        }

        [Test]
        public void SwizzleChannels_ZeroConstant_OutputBlack()
        {
            var src = MakeTestTexture(MakePixels(0.5f, 0.5f, 0.5f, 0.5f));
            var ops = new[] { SwizzleOp.Zero, SwizzleOp.Zero, SwizzleOp.Zero, SwizzleOp.Zero };
            var result = TextureChannelMergeUtility.SwizzleChannels(src, ops, new[] { 0f, 0f, 0f, 0f }, false);

            foreach (var p in result.GetPixels())
                Assert.AreEqual(0f, p.r + p.g + p.b + p.a, 0.01f);
        }

        [Test]
        public void SwizzleChannels_OneConstant_OutputWhite()
        {
            var src = MakeTestTexture(MakePixels(0f, 0f, 0f, 0f));
            var ops = new[] { SwizzleOp.One, SwizzleOp.One, SwizzleOp.One, SwizzleOp.One };
            var result = TextureChannelMergeUtility.SwizzleChannels(src, ops, new[] { 0f, 0f, 0f, 0f }, false);

            foreach (var p in result.GetPixels())
            {
                Assert.AreEqual(1f, p.r, 0.01f);
                Assert.AreEqual(1f, p.g, 0.01f);
                Assert.AreEqual(1f, p.b, 0.01f);
                Assert.AreEqual(1f, p.a, 0.01f);
            }
        }

        [Test]
        public void SwizzleChannels_GrayConstant_HalfGray()
        {
            var src = MakeTestTexture(MakePixels(0f, 0f, 0f, 0f));
            var ops = new[] { SwizzleOp.Gray, SwizzleOp.Gray, SwizzleOp.Gray, SwizzleOp.Gray };
            var result = TextureChannelMergeUtility.SwizzleChannels(src, ops, new[] { 0f, 0f, 0f, 0f }, false);

            foreach (var p in result.GetPixels())
            {
                Assert.AreEqual(0.5f, p.r, 0.01f);
                Assert.AreEqual(0.5f, p.a, 0.01f);
            }
        }

        [Test]
        public void SwizzleChannels_CustomValue_Applied()
        {
            var src = MakeTestTexture(MakePixels(0f, 0f, 0f, 0f));
            var ops = new[] { SwizzleOp.Custom, SwizzleOp.Custom, SwizzleOp.Custom, SwizzleOp.Custom };
            var result = TextureChannelMergeUtility.SwizzleChannels(src, ops, new[] { 0.75f, 0.75f, 0.75f, 0.75f }, false);

            foreach (var p in result.GetPixels())
            {
                Assert.AreEqual(0.75f, p.r, 0.01f);
                Assert.AreEqual(0.75f, p.a, 0.01f);
            }
        }

        [Test]
        public void SwizzleChannels_InverseR_ValuesInverted()
        {
            var src = MakeTestTexture(MakePixels(0.3f, 0.6f, 0.9f, 1.0f));
            var ops = new[] { SwizzleOp.InverseR, SwizzleOp.SourceG, SwizzleOp.SourceB, SwizzleOp.SourceA };
            var result = TextureChannelMergeUtility.SwizzleChannels(src, ops, new[] { 0f, 0f, 0f, 0f }, false);

            foreach (var p in result.GetPixels())
                Assert.AreEqual(0.7f, p.r, 0.01f);
        }

        // ─── MergePreview ────────────────────────────────────────────────

        [Test]
        public void MergePreview_SingleChannel_CorrectOutputSize()
        {
            var src = MakeTestTexture(MakePixels(0.5f, 0.5f, 0.5f, 0.5f));
            var sources = new ChannelSource[4];
            sources[0] = new ChannelSource { texture = src, sourceChannel = 0, defaultColor = 0f };
            sources[1] = new ChannelSource { texture = null, sourceChannel = 0, defaultColor = 0.5f };
            sources[2] = new ChannelSource { texture = null, sourceChannel = 0, defaultColor = 0f };
            sources[3] = new ChannelSource { texture = null, sourceChannel = 0, defaultColor = 1f };

            var result = TextureChannelMergeUtility.MergePreview(sources, false);
            Assert.AreEqual(TexSize, result.width);
            Assert.AreEqual(TexSize, result.height);

            var pixels = result.GetPixels();
            foreach (var p in pixels)
            {
                Assert.AreEqual(0.5f, p.r, 0.01f);
                Assert.AreEqual(0.5f, p.g, 0.01f);
                Assert.AreEqual(0.0f, p.b, 0.01f);
                Assert.AreEqual(1.0f, p.a, 0.01f);
            }
        }

        [Test]
        public void MergePreview_NoTextureUsesDefault_DefaultValuesApplied()
        {
            var sources = new ChannelSource[4];
            sources[0] = new ChannelSource { texture = null, sourceChannel = 0, defaultColor = 1f };
            sources[1] = new ChannelSource { texture = null, sourceChannel = 0, defaultColor = 0f };
            sources[2] = new ChannelSource { texture = null, sourceChannel = 0, defaultColor = 0.5f };
            sources[3] = new ChannelSource { texture = null, sourceChannel = 0, defaultColor = 1f };

            var result = TextureChannelMergeUtility.MergePreview(sources, false);
            foreach (var p in result.GetPixels())
            {
                Assert.AreEqual(1.0f, p.r, 0.01f);
                Assert.AreEqual(0.0f, p.g, 0.01f);
                Assert.AreEqual(0.5f, p.b, 0.01f);
                Assert.AreEqual(1.0f, p.a, 0.01f);
            }
        }

        // ─── Format ──────────────────────────────────────────────────────

        [Test]
        public void GetExtension_AllFormats_ReturnsCorrectExtension()
        {
            Assert.AreEqual(".png", TextureChannelMergeUtility.GetExtension(SaveFormat.PNG));
            Assert.AreEqual(".jpg", TextureChannelMergeUtility.GetExtension(SaveFormat.JPG));
            Assert.AreEqual(".tga", TextureChannelMergeUtility.GetExtension(SaveFormat.TGA));
        }
    }
}
