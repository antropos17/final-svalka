#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace JunkToNeon.Tests
{
    public sealed class CoreRegressionUnity
    {
        [Test] public void EconomyAndPersistenceBehavior()
        {
            var directory = Path.Combine(UnityEngine.Application.temporaryCachePath, "jtn-core-regression-" + Guid.NewGuid().ToString("N"));
            try { Assert.That(CoreRegression.Run(directory).Count, Is.EqualTo(49)); }
            finally
            {
                foreach (var name in new[] { "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json" })
                { var path = Path.Combine(directory, name); if (File.Exists(path)) File.Delete(path); }
                if (Directory.Exists(directory)) Directory.Delete(directory, false);
            }
        }
    }
}
#endif
