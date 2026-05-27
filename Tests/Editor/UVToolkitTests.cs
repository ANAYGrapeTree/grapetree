using UnityEngine;
using NUnit.Framework;
using GTK.UVToolkit;

namespace GTK.Tests
{
    public class UVToolkitTests
    {
        // ─── Test helpers ──────────────────────────────────────────────

        private static Mesh MakeTestMesh(Vector3[] verts, Vector2[] uv, int[] tris)
        {
            var m = new Mesh { vertices = verts, uv = uv, triangles = tris };
            m.RecalculateNormals();
            return m;
        }

        // ═══════════════════════════════════════════════════════════════
        //  UVToolkitUtility
        // ═══════════════════════════════════════════════════════════════

        // ─── EvaluateHeatColor ─────────────────────────────────────────

        [Test]
        public void EvaluateHeatColor_Zero_ReturnsBlue()
        {
            Assert.AreEqual(Color.blue, UVToolkitUtility.EvaluateHeatColor(0f));
        }

        [Test]
        public void EvaluateHeatColor_One_ReturnsRed()
        {
            Assert.AreEqual(Color.red, UVToolkitUtility.EvaluateHeatColor(1f));
        }

        [Test]
        public void EvaluateHeatColor_Half_ReturnsYellow()
        {
            Assert.AreEqual(Color.yellow, UVToolkitUtility.EvaluateHeatColor(0.5f));
        }

        [Test]
        public void EvaluateHeatColor_ClampsBelowZero()
        {
            Assert.AreEqual(Color.blue, UVToolkitUtility.EvaluateHeatColor(-0.5f));
        }

        [Test]
        public void EvaluateHeatColor_ClampsAboveOne()
        {
            Assert.AreEqual(Color.red, UVToolkitUtility.EvaluateHeatColor(1.5f));
        }

        // ─── SutherlandHodgmanArea ─────────────────────────────────────

        [Test]
        public void SutherlandHodgmanArea_NoOverlap_ReturnsZero()
        {
            var a = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
            var b = new[] { new Vector2(2, 2), new Vector2(3, 2), new Vector2(2, 3) };
            Assert.AreEqual(0f, UVToolkitUtility.SutherlandHodgmanArea(a, b), 0.0001f);
        }

        [Test]
        public void SutherlandHodgmanArea_IdenticalTriangles_ReturnsTriangleArea()
        {
            var a = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
            Assert.AreEqual(0.5f, UVToolkitUtility.SutherlandHodgmanArea(a, a), 0.0001f);
        }

        [Test]
        public void SutherlandHodgmanArea_PartialOverlap_ReturnsReasonableArea()
        {
            var a = new[] { new Vector2(0, 0), new Vector2(2, 0), new Vector2(0, 2) };
            var b = new[] { new Vector2(1, 0), new Vector2(2, 1), new Vector2(0, 2) };
            float area = UVToolkitUtility.SutherlandHodgmanArea(a, b);
            Assert.Greater(area, 0f);
            Assert.Less(area, 1.5f);
        }

        [Test]
        public void SutherlandHodgmanArea_TouchingAtEdge_ReturnsZero()
        {
            var a = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
            var b = new[] { new Vector2(1, 0), new Vector2(2, 0), new Vector2(1, 1) };
            Assert.AreEqual(0f, UVToolkitUtility.SutherlandHodgmanArea(a, b), 0.0001f);
        }

        // ═══════════════════════════════════════════════════════════════
        //  CheckerModule
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void GenerateCheckerTexture_Size_ReturnsCorrectDimensions()
        {
            var tex = CheckerModule.GenerateCheckerTexture(1024, 8, Color.white, Color.black);
            Assert.AreEqual(1024, tex.width);
            Assert.AreEqual(1024, tex.height);
            Assert.AreEqual(TextureFormat.RGBA32, tex.format);
        }

        [Test]
        public void GenerateCheckerTexture_FirstCellWhite_SecondCellBlack()
        {
            var tex = CheckerModule.GenerateCheckerTexture(16, 4, Color.white, Color.black);
            var pixels = tex.GetPixels();
            Assert.AreEqual(Color.white, pixels[0]);
            Assert.AreEqual(Color.black, pixels[16 / 4]); // first pixel of second column
        }

        // ═══════════════════════════════════════════════════════════════
        //  OverlapDetectionModule
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void OverlapDetection_NoOverlap_ReturnsZero()
        {
            var mesh = MakeTestMesh(
                new[] { Vector3.zero, Vector3.right, Vector3.up },
                new[] { new Vector2(0, 0), new Vector2(0.1f, 0), new Vector2(0, 0.1f) },
                new[] { 0, 1, 2 }
            );
            var mod = new OverlapDetectionModule();
            mod.SetTargetMesh(mesh);
            mod.DetectOverlaps(0.001f);
            Assert.AreEqual(0, mod.OverlapCount);
        }

        [Test]
        public void OverlapDetection_WithOverlap_DetectsCorrectly()
        {
            var mesh = MakeTestMesh(
                new Vector3[6] { Vector3.zero, Vector3.right, Vector3.up, Vector3.zero, Vector3.right, Vector3.up },
                new Vector2[6]
                {
                    new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0, 0.5f),
                    new Vector2(0.1f, 0.1f), new Vector2(0.4f, 0.1f), new Vector2(0.1f, 0.4f)
                },
                new[] { 0, 1, 2, 3, 4, 5 }
            );
            var mod = new OverlapDetectionModule();
            mod.SetTargetMesh(mesh);
            mod.DetectOverlaps(0.001f);
            Assert.Greater(mod.OverlapCount, 0);
        }

        // ═══════════════════════════════════════════════════════════════
        //  TexelDensityModule
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void TexelDensity_SingleTriangle_ReturnsTextureSize()
        {
            var mesh = MakeTestMesh(
                new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) },
                new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) },
                new[] { 0, 1, 2 }
            );
            var mod = new TexelDensityModule();
            mod.SetTargetMesh(mesh);
            mod.Analyze(1024, false);

            // area3D=0.5, areaUV=0.5, TD=1024*sqrt(0.5/0.5)=1024
            Assert.AreEqual(1024f, mod.AverageDensity, 1f);
        }

        [Test]
        public void TexelDensity_UniformDensity_ZeroDeviation()
        {
            var mesh = MakeTestMesh(
                new Vector3[6]
                {
                    new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1),
                    new Vector3(2, 0, 0), new Vector3(3, 0, 0), new Vector3(2, 0, 1)
                },
                new Vector2[6]
                {
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1),
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1)
                },
                new[] { 0, 1, 2, 3, 4, 5 }
            );
            var mod = new TexelDensityModule();
            mod.SetTargetMesh(mesh);
            mod.Analyze(1024, false);
            Assert.AreEqual(1024f, mod.AverageDensity, 1f);
            Assert.AreEqual(0f, mod.MaxDensity - mod.MinDensity, 0.1f);
        }

        [Test]
        public void TexelDensity_NotAnalyzed_HasNoTriangles()
        {
            var mod = new TexelDensityModule();
            Assert.AreEqual(0, mod.TriCount);
        }
    }
}
