using System;
using System.Linq;
using JunkToNeon.SaveInspector;

try
{
    if (args.Length != 2 && !(args.Length == 3 && (args[2] == "r11" || args[2] == "arcade"))) throw new ArgumentException("Expected explicit repository and new private workspace, optionally r11 or arcade.");
    var cases = args.Length == 3 ? args[2] == "arcade" ? ArcadeChecks.Run(args[0], args[1]) : CurrentRegressions.Run(args[0], args[1]) : FixRegressions.Run(args[0], args[1]);
    using (var output = Console.OpenStandardOutput()) output.Write(Report.Json(new Obj().Add("cases", cases.Cast<object>().ToList())));
    return cases.All(c => (bool)c.Get("passed")) ? 0 : 7;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
