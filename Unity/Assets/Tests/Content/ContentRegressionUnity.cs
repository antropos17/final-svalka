#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace JunkToNeon.Content.Tests
{
    public sealed class ContentRegressionUnity
    {
        [Test] public void CatalogAndLegacyProjection() { Assert.That(ContentRegression.Run().Count, Is.EqualTo(19)); }
        [Test] public void TwoEraChapterProgression() { Assert.That(ChapterRegression.Run().Count, Is.EqualTo(11)); }
    }
}
#endif
