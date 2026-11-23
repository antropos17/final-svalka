using System;
using System.IO;
using JunkToNeon.Tests;

#if R12_BOUNDARY
if(args.Length==4&&args[0]=="--selected-r12")
{SelectedRevisionChecks.Verify(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),Path.GetFullPath(args[3]));return;}
#endif
if(args.Length!=2)throw new ArgumentException("Exact private output and repository roots required");
string output=Path.GetFullPath(args[0]),repo=Path.GetFullPath(args[1]);
if(Directory.Exists(output))throw new IOException("Preserve receipts: use a new private test directory");
Directory.CreateDirectory(output);
foreach(string result in ConstructionPricingRegression.Run(output,repo))Console.WriteLine("PASS "+result);
Console.WriteLine("PASS bounded construction pricing regression");
