using System;
using JunkToNeon.Orders.Tests;

try { foreach (string result in OrdersRegression.Run()) Console.WriteLine("PASS " + result); }
catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
