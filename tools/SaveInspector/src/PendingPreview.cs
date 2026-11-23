using System;
using System.Collections.Generic;
using JunkToNeon.Core;

namespace JunkToNeon.SaveInspector
{
    // Replays the frozen pending operation with production GameLogic.ApplyPending on the decoded
    // in-memory state. No storage, backup or clock is involved; only hashes of the result leave.
    public static class PendingPreview
    {
        static readonly JsonStateCodec Codec = new JsonStateCodec();

        public static int Describe(ModelState state, out Obj report)
        {
            if (state.pending == null)
            {
                report = Cli.Envelope("preview-pending", "not-applicable")
                    .Add("reason", "No pending operation. The snapshot is committed through sequence " + state.sequence + ".")
                    .Add("lastSpinReceipt", state.lastSpin == null ? "none" : "sequence " + state.lastSpin.sequence + " is a retained historical receipt, not a payable grant")
                    .Add("preview", "nothing replayed; a committed operation is never previewed as another grant");
                return ExitCodes.PreviewNotApplicable;
            }
            string beforeCanonical = Codec.Encode(state);
            var pending = state.pending;
            ModelState next;
            try { next = GameLogic.ApplyPending(state); }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is FormatException || error is OverflowException)
            {
                report = Cli.Envelope("preview-pending", "replay-refused").Add("error", Cli.Rejection(error))
                    .Add("preview", "refused: no repaired or fabricated state is produced");
                return ExitCodes.PreviewFailed;
            }
            // Mirror GameStore.Write's persistability gate in memory: validate, encode, decode, validate.
            bool persistable;
            string afterCanonical = null;
            try
            {
                GameLogic.Validate(next);
                afterCanonical = Codec.Encode(next);
                GameLogic.Validate(Codec.Decode(afterCanonical));
                persistable = true;
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is FormatException || error is OverflowException)
            { persistable = false; }
            bool beforeUntouched = Codec.Encode(state) == beforeCanonical;
            bool committed = next.pending == null && next.sequence == pending.sequence;

            var beforeFacts = Facts.Extract(state); var afterFacts = Facts.Extract(next);
            var changes = SemanticDiff.Changes(beforeFacts, afterFacts, out var groups);
            var summary = new Obj().Add("kind", pending.kind).Add("sequence", pending.sequence)
                .Add("coinsBefore", state.coins).Add("coinsAfter", next.coins).Add("walletDelta", next.coins - state.coins)
                .Add("seedBefore", state.seed).Add("seedAfter", next.seed)
                .Add("deliveryCarryBefore", WaveProgress.Of(state).Delivery.PigeonProgress).Add("deliveryCarryAfter", WaveProgress.Of(next).Delivery.PigeonProgress);
            if (pending.chapter != null)
                summary.Add("frozenChapterCoinsAfter", pending.chapter.CoinsAfter).Add("frozenChapterMatches", pending.chapter.CoinsAfter == next.coins);
            if (pending.journey != null)
                summary.Add("frozenJourneyCoinsAfter", pending.journey.CoinsAfter).Add("frozenJourneyMatches", pending.journey.CoinsAfter == next.coins);

            bool expansionMatches=true;
            if(pending.expansion!=null)
            {
                var expected=pending.expansion;
                var beforeExpansion=ExpansionEconomy.Project(state);
                var actual=ExpansionEconomy.Project(next);
                // Composed resource descriptors deliberately carry zero wallet placeholders;
                // their fully validated lower descriptor owns the wallet.
                bool dedicated=expected.Kind=="expansion"||expected.Kind=="arcade";
                expansionMatches=(!dedicated||expected.CoinsAfter==next.coins)&&expected.SeedAfter==next.seed&&
                    ExpansionCodec.EncodeState(expected.After)==ExpansionCodec.EncodeState(actual);
                summary.Add("expansionWalletAuthority",Facts.ExpansionWalletAuthority(expected.Kind))
                    .Add("expansionRuleRevision",expected.RuleRevision)
                    .Add("expansionFormatBefore",beforeExpansion.FormatVersion).Add("expansionFormatAfter",actual.FormatVersion)
                    .Add("bonusWorkbenchBefore",beforeExpansion.BonusWorkbenchLevel).Add("bonusWorkbenchAfter",actual.BonusWorkbenchLevel)
                    .Add("bonusFocusBefore",beforeExpansion.BonusFocusId).Add("bonusFocusAfter",actual.BonusFocusId)
                    .Add("frozenExpansionCoinsAfter",dedicated?(object)expected.CoinsAfter:null)
                    .Add("partsBefore",beforeExpansion.Parts).Add("partsAfter",actual.Parts)
                    .Add("potatoesBefore",beforeExpansion.Potatoes).Add("potatoesAfter",actual.Potatoes)
                    .Add("energyBefore",beforeExpansion.Energy).Add("energyAfter",actual.Energy)
                    .Add("frozenExpansionMatches",expansionMatches);
                if(expected.ArcadeQuote!=null)summary.Add("arcadeAction",expected.ArcadeQuote.Action).Add("arcadeId",expected.ArcadeQuote.Id)
                    .Add("arcadeInstanceSequence",expected.ArcadeQuote.ExpectedInstance);
            }

            bool ok = persistable && beforeUntouched && committed && expansionMatches;
            report = Cli.Envelope("preview-pending", ok ? "replayed-in-memory" : "replay-inconsistent")
                .Add("operation", summary)
                .Add("checks", new Obj().Add("resultPassesProductionPersistValidation", persistable).Add("decodedBeforeStateUnchanged", beforeUntouched)
                    .Add("resultHasNoPendingAndCommitsFrozenSequence", committed).Add("resultMatchesFrozenExpansion",expansionMatches)
                    .Add("resultMatchesFrozenArcade",pending.expansion?.RuleRevision==ExpansionCatalog.ArcadeRuleRevision?(object)expansionMatches:null))
                .Add("milestones", new Obj().Add("before", beforeFacts.Get("milestones")).Add("after", afterFacts.Get("milestones")))
                .Add("optionalAchievements", new Obj().Add("before", beforeFacts.Get("optionalAchievements"))
                    .Add("computedAfter", afterFacts.Get("optionalAchievements")))
                .Add("arcade",new Obj().Add("before",beforeFacts.Get("arcade")).Add("computedAfter",afterFacts.Get("arcade")))
                .Add("afterCanonicalSha256", afterCanonical == null ? null : InputGuard.HashText(afterCanonical))
                .Add("groupsChanged", groups).Add("changes", changes)
                .Add("notes", new List<object> {
                    "In-memory replay of production GameLogic.ApplyPending on a private decoded clone; no storage call or clock access.",
                    "No replacement save is produced; only the canonical hash of the replayed state is reported.",
                    "GameStore.Open's backup validation, interrupted-write quarantine and recovery-evidence blocking were not modelled.",
                    "This does not establish Android filesystem durability or real recovery success." });
            return ok ? ExitCodes.Ok : ExitCodes.PreviewFailed;
        }
    }
}
