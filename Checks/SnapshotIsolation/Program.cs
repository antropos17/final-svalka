using System;
using JunkToNeon.Tests;

foreach (string result in SnapshotIsolationRegression.Run()) Console.WriteLine("PASS " + result);
