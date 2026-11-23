using System;
if (args.Length != 2 || args[0] != "--workspace") throw new ArgumentException("Explicit isolated X: workspace required");
NightLegacyPricing.CheckDeadline();
NightLegacyPricing.Run(args[1]);
