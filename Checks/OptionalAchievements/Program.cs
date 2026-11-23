using System;
using JunkToNeon.Tests;

try
{
    var cases=OptionalAchievementRegression.Run(System.IO.Directory.GetCurrentDirectory());
    foreach(var item in cases)Console.WriteLine("PASS "+item);
    Console.WriteLine("PASS "+cases.Count+" optional achievement groups");
}
catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
