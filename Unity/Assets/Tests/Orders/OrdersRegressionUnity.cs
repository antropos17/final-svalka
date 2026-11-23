#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
namespace JunkToNeon.Orders.Tests
{
    public sealed class OrdersRegressionUnity
    {
        [Test] public void RepeatablePostChapterOrders() { Assert.That(OrdersRegression.Run().Count, Is.EqualTo(12)); }
    }
}
#endif
