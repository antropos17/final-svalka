using System;
using JunkToNeon.Tests.Upgrades;
var results = UpgradeRegression.Run();
foreach (var result in results) Console.WriteLine("PASS " + result);
Console.WriteLine("PASS " + results.Count + " upgrade regression groups");
