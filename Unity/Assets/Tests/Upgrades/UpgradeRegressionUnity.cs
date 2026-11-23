#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
namespace JunkToNeon.Tests.Upgrades
{
    public sealed class UpgradeRegressionUnity
    {
        [Test] public void PureShopAndRewardBehavior()
        { Assert.That(UpgradeRegression.Run().Count, Is.EqualTo(8)); }
    }
}
#endif
