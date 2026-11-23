using System;
using System.IO;
using JunkToNeon.Tests;

var directory = Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "core-files-" + Guid.NewGuid().ToString("N"));
try
{
    var results = CoreRegression.Run(directory);
    foreach (var result in results) Console.WriteLine("PASS " + result);
    Console.WriteLine("PASS " + results.Count + " regression groups");
}
finally
{
    foreach (var name in new[] { "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json" })
    { var path = Path.Combine(directory, name); if (File.Exists(path)) File.Delete(path); }
    if (Directory.Exists(directory)) Directory.Delete(directory, false);
}
