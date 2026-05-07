using UnityEngine;
using NUnit.Framework;
using GTK.VertexPaint;

namespace GTK.Tests
{
    public class VertexPaintTests
    {
        // ─── LinearFalloff ───────────────────────────────────────────────

        [Test]
        public void LinearFalloff_AtCenter_ReturnsOne()
        {
            Assert.AreEqual(1f, VertexPaintUtility.LinearFalloff(0f, 1f), 0.0001f);
        }

        [Test]
        public void LinearFalloff_AtEdge_ReturnsZero()
        {
            Assert.AreEqual(0f, VertexPaintUtility.LinearFalloff(1f, 1f), 0.0001f);
        }

        [Test]
        public void LinearFalloff_BeyondEdge_ReturnsZero()
        {
            Assert.AreEqual(0f, VertexPaintUtility.LinearFalloff(2f, 1f), 0.0001f);
        }

        [Test]
        public void LinearFalloff_HalfDistance_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, VertexPaintUtility.LinearFalloff(0.5f, 1f), 0.0001f);
        }

        // ─── BlendColor ──────────────────────────────────────────────────

        [Test]
        public void BlendColor_FullFalloff_ReturnsTarget()
        {
            var result = VertexPaintUtility.BlendColor(Color.black, Color.white, 1f);
            Assert.AreEqual(Color.white, result);
        }

        [Test]
        public void BlendColor_ZeroFalloff_ReturnsSource()
        {
            var result = VertexPaintUtility.BlendColor(Color.black, Color.white, 0f);
            Assert.AreEqual(Color.black, result);
        }

        [Test]
        public void BlendColor_HalfFalloff_ReturnsMidpoint()
        {
            var result = VertexPaintUtility.BlendColor(Color.black, Color.white, 0.5f);
            Assert.AreEqual(0.5f, result.r, 0.001f);
            Assert.AreEqual(0.5f, result.g, 0.001f);
            Assert.AreEqual(0.5f, result.b, 0.001f);
            Assert.AreEqual(0.5f, result.a, 0.001f);
        }

        // ─── BlendChannel ────────────────────────────────────────────────

        [Test]
        public void BlendChannel_FullFalloff_SetsChannel()
        {
            var src = new Color(0f, 0.5f, 0f, 0f);
            var result = VertexPaintUtility.BlendChannel(src, 1f, 1f, 0);
            Assert.AreEqual(1f, result.r, 0.001f);
            Assert.AreEqual(0.5f, result.g, 0.001f);
        }

        [Test]
        public void BlendChannel_ZeroFalloff_PreservesChannel()
        {
            var src = new Color(0.5f, 0f, 0f, 0f);
            var result = VertexPaintUtility.BlendChannel(src, 1f, 0f, 0);
            Assert.AreEqual(0.5f, result.r, 0.001f);
        }

        [Test]
        public void BlendChannel_ChannelA_CorrectIndex()
        {
            var src = new Color(0f, 0f, 0f, 0f);
            var result = VertexPaintUtility.BlendChannel(src, 0.75f, 1f, 3);
            Assert.AreEqual(0.75f, result.a, 0.001f);
        }

        // ─── BuildAdjacency ──────────────────────────────────────────────

        [Test]
        public void BuildAdjacency_SingleTriangle_EachVertexHasTwoNeighbors()
        {
            var tris = new int[] { 0, 1, 2 };
            var adj = VertexPaintUtility.BuildAdjacency(tris, 3);

            Assert.AreEqual(3, adj.Length);
            Assert.IsNotNull(adj[0]);
            Assert.IsNotNull(adj[1]);
            Assert.IsNotNull(adj[2]);
            Assert.AreEqual(2, adj[0].Count);
            Assert.IsTrue(adj[0].Contains(1));
            Assert.IsTrue(adj[0].Contains(2));
        }

        [Test]
        public void BuildAdjacency_TwoTriangles_SharedEdgeHasTwoNeighbors()
        {
            var tris = new int[] { 0, 1, 2, 0, 2, 3 };
            var adj = VertexPaintUtility.BuildAdjacency(tris, 4);

            Assert.AreEqual(4, adj.Length);
            Assert.IsTrue(adj[0].Contains(1));
            Assert.IsTrue(adj[0].Contains(2));
            Assert.IsTrue(adj[0].Contains(3));
        }

        // ─── FloodColors ─────────────────────────────────────────────────

        [Test]
        public void FloodColors_RGBA_AllVerticesSet()
        {
            var colors = new Color[10];
            VertexPaintUtility.FloodColors(colors, Color.cyan, PaintChannel.RGBA, 0f);

            foreach (var c in colors)
                Assert.AreEqual(Color.cyan, c);
        }

        [Test]
        public void FloodColors_SingleChannel_OnlyThatChannelChanged()
        {
            var colors = new Color[10];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color(0f, 0f, 0f, 1f);

            VertexPaintUtility.FloodColors(colors, Color.clear, PaintChannel.A, 0.5f);

            foreach (var c in colors)
            {
                Assert.AreEqual(0f, c.r, 0.001f);
                Assert.AreEqual(0.5f, c.a, 0.001f);
            }
        }

        [Test]
        public void FloodColors_SmoothDoesNothing()
        {
            var colors = new Color[5];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color(0.3f, 0.3f, 0.3f, 0.3f);

            VertexPaintUtility.FloodColors(colors, Color.white, PaintChannel.Smooth, 1f);

            foreach (var c in colors)
                Assert.AreEqual(0.3f, c.r, 0.001f);
        }

        // ─── RaycastMesh ─────────────────────────────────────────────────

        [Test]
        public void RaycastMesh_DirectHit_ReturnsTrue()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-1, 0, 1),
                new Vector3( 1, 0, 1),
                new Vector3( 0, 0, -1),
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.RecalculateNormals();

            var ray = new Ray(new Vector3(0, 10, 0), Vector3.down);
            RaycastHit hit;
            bool result = VertexPaintUtility.RaycastMesh(ray, mesh, Matrix4x4.identity, out hit);

            Assert.IsTrue(result);
            Assert.Greater(hit.distance, 0f);
            Assert.AreEqual(0f, hit.point.y, 0.001f);
        }

        [Test]
        public void RaycastMesh_Miss_ReturnsFalse()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-1, 0, 1),
                new Vector3( 1, 0, 1),
                new Vector3( 0, 0, -1),
            };
            mesh.triangles = new int[] { 0, 1, 2 };

            var ray = new Ray(new Vector3(0, 10, 0), Vector3.right);
            RaycastHit hit;
            bool result = VertexPaintUtility.RaycastMesh(ray, mesh, Matrix4x4.identity, out hit);

            Assert.IsFalse(result);
        }
    }
}
