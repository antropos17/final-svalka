#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace JunkToNeon.Tests
{
    public sealed class MilestoneRegressionUnity
    {
        [Test] public void JunkToNeonMilestoneFactsRemainReadOnly()
        {
            string fixtures=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures"));
            Assert.That(MilestoneRegression.Run(fixtures).Count,Is.EqualTo(6));
        }
    }
}
#endif
