using System;
using JunkToNeon.Tests;
bool afterClearSpin=args.Length==2&&args[1]=="--after-clear-spin";
if(args.Length<1||args.Length>2||(args.Length==2&&!afterClearSpin))throw new ArgumentException("Usage: ClearGoalChecks <repository> [--after-clear-spin]");
foreach(var group in afterClearSpin?ClearGoalRegression.RunAfterClearSpin(args[0]):ClearGoalRegression.Run(args[0]))Console.WriteLine("PASS "+group);
Console.WriteLine(afterClearSpin?"PASS1 focused after-clear-spin group (two rows)":"PASS8 focused goal-clear groups");
