#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace JunkToNeon.Tests
{
    public sealed class GuidanceRegressionUnity
    {
        [Test] public void JunkToNeonGuidanceBehavior() { Assert.That(GuidanceRegression.Run().Count, Is.EqualTo(10)); }
        [Test] public void JunkToNeonLateFirstRepairKeepsIdentityAndHonestAdvice() { Assert.That(GuidanceRegression.RunLateFirstRepair().Count, Is.EqualTo(3)); }
    }
}
#endif
