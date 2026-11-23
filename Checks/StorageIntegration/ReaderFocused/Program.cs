using System;
if (args.Length != 2 || args[0] != "--workspace") throw new ArgumentException("Explicit isolated X: workspace required");
BoundedReaderChecks.Run(args[1]);
