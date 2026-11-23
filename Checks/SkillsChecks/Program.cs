using System;
using JunkToNeon.Tests.Upgrades;
if (Array.IndexOf(args, "--pricing-only") < 0)
{
    foreach (var group in UpgradeRegression.Run()) Console.WriteLine("PASS legacy: " + group);
    var groups = SkillRegression.Run();
    foreach (var group in groups) Console.WriteLine("PASS skills: " + group);
    Console.WriteLine("PASS 8 legacy + " + groups.Count + " skill regression groups");
}
var pricing = PricingRegression.Run();
foreach (var group in pricing) Console.WriteLine("PASS pricing: " + group);
Console.WriteLine("PASS " + pricing.Count + " pricing compatibility groups");
var delivery = DeliveryPriceRegression.Run();
foreach (var group in delivery) Console.WriteLine("PASS delivery-price: " + group);
Console.WriteLine("PASS " + delivery.Count + " delivery retune groups");
