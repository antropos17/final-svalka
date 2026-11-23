#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
namespace JunkToNeon.Tests.Upgrades
{
    public sealed class DeliveryPriceRegressionUnity
    {
        [Test] public void DeliveryTier2RevisionCompatibility()
        { Assert.That(DeliveryPriceRegression.Run().Count, Is.EqualTo(3)); }
    }
}
#endif
