using System;
using JunkToNeon.Content.Tests;

try
{
    foreach (var result in ContentRegression.Run()) Console.WriteLine("PASS " + result);
    foreach (var result in ChapterRegression.Run()) Console.WriteLine("PASS " + result);
    var fixture = ChapterRegression.Traverse(20261029);
    Console.WriteLine("FIXTURE seed=20261029 actions=" + fixture.Trace.Count + " coins=" + fixture.Coins + " era=" + fixture.Progress.EraId + " owned=" + fixture.Progress.CompletedUpgradeIds.Count + " visitedStates=" + fixture.VisitedStageIds.Count);
    Console.WriteLine("TRACE " + string.Join(",", fixture.Trace));
}
catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
