using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JunkToNeon.Core;

internal static class Program
{
    static int assertions, largestLabel, largestDescription;
    static void Need(bool value, string reason)
    { assertions++; if (!value) throw new InvalidOperationException(reason); }

    static void Copy(string id)
    {
        string label = ExpansionCopy.Label(id), description = ExpansionCopy.Description(id);
        Need(!string.IsNullOrWhiteSpace(label) && label.Length <= 32, "Missing/long label: " + id);
        Need(!string.IsNullOrWhiteSpace(description) && description.Length <= 160, "Missing/long purpose: " + id);
        Need(!label.Any(char.IsDigit) && !description.Any(char.IsDigit), "Numerical balance/level duplicated in copy: " + id);
        Need(!label.Contains('\n') && !description.Contains('\n'), "Unexpected multiline card copy: " + id);
        largestLabel = Math.Max(largestLabel, label.Length);
        largestDescription = Math.Max(largestDescription, description.Length);
    }

    static void ActualCatalog()
    {
        // Exact input catalog coverage, plus independent allowed identities. No test
        // invents a price/reward or treats a string as ownership/commit evidence.
        var expected = new HashSet<string>(new[] {
            "module.salvage", "module.packing", "module.harvest", "module.combo", "module.dynamo", "socket.2", "socket.3",
            "theme.amber", "theme.mint", "theme.violet", "theme.frost", "theme.rose", "bonus.unlock", "workbench.bonus.1", "workbench.bonus.2", "potato.yield.1", "potato.yield.2",
            "trade.parts", "trade.potatoes" }, StringComparer.Ordinal);
        string[] families = { "house", "workshop", "recycler", "pigeon-loft", "cart", "power-source" };
        foreach (string family in families)
            for (int level = 1; level <= 2; level++) expected.Add("equipment." + family + "." + level);
        Need(expected.SetEquals(ExpansionCatalog.Offers.Select(o => o.Id)) && ExpansionCatalog.Offers.Count == 31, "Catalog IDs changed; reconcile copy against actual rules");
        Need(ExpansionCatalog.Offers.Count(o => o.Kind == "module") == 5, "The loadout choice is not the actual five-module catalog");
        foreach (var offer in ExpansionCatalog.Offers)
        {
            Copy(offer.Id);
            Copy("category." + offer.Kind);
            if (offer.Kind == "equipment")
            {
                Need(families.Contains(offer.FamilyId) && offer.Level >= 1 && offer.Level <= 2, "Equipment identity is outside the existing six families/two separate levels");
                Need(ExpansionCopy.Description(offer.Id) == ExpansionCopy.Description(offer.FamilyId), "Equipment/family purpose mismatch");
            }
        }
        Copy(ExpansionCatalog.DefaultTheme); Copy(ExpansionCatalog.NextBonusBoost);
        Console.WriteLine("PASS actual31-offer coverage, five modules, default theme/booster and separate equipment families");
    }

    static void FactualVocabulary()
    {
        foreach (string key in new[] { "resource.coins", "resource.parts", "resource.potatoes", "resource.energy",
            "section.shop", "section.loadout", "section.resources", "section.equipment", "category.boost", "category.speed",
            "action.buy", "action.equip", "action.theme", "action.boost", "action.speed", "speed.normal", "speed.fast", "module.none" }) Copy(key);
        string[] families = { "house", "workshop", "recycler", "pigeon-loft", "cart", "power-source" };
        string[] purpose = { "Parts and Potato storage capacity", "Parts on future spins", "Trash symbols", "Energy storage capacity", "Box symbols", "Energy on future spins" };
        for (int i = 0; i < families.Length; i++)
        {
            Copy(families[i]);
            Need(ExpansionCopy.Description(families[i]).Contains(purpose[i]), "Family utility has drifted: " + families[i]);
            Need(ExpansionCopy.Description(families[i]).Contains("counted separately"), "Family equipment masquerades as main construction progress");
        }
        Need(ExpansionCopy.Description("category.equipment").Contains("do not count toward main chapter progress"), "Equipment completion is implied to be mandatory");
        Need(ExpansionCopy.Description("module.salvage").Contains("Trash symbol") && ExpansionCopy.Description("module.packing").Contains("Box symbol") && ExpansionCopy.Description("module.harvest").Contains("Potatoes"), "Module resource specialization is ambiguous");
        Need(ExpansionCopy.Description("category.module").Contains("support slot"), "Module purpose omits equipped condition");
        Need(ExpansionCopy.Description("module.combo").Contains("Parts and Potatoes") && ExpansionCopy.Description("module.combo").Contains("pair or triple") && ExpansionCopy.Description("module.combo").Contains("per spin"), "Combo copy implies a per-symbol or different trigger");
        Need(ExpansionCopy.Description("module.dynamo").Contains("Pigeon appears") && ExpansionCopy.Description("module.dynamo").Contains("per spin"), "Dynamo copy implies a per-pigeon or delivery trigger");
        Need(ExpansionCopy.Description("module.none").Contains("module remains purchased"), "Module removal implies loss/refund of ownership");
        Need(ExpansionCopy.Description("speed.fast").Contains("resources per spin stay the same"), "Presentation speed implies unchanged resources per minute");
        Need(!ExpansionCopy.Description("theme.neon").Contains("purchased") && !ExpansionCopy.Description("action.theme").Contains("purchased"), "Free default theme described as a purchase");
        Need(ExpansionCopy.Description("boost.next-bonus").Contains("Energy is spent on activation") && ExpansionCopy.Description("boost.next-bonus").Contains("storage caps"), "Booster timing/cap meaning omitted");
        foreach (string id in new[] { "trade.parts", "trade.potatoes" })
            Need(ExpansionCopy.Description(id).Contains("does not buy the selected construction") && ExpansionCopy.Description(id).Contains("saved price"), "Exchange implies a construction purchase/reprice");
        string first = ExpansionCopy.FirstUse;
        Need(first.Length <= 240 && !first.Any(char.IsDigit), "First use repeats numerical tuning or is oversized");
        foreach (string term in new[] { "Main spins are free", "Parts", "Potatoes", "Energy", "new spins", "never retroactively", "on a timer" })
            Need(first.Contains(term), "First-use purpose/free-play/history boundary absent: " + term);
        Need(first.Contains("explicit Diamond purchases of Parts or Potatoes") && first.Contains("never retroactively or on a timer"), "First use omits the explicit pack source or changes the history/timer boundary");
        Console.WriteLine("PASS resource utility, equipped modules, optional equipment, free spins and future-only first use");
    }

