using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Guidance;

internal static class Program
{
    static int assertions;
    static void Need(bool value, string reason)
    { assertions++; if (!value) throw new InvalidOperationException(reason); }

    static void Bounded(string value, int maximum, string identity)
    {
        Need(!string.IsNullOrWhiteSpace(value) && value.Length <= maximum, "Missing/oversized copy: " + identity);
        Need(!value.Any(char.IsDigit) && !value.Contains('\n') && !value.Contains('\r'), "Numeric tuning or multiline copy: " + identity);
    }

    static void Orders()
    {
        string[] ids = { "order.sort-salvage", "order.neighborhood-supply", "order.gather-potatoes", "order.pack-boxes" };
        var introductions = new HashSet<string>();
        var readyLines = new HashSet<string>();
        var claimLines = new HashSet<string>();
        foreach (string id in ids)
        {
            string introduction = ChapterNarrative.OrderIntroduction(id);
            string ready = ChapterNarrative.OrderReady(id);
            string claimed = ChapterNarrative.OrderClaimed(id);
            Bounded(introduction, 128, id + "/introduction");
            Bounded(ready, 128, id + "/ready");
            Bounded(claimed, 128, id + "/claimed");
            Need(introduction.EndsWith("Заказ необязателен.", StringComparison.Ordinal), "Offer hides optionality: " + id);
            Need(ready.Contains("Заберите награду") && ready.EndsWith("по желанию.", StringComparison.Ordinal), "Ready line skips explicit claim/optionality: " + id);
            Need(claimed.Contains("оплачен") && claimed.EndsWith("Другой заказ — по желанию.", StringComparison.Ordinal), "Claim line lacks distinct payoff/optionality: " + id);
            Need(!introduction.Contains("оплачен") && !ready.Contains("оплачен"), "Unclaimed state announces payment: " + id);
            Need(introductions.Add(introduction) && readyLines.Add(ready) && claimLines.Add(claimed), "Order lines are not distinct: " + id);
        }
        Need(ChapterNarrative.OrderIntroduction(ids[2]).Contains("на барабанах"), "Potato offer implies a resource inventory");
        Need(ChapterNarrative.OrderIntroduction(ids[3]).Contains("считать символы"), "Box offer implies a new packing mechanic");
        Need(ChapterNarrative.OrderReady(ids[2]).Contains("появления картошки") && ChapterNarrative.OrderReady(ids[3]).Contains("появления коробок"), "Candidate target meaning drifted");
        Console.WriteLine("PASS four distinct optional offers, ready/claimed separation and bounded unpriced copy");
    }

    static void FamiliesAndExistingStories()
    {
        // Independent purpose expectations; no price/amount is copied from the catalog.
        string[] families = { "house", "workshop", "recycler", "pigeon-loft", "cart", "power-source" };
        int[] symbols = { SymbolIds.Potato, SymbolIds.Box, SymbolIds.Trash, SymbolIds.Pigeon, SymbolIds.Box, SymbolIds.Potato };
        string[] keywords = { "AUTO", "спинах", "мусора", "доставки", "коробок", "картошки" };
        var catalog = ChapterData.TwoEras();
        Need(catalog.Eras.Count == 2, "Overview assumes exactly the current two eras");
        var overviews = new HashSet<string>();
        for (int i = 0; i < families.Length; i++)
        {
            string family = families[i];
            string text = ChapterNarrative.FamilyOverview(family);
            Bounded(text, 160, family);
            Need(overviews.Add(text) && text.Contains(keywords[i]), "Missing distinct family purpose: " + family);
            Need(!text.Contains("уже постро") && !text.Contains("куплен") && !text.Contains("получен"), "Overview claims ownership: " + family);
            var effects = catalog.Eras.SelectMany(e => e.Catalog.Upgrades)
                .Where(u => u.Id.StartsWith(family + ".", StringComparison.Ordinal)).SelectMany(u => u.Effects).ToArray();
            Need(effects.Any(e => e.Kind == ContentEffectKind.SymbolCoins && e.SymbolId == symbols[i]), "Family symbol expectation no longer matches actual catalog: " + family);
            if (family == "house") Need(effects.Any(e => e.Kind == ContentEffectKind.UnlockAuto), "House AUTO promise has no catalog support");
            if (family == "workshop") Need(effects.Any(e => e.Kind == ContentEffectKind.SpinCoins), "Workshop spin promise has no catalog support");
            if (family == "recycler") Need(effects.Any(e => e.Kind == ContentEffectKind.TrashSymbolCoins), "Recycler promise has no catalog support");
            if (family == "pigeon-loft") Need(effects.Any(e => e.Kind == ContentEffectKind.PigeonDeliveryCoins), "Loft delivery promise has no catalog support");
        }
        var projects = catalog.Eras.SelectMany(e => e.Catalog.Upgrades).ToArray();
        Need(projects.Length == 24, "Existing completion count changed");
        foreach (var project in projects)
        {
            Need(ChapterNarrative.TryCompletion(project.Id, out var beat) && beat.ProjectId == project.Id, "Existing completion missing: " + project.Id);
            Need(beat.Title.Length > 0 && beat.Title.Length <= 48 && beat.Body.Length > 0 && beat.Body.Length <= 150, "Existing completion bound changed: " + project.Id);
        }
        Console.WriteLine("PASS six future-purpose overviews grounded in actual catalog; existing24 story lookups");
    }

    static void LookupContracts()
    {
        foreach (string id in new[] { null, "", " ", "ORDER.SORT-SALVAGE", "House", "order.pack-boxes.extra", "house.repair", "skill.delivery.1", new string('x', 4096) })
        {
            Need(ChapterNarrative.OrderIntroduction(id) == null && ChapterNarrative.OrderReady(id) == null && ChapterNarrative.OrderClaimed(id) == null, "Unknown order invents copy");
            Need(ChapterNarrative.FamilyOverview(id) == null, "Unknown family invents copy");
        }
        Need(ChapterNarrative.FamilyOverview("order.pack-boxes") == null && ChapterNarrative.OrderClaimed("house") == null, "Identity domains alias");
        Need(typeof(NarrativeBeat).GetProperties().All(p => p.SetMethod == null), "Story exposes setters");
        Need(typeof(ChapterNarrative).GetFields(BindingFlags.Public | BindingFlags.Static).Length == 0, "Catalog exposes mutable collection");
        foreach (string name in new[] { "OrderIntroduction", "OrderReady", "OrderClaimed", "FamilyOverview" })
        {
            var method = typeof(ChapterNarrative).GetMethod(name);
            Need(method != null && method.ReturnType == typeof(string) && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string), "Lookup is not a pure string interface: " + name);
        }
        Console.WriteLine("PASS null/unknown/case-sensitive fallbacks and immutable string lookup surface");
    }

    static int Main()
    {
        try
        {
            Orders(); FamiliesAndExistingStories(); LookupContracts();
            Console.WriteLine("PASS expansion content: 3 groups, " + assertions + " assertions; no Core/session, IO, Unity or native execution");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL expansion content: " + error); return 1; }
    }
}
