using System;
using JunkToNeon.Tests;

var results = GuidanceRegression.Run();
foreach (string result in results) Console.WriteLine("PASS " + result);
Console.WriteLine("PASS " + results.Count + " guidance regression groups");