    static void WorkbenchFocusAndPresets()
    {
        foreach (string key in new[] { "focus.balanced", "focus.parts", "focus.potatoes", "preset.parts", "preset.harvest", "preset.energy",
            "category.focus", "category.preset", "action.focus", "action.preset", "guidance.loadout", "guidance.resources", "guidance.bonus" }) Copy(key);
        Need(ExpansionCatalog.Get("workbench.bonus.1").Kind == "workbench" && ExpansionCatalog.Get("workbench.bonus.1").Level == 1 &&
            ExpansionCatalog.Get("workbench.bonus.2").Kind == "workbench" && ExpansionCatalog.Get("workbench.bonus.2").Level == 2, "Workbench descriptions bound to the wrong actual offers");
        Need(ExpansionCopy.Description("workbench.bonus.1").Contains("existing total") && ExpansionCopy.Description("workbench.bonus.1").Contains("before the booster"), "WorkbenchI falsely adds a resource yield");
        Need(ExpansionCopy.Description("workbench.bonus.2").Contains("raw bonus reward") && ExpansionCopy.Description("workbench.bonus.2").Contains("Focus, the booster and storage caps apply afterward"), "WorkbenchII raw bonus order is ambiguous");
        Need(ExpansionCopy.Description("focus.balanced").Contains("Keeps the resource bonus distribution"), "Balanced focus promises equal shares");
        foreach (string focus in new[] { "focus.parts", "focus.potatoes" })
            Need(ExpansionCopy.Description(focus).Contains("before the booster") && ExpansionCopy.Description(focus).Contains("Excess is not shifted"), "Focused bonus implies redirected capped overflow");
        Need(ExpansionCopy.Description("category.focus").Contains("free after buying the workbench") && ExpansionCopy.Description("category.focus").Contains("not Coins or symbol odds"), "Focus omits its gate or implies main-reel odds/payout changes");
        var labels = new HashSet<string>();
        foreach (string preset in new[] { "preset.parts", "preset.harvest", "preset.energy" })
        {
            Need(labels.Add(ExpansionCopy.Label(preset)), "Preset priorities have indistinguishable names");
            string description = ExpansionCopy.Description(preset);
            Need(description.Contains("only after confirmation") && description.Contains("purchased modules and open slots"), "Preset implies an automatic purchase/equip or use of locked sockets");
        }
        foreach (string theme in new[] { "theme.frost", "theme.rose" })
            Need(ExpansionCopy.Description(theme) == ExpansionCopy.Description("theme.neon"), "New palette promises an economic effect");
        Need(ExpansionCopy.Description("guidance.loadout").Contains("Current loadout: equipped modules"), "Guidance invents a selected-preset history");
        Need(ExpansionCopy.Description("guidance.resources").Contains("reduce the reward") && ExpansionCopy.Description("guidance.resources").Contains("an exchange"), "Resource guidance omits clipping/useful next action");
        Need(ExpansionCopy.Description("guidance.bonus").Contains("before focus") && ExpansionCopy.Description("guidance.bonus").Contains("only once"), "Guidance implies a double booster");
        Console.WriteLine("PASS workbench/focus order, explicit owned-only presets, visual palettes and current-loadout guidance");
    }

    static void UnknownAndStateless()
    {
        foreach (string id in new[] { null, "", " ", "MODULE.SALVAGE", "module.missing", "equipment.workshop.0", "equipment.house.3",
            "equipment.dock.1", "socket.4", "resource.gold", "house.repair", "buy", "category.unknown", "focus.Parts", "focus.coins",
            "workbench.bonus.0", "workbench.bonus.3", "preset.free-modules", "theme.frost.extra", new string('x', 4096) })
            Need(ExpansionCopy.Label(id) == null && ExpansionCopy.Description(id) == null, "Unknown key fabricated copy");
        Need(typeof(ExpansionCopy).GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0, "Copy helper retains mutable state");
        Need(typeof(ExpansionCopy).GetProperties().All(p => p.PropertyType == typeof(string) && p.SetMethod == null), "Mutable/non-string copy property");
        foreach (string name in new[] { "Label", "Description" })
        {
            var method = typeof(ExpansionCopy).GetMethod(name);
            Need(method != null && method.ReturnType == typeof(string) && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string), "Lookup depends on economic state or loses its fallback contract");
        }
        Console.WriteLine("PASS null/unknown/case-sensitive fallbacks and stateless immutable string API");
    }

    static int Main()
    {
        try
        {
            ActualCatalog(); FactualVocabulary(); WorkbenchFocusAndPresets(); UnknownAndStateless();
            Console.WriteLine("PASS ExpansionCopy: 4 groups / " + assertions + " assertions; max label=" + largestLabel + ", description=" + largestDescription + ", firstUse=" + ExpansionCopy.FirstUse.Length);
            Console.WriteLine("Scope: pure catalog/copy only; no GameSession, persistence, Runtime, Unity or native execution.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL ExpansionCopy: " + error); return 1; }
    }
}
