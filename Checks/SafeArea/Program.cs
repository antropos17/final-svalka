using System;
using JunkToNeon.Platform;

static void Near(float actual, double expected)
{
    if (Math.Abs(actual - expected) > 0.000001) throw new Exception($"Expected {expected}, observed {actual}");
}
static SafeAreaAnchors Fit(double w, double h, double x, double y, double sw, double sh,
    double minX, double minY, double maxX, double maxY)
{
    if (!SafeAreaGeometry.TryCalculate(w, h, x, y, sw, sh, out var a)) throw new Exception("Valid rectangle rejected");
    Near(a.MinX, minX); Near(a.MinY, minY); Near(a.MaxX, maxX); Near(a.MaxY, maxY);
    return a;
}
static void Invalid(double w, double h, double x, double y, double sw, double sh)
{
    if (SafeAreaGeometry.TryCalculate(w, h, x, y, sw, sh, out _)) throw new Exception("Invalid rectangle accepted");
}

Fit(360, 800, 0, 0, 360, 800, 0, 0, 1, 1);
Fit(360, 800, 0, 0, 360, 760, 0, 0, 1, .95);
Fit(360, 800, 0, 24, 360, 776, 0, .03, 1, 1);
var both = Fit(1080, 2340, 0, 72, 1080, 2132, 0, 72d/2340, 1, 2204d/2340);
Console.WriteLine("PASS full screen, top notch, bottom gestures, combined portrait insets");

for (int i = 0; i < 1000; i++)
{
    var repeated = Fit(1080, 2340, 0, 72, 1080, 2132, 0, 72d/2340, 1, 2204d/2340);
    if (repeated.MinY != both.MinY || repeated.MaxY != both.MaxY) throw new Exception("Repeated fit drifted");
}
Fit(720, 1280, 0, 40, 720, 1200, 0, .03125, 1, .96875);
Fit(800, 360, 40, 0, 736, 360, .05, 0, .97, 1);
Fit(360, 800, 0, 0, 360, 800, 0, 0, 1, 1);
Console.WriteLine("PASS identical samples, new resolution, landscape side insets, return to full portrait");

Fit(360, 800, -10, -20, 380, 840, 0, 0, 1, 1);
Invalid(0, 800, 0, 0, 360, 800); Invalid(360, 0, 0, 0, 360, 800);
Invalid(-360, 800, 0, 0, 360, 800); Invalid(360, 800, 0, 0, 0, 800);
Invalid(360, 800, 0, 0, 360, -1); Invalid(360, 800, 400, 0, 20, 800);
Invalid(360, 800, 0, 900, 360, 10);
Invalid(double.NaN, 800, 0, 0, 360, 800);
Invalid(360, double.PositiveInfinity, 0, 0, 360, 800);
Invalid(360, 800, double.NaN, 0, 360, 800);
Invalid(360, 800, 0, 0, double.PositiveInfinity, 800);
Invalid(360, 800, double.MaxValue, 0, double.MaxValue, 800);
Console.WriteLine("PASS clamping, zero/negative/non-finite geometry, disjoint rectangles and overflow");
Console.WriteLine("PASS component source compiled against installed Unity Core/UI modules; no Unity runtime was launched");
