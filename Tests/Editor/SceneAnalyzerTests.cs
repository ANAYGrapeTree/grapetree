using UnityEngine;
using NUnit.Framework;
using GTK.SceneAnalyzer;

namespace GTK.Tests
{
    public class SceneAnalyzerTests
    {
        [Test]
        public void GetHeatColor_Zero_ReturnsGreen()
        {
            Assert.AreEqual(Color.green, SceneAnalyzerUtility.GetHeatColor(0f, 0f, 1f));
        }

        [Test]
        public void GetHeatColor_One_ReturnsRed()
        {
            Assert.AreEqual(Color.red, SceneAnalyzerUtility.GetHeatColor(1f, 0f, 1f));
        }

        [Test]
        public void GetHeatColor_Mid_ReturnsYellow()
        {
            var c = SceneAnalyzerUtility.GetHeatColor(0.5f, 0f, 1f);
            Assert.AreEqual(Color.yellow, c);
        }

        [Test]
        public void AnalyzeScene_EmptyScene_ReturnsEmpty()
        {
            // Should not throw and return a list
            var results = SceneAnalyzerUtility.AnalyzeScene();
            Assert.IsNotNull(results);
        }
    }
}
