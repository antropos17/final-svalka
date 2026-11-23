#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
namespace JunkToNeon.Tests.Upgrades
{
    public sealed class PricingRegressionUnity
    {
        [Test] public void VersionedPricingCompatibility()
        { Assert.That(PricingRegression.Run().Count, Is.EqualTo(6)); }
    }
}
#endif
