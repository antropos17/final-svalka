using System;
using System.IO;
using JunkToNeon.Tests;

try
{
    string fixtures=Path.GetFullPath(args.Length==0?"Checks/Milestones/fixtures":args[0]);
    var results=MilestoneRegression.Run(fixtures);
    foreach(var result in results)Console.WriteLine("PASS "+result);
    Console.WriteLine("PASS "+results.Count+" milestone groups");
}
catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
