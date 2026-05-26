using UnityEngine;
using NUnit.Framework;
using GTK.AssetTracker;

namespace GTK.Tests
{
    public class AssetTrackerTests
    {
        [Test]
        public void BuildReferenceMap_DoesNotThrow()
        {
            var map = AssetTrackerUtility.BuildReferenceMap();
            Assert.IsNotNull(map);
        }

        [Test]
        public void GetAllTexturePaths_ReturnsPaths()
        {
            var paths = AssetTrackerUtility.GetAllTexturePaths();
            Assert.IsNotNull(paths);
        }

        [Test]
        public void FindUnusedTextures_DoesNotThrow()
        {
            var unused = AssetTrackerUtility.FindUnusedTextures();
            Assert.IsNotNull(unused);
        }
    }
}
