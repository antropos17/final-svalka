using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JunkToNeon.Content
{
    public sealed class ValidatedCatalog
    {
        public CatalogDefinition Definition { get; }
        readonly Dictionary<string, UpgradeDefinition> upgrades;
        public ValidatedCatalog(CatalogDefinition definition, IEnumerable<string> availableAssetKeys)
        {
            var issues = CatalogValidator.Validate(definition, availableAssetKeys);
            if (issues.Count != 0) throw new ArgumentException(string.Join("\n", issues.Select(x => x.ToString())), nameof(definition));
            Definition = definition;
            upgrades = definition.Upgrades.ToDictionary(u => u.Id, StringComparer.Ordinal);
        }
        public bool TryGetUpgrade(string id, out UpgradeDefinition upgrade)
        { upgrade = null; return id != null && upgrades.TryGetValue(id, out upgrade); }

        // This accepts a caller-owned snapshot, never a live mutable save or wallet.
        public ReadOnlyCollection<string> AvailableUpgrades(IEnumerable<string> completedUpgradeIds)
        {
            var completed = Progress(completedUpgradeIds);
            return Definition.Upgrades.Where(u => u.Enabled && !completed.Contains(u.Id) && u.Prerequisites.All(completed.Contains))
                .Select(u => u.Id).ToList().AsReadOnly();
        }
        public ReadOnlyCollection<string> RemainingCompletionUpgrades(IEnumerable<string> completedUpgradeIds)
        {
            var completed = Progress(completedUpgradeIds);
            return Definition.CompletionUpgradeIds.Where(id => !completed.Contains(id)).ToList().AsReadOnly();
        }
        public bool IsChapterComplete(IEnumerable<string> completedUpgradeIds)
        {
            var completed = Progress(completedUpgradeIds);
            return !Definition.Proposed && Definition.CompletionUpgradeIds.All(completed.Contains);
        }
        HashSet<string> Progress(IEnumerable<string> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var completed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in values)
            {
                if (completed.Count >= CatalogValidator.MaximumEntries || id == null || !upgrades.ContainsKey(id) || !completed.Add(id))
                    throw new ArgumentException("Unknown, duplicate or oversized completed-upgrade snapshot.", nameof(values));
                if (!upgrades[id].Enabled) throw new ArgumentException("Disabled upgrade cannot appear in live progress.", nameof(values));
            }
            foreach (var id in completed)
                if (!upgrades[id].Prerequisites.All(completed.Contains)) throw new ArgumentException("Completed upgrade lacks its prerequisite.", nameof(values));
            return completed;
        }
    }
}
