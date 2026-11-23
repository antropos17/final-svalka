#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace JunkToNeon.Tests
{
    public sealed class OptionalAchievementRegressionUnity
    {
        [Test] public void JunkToNeonOptionalAchievementsAreCurrentFactsOnly()
        { Assert.That(OptionalAchievementRegression.Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../.."))).Count,Is.EqualTo(5)); }
    }
}
#endif
