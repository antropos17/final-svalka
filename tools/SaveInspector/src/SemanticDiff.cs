using System;
using System.Collections.Generic;
using System.Text.Json;
using JunkToNeon.Core;

namespace JunkToNeon.SaveInspector
{
    // Compares two independently validated snapshots through the same facts extractor.
    // Observed deltas, inferred explanations and computed checks are kept separate.
    public static class SemanticDiff
    {
        public static List<object> Changes(Obj beforeFacts, Obj afterFacts, out List<object> groups)
        {
            var a = Report.Flatten(beforeFacts); var b = Report.Flatten(afterFacts);
            var beforeMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in a) beforeMap[pair.Key] = pair.Value;
            var afterKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in b) afterKeys.Add(pair.Key);
            var changes = new List<object>();
            var changedGroups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in b)
            {
                beforeMap.TryGetValue(pair.Key, out string old);
                if (old == null && pair.Value == "null" && HasDescendant(beforeMap.Keys, pair.Key)) old = "{object}";
                if (old == pair.Value) continue;
                changes.Add(new Obj().Add("group", Group(pair.Key)).Add("path", pair.Key).Add("before", old).Add("after", pair.Value));
                changedGroups.Add(Group(pair.Key));
            }
            foreach (var pair in a)
            {
                if (afterKeys.Contains(pair.Key)) continue;
                // A null that became an object is already described by the object's new fields.
                if (pair.Value == "null" && HasDescendant(afterKeys, pair.Key)) continue;
                changes.Add(new Obj().Add("group", Group(pair.Key)).Add("path", pair.Key).Add("before", pair.Value).Add("after", null));
                changedGroups.Add(Group(pair.Key));
            }
            groups = new List<object>();
            foreach (string g in Facts.Groups) if (changedGroups.Contains(g)) groups.Add(g);
            return changes;
        }

        static bool HasDescendant(IEnumerable<string> keys, string path)
        {
            foreach (string key in keys)
                if (key.Length > path.Length && key.StartsWith(path, StringComparison.Ordinal) && (key[path.Length] == '.' || key[path.Length] == '[')) return true;
            return false;
        }

        static string Group(string path)
        {
            int dot = path.IndexOf('.');
            return dot < 0 ? path : path.Substring(0, dot);
        }

        static object Path(Obj root, string path)
        {
            object cursor = root;
            foreach (string part in path.Split('.'))
            {
                if (!(cursor is Obj o)) return null;
                cursor = o.Get(part);
            }
            return cursor;
        }

        static long Long(Obj root, string path) { var v = Path(root, path); return v is long l ? l : v is int i ? i : 0; }

        // Facts intentionally summarize some validated receipt payloads. Never let that
        // projection imply equality or clock-only coverage of a different canonical state.
        internal static List<object> ChangesWithCoverage(Obj beforeFacts, Obj afterFacts, string canonicalBefore, string canonicalAfter,
            out List<object> groups, out bool unexplained)
        {
            var changes = Changes(beforeFacts, afterFacts, out groups);
            bool clockGroupsOnly = groups.Count > 0;
            foreach (string group in groups) if (group != "clockSequence") clockGroupsOnly = false;
            unexplained = canonicalBefore != canonicalAfter && (changes.Count == 0 || clockGroupsOnly && !CanonicalClockOnly(canonicalBefore, canonicalAfter));
            if (unexplained)
            {
                changes.Add(new Obj().Add("group", "format").Add("path", "format.unexplainedCanonicalDifference")
                    .Add("before", InputGuard.HashText(canonicalBefore)).Add("after", InputGuard.HashText(canonicalAfter)));
                if (!groups.Contains("format")) groups.Insert(0, "format");
            }
            return changes;
        }

        static bool CanonicalClockOnly(string before, string after)
        {
            var clock = new HashSet<string>(StringComparer.Ordinal) { "sequence", "revision", "lastSettledAt", "lastSettlementSeen", "lastSettlement" };
            using (var a = JsonDocument.Parse(before)) using (var b = JsonDocument.Parse(after))
            {
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in a.RootElement.EnumerateObject()) if (!clock.Contains(property.Name)) fields.Add(property.Name, property.Value.GetRawText());
                foreach (var property in b.RootElement.EnumerateObject())
                {
                    if (clock.Contains(property.Name)) continue;
                    if (!fields.TryGetValue(property.Name, out string value) || value != property.Value.GetRawText()) return false;
                    fields.Remove(property.Name);
                }
                return fields.Count == 0;
            }
        }

        public static void Describe(ModelState before, ModelState after, Obj report)
        {
            var fa = Facts.Extract(before); var fb = Facts.Extract(after);
            string canonicalBefore = Cli.Canonical(before), canonicalAfter = Cli.Canonical(after);
            bool equal = canonicalBefore == canonicalAfter;
            var changes = ChangesWithCoverage(fa, fb, canonicalBefore, canonicalAfter, out var groups, out bool unexplained);
            var observed = new List<object>(); var inferred = new List<object>(); var computed = new List<object>();

            long delta = after.coins - before.coins;
            observed.Add("sequence " + before.sequence + " -> " + after.sequence);
            if (delta != 0) observed.Add("wallet delta " + (delta > 0 ? "+" : "") + delta + " (" + before.coins + " -> " + after.coins + ")");
            else observed.Add("wallet unchanged at " + after.coins);

            bool onlyClock = groups.Count > 0;
            foreach (var g in groups) if ((string)g != "clockSequence") onlyClock = false;
            if (onlyClock && !unexplained)
                inferred.Add("Only clock/sequence metadata changed; no income grant, reel draw, purchase or order progress is observed.");
            if (unexplained)
                inferred.Add("Canonical state differs beyond the displayed facts; the unexplained canonical difference is marked by hashes, not interpreted as a clock-only change.");

            // Order claim and new instance.
            var claimA = Path(fa, "orders.latestClaim") as Obj; var claimB = Path(fb, "orders.latestClaim") as Obj;
            if (claimB != null && (claimA == null || Long(claimA, "claimSequence") != Long(claimB, "claimSequence")))
            {
                long reward = Long(claimB, "rewardCoins");
                inferred.Add("Consistent with an explicit order claim of " + (string)claimB.Get("orderId") + " instance " + Long(claimB, "instanceSequence") +
                    " at sequence " + Long(claimB, "claimSequence") + " crediting " + reward + ".");
                computed.Add(new Obj().Add("check", "walletDeltaEqualsNewClaimReward").Add("result", delta == reward));
            }
            var activeA = Path(fa, "orders.active") as Obj; var activeB = Path(fb, "orders.active") as Obj;
            if (activeB != null && (activeA == null || Long(activeA, "instanceSequence") != Long(activeB, "instanceSequence")))
                inferred.Add("Consistent with acceptance of a new order instance " + Long(activeB, "instanceSequence") + " (" + (string)activeB.Get("orderId") +
                    ") starting at progress " + Long(activeB, "progress") + "; any older claim stays a retained receipt.");
            bool sameClaim = claimA == null ? claimB == null : claimB != null && Long(claimA, "claimSequence") == Long(claimB, "claimSequence");
            if (activeA != null && activeB == null && sameClaim)
                inferred.Add("Active order instance " + Long(activeA, "instanceSequence") + " ended without a new claim (consistent with cancellation).");

            // Pending operation becoming committed.
            if (before.pending != null && after.pending == null)
            {
                if (after.sequence == before.pending.sequence)
                    inferred.Add("Consistent with commit of the frozen pending " + before.pending.kind + " operation at sequence " + before.pending.sequence + ".");
                bool replay = false;
                try { replay = Cli.Canonical(GameLogic.ApplyPending(before)) == Cli.Canonical(after); }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is FormatException || error is OverflowException) { }
                computed.Add(new Obj().Add("check", "afterEqualsInMemoryReplayOfBeforePending").Add("result", replay));
            }
            if (before.pending == null && after.pending != null)
                inferred.Add("A new operation is reserved but not committed in the after snapshot.");

            // Projected modern metadata becoming persisted.
            foreach (var name in new[] { "wave", "chapter", "journey" })
            {
                bool wasStored = (bool)Path(fa, "format." + name + "Stored"); bool isStored = (bool)Path(fb, "format." + name + "Stored");
                if (!wasStored && isStored) inferred.Add("The " + name + " aggregate is now stored; before it was absent and only projected in memory.");
                if (wasStored && !isStored) inferred.Add("The " + name + " aggregate is absent after being stored (a downgrade/rollback shape, not produced by normal commands).");
            }
            if (after.sequence < before.sequence) inferred.Add("Sequence moved backwards: consistent with a backup rollback or swapped inputs.");

            report.Add("equal", equal);
            report.Add("changeCount", changes.Count);
            report.Add("groupsChanged", groups);
            report.Add("observed", observed);
            report.Add("inferred", inferred);
            report.Add("computed", computed);
            report.Add("changes", changes);
            report.Add("caveats", new List<object> {
                "Explanations are inferences from two snapshots; they do not prove which UI action occurred.",
                "Two valid snapshots cannot prove that the history between them was not modified.",
                "No merge, repair or rollback is performed." });
        }
    }
}
