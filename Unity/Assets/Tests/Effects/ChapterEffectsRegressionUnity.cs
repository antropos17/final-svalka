#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace JunkToNeon.Tests
{
    public sealed class ChapterEffectsRegressionUnity
    {
        [Test] public void JunkToNeonChapterEffectsBehavior() { Assert.That(ChapterEffectsRegression.Run().Count, Is.EqualTo(10)); }
    }
}
#endif
