#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using JunkToNeon.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace JunkToNeon.Tests.BackNavigation
{
    [Category("Expansion07")]
    public sealed class PausedBackBoundaryTests
    {
        [TestCase(BackContext.Root, 0, 0, 1)]
        [TestCase(BackContext.DismissiblePanel, 1, 0, 0)]
        [TestCase(BackContext.RestoreConfirmation, 0, 1, 0)]
        [TestCase(BackContext.BlockingError, 0, 0, 0)]
        public void PausedRequestDoesNothingAndFreshResumeRoutesOnce(BackContext context,
            int expectedCloses, int expectedCancels, int expectedRoots)
        {
            var host = new GameObject("Private paused Back boundary");
            try
            {
                var back = host.AddComponent<JtnBackNavigation>();
                int reads = 0, closes = 0, cancels = 0, roots = 0, stops = 0;
                back.Configure(() => { reads++; return context; }, () => closes++, () => cancels++,
                    () => roots++, () => stops++);

                // Synthetic component lifecycle only: no OS pause, Editor setting,
                // scene, economic command, ordinary save or input device is touched.
                host.SendMessage("OnApplicationPause", true, SendMessageOptions.DontRequireReceiver);
                Assert.That(back.RequestBack(), Is.False, "Back must refuse while the application is paused");
                Assert.That(new[] { reads, closes, cancels, roots, stops }, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }),
                    "A paused request must not query context or dispatch even StopAuto");

                host.SendMessage("OnApplicationPause", false);
                Assert.That(back.RequestBack(), Is.True, "A refused request must not consume the resumed frame");
                Assert.That(back.RequestBack(), Is.False, "Same-frame duplicate must not reach a changed context");
                Assert.That(new[] { reads, closes, cancels, roots, stops },
                    Is.EqualTo(new[] { 1, expectedCloses, expectedCancels, expectedRoots, 1 }));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
#endif
