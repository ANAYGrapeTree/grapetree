using UnityEngine;
using NUnit.Framework;
using GTK.TextureBaker;

namespace GTK.Tests
{
    public class TextureBakerTests
    {
        private static Texture2D MakeGrayTexture(float value, int size = 8)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(value, value, value, 1f);
            tex.SetPixels(pixels); tex.Apply();
            return tex;
        }

        private static Texture2D MakeNormalMap(float nx, float ny, float nz, int size = 8)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var pixels = new Color[size * size];
            var c = new Color(nx * 0.5f + 0.5f, ny * 0.5f + 0.5f, nz * 0.5f + 0.5f, 1f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels(pixels); tex.Apply();
            return tex;
        }

        [Test]
        public void HeightToNormal_FlatHeight_ReturnsFlatNormal()
        {
            var h = MakeGrayTexture(0.5f);
            var n = TextureBakerUtility.HeightToNormal(h, 4f);
            Assert.AreEqual(8, n.width);
            // Flat height → normals should be (0.5, 0.5, 1.0) encoded
            var p = n.GetPixels();
            Assert.AreEqual(0.5f, p[0].r, 0.02f);
            Assert.AreEqual(0.5f, p[0].g, 0.02f);
        }

        [Test]
        public void NormalToHeight_FlatNormal_ReturnsFlat()
        {
            var n = MakeNormalMap(0f, 0f, 1f);
            var h = TextureBakerUtility.NormalToHeight(n);
            var p = h.GetPixels();
            Assert.AreEqual(p[0], p[^1]);
        }

        [Test]
        public void ConvertNormalFormat_GLtoDX_InvertsGreen()
        {
            var n = MakeNormalMap(0f, 1f, 0f); // Y-up (GL)
            var dx = TextureBakerUtility.ConvertNormalFormat(n, true);
            var p = dx.GetPixels();
            Assert.AreEqual(0f, p[0].g, 0.01f); // Y-down => 0.0
        }

        [Test]
        public void ConvertNormalFormat_DXtoGL_RestoresGreen()
        {
            var n = MakeNormalMap(0f, 0.5f, 0f); // Y-down (DX)
            var gl = TextureBakerUtility.ConvertNormalFormat(n, false);
            var p = gl.GetPixels();
            Assert.AreEqual(0.25f, p[0].g, 0.01f); // Y-up => 0.25
        }

        [Test]
        public void AOFromNormal_FlatNormal_ReturnsHalfAO()
        {
            var n = MakeNormalMap(0f, 0f, 1f);
            var ao = TextureBakerUtility.AOFromNormal(n, 1f);
            var p = ao.GetPixels();
            Assert.AreEqual(1f, p[0].r, 0.01f); // flat → fully upward → full AO
        }

        [Test]
        public void AODilate_PreservesDimensions()
        {
            var ao = MakeGrayTexture(0.5f);
            var dilated = TextureBakerUtility.AODilate(ao, 3);
            Assert.AreEqual(8, dilated.width);
            Assert.AreEqual(8, dilated.height);
        }
    }
}
