#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace JunkToNeon.Tests
{
    public sealed class EffectsRegressionUnity
    {
        [Test] public void JunkToNeonEffectsBehavior() { Assert.That(EffectsRegression.Run().Count, Is.EqualTo(11)); }
    }
}
#endif
