using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JunkToNeon.Contracts;

namespace JunkToNeon.Guidance
{
    public enum OrderGuidanceMode { Choose, Continue, Claim }
    public enum FirstRepairGuidanceMode { AutoAvailable, EnterWorkshopYard, ChapterComplete }

    public static class GuideFlow
    {
        public static FirstRepairGuidanceMode FirstRepairMode(GuideContext context)
        {
            if(context==null)throw new ArgumentNullException(nameof(context));
            return context.ChapterComplete?FirstRepairGuidanceMode.ChapterComplete:
                context.EraReady?FirstRepairGuidanceMode.EnterWorkshopYard:FirstRepairGuidanceMode.AutoAvailable;
        }
        public static OrderGuidanceMode OrderMode(GuideContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!context.OrdersAvailable) throw new InvalidOperationException("Orders are not available.");
            return context.OrderReady ? OrderGuidanceMode.Claim : context.OrderActive ? OrderGuidanceMode.Continue : OrderGuidanceMode.Choose;
        }
        public static GuideProposal Observe(GuideState before, GuideObservation observation)
        {
            if (before == null || observation == null) throw new ArgumentNullException(nameof(before));
            ValidateOrigin(before, observation.BeforeContext); ValidateOrigin(before, observation.AfterContext);
            ValidateEvent(observation);
            string key = ObservationKey(observation);
            if (observation.Sequence == before.LastAckSequence) throw new InvalidOperationException("Observation reuses an acknowledgement sequence");
            if (observation.Sequence == before.LastObservedSequence)
            {
                if (key != before.LastObservationKey) throw new InvalidOperationException("Conflicting same-sequence guide observation");
                return new GuideProposal(before, before, observation.Sequence);
            }
            // Only the latest event identity is retained. Older valid payloads are
            // ignored without authenticating historical identity or rewinding state.
            if (observation.Sequence < before.LastObservedSequence) return new GuideProposal(before, before, observation.Sequence);
            if (observation.Sequence <= before.HighWater) throw new InvalidOperationException("Stale guide observation");
            int tutorial = before.TutorialBits;
            if (before.Origin == GuideOrigin.Fresh)
            {
                tutorial |= observation.ObservedTutorialBits;
                if (observation.Kind == GuideOperationKind.ProjectSelection) tutorial |= (int)TutorialSteps.Selected;
                if (observation.ManualSpin) tutorial |= (int)TutorialSteps.ManualSpin;
                // A committed purchase is not proof that its presentation ran.
                // Visibility is supplied explicitly after an actual UI observation.
            }
            var completion = observation.CompletedProjectId == null ? before.LastCompletion : new ProjectCompletion(observation.CompletedProjectId, observation.Sequence);
            long acknowledged = observation.CompletedProjectId == null ? before.CompletionAcknowledgedSequence : 0;
            var after = new GuideState(before.Origin, before.AcknowledgementBits, tutorial, observation.Sequence, key, completion,
                before.LastAckSequence, before.LastAckHintId, acknowledged);
            return new GuideProposal(before, after, observation.Sequence);
        }

        public static GuideProposal Acknowledge(GuideState before, GuideContext context, string hintId, long sequence, long expectedCompletionSequence = 0)
        {
            if (before == null || context == null) throw new ArgumentNullException(nameof(before));
            ValidateOrigin(before, context);
            if (!GuideIds.IsKnownAck(hintId) || sequence < 1) throw new ArgumentException("Invalid guide acknowledgement");
            if (hintId == GuideIds.Completion)
            {
                if (before.LastCompletion == null || expectedCompletionSequence != before.LastCompletion.Sequence) throw new InvalidOperationException("Stale completion acknowledgement");
            }
            else if (expectedCompletionSequence != 0) throw new ArgumentException("Unexpected completion acknowledgement identity");
            // Identity checks precede already-acknowledged shortcuts.
            if (sequence == before.LastAckSequence)
            {
                if (hintId != before.LastAckHintId || hintId == GuideIds.Completion && before.CompletionAcknowledgedSequence != expectedCompletionSequence)
                    throw new InvalidOperationException("Conflicting same-sequence acknowledgement");
                return new GuideProposal(before, before, sequence);
            }
            if (sequence <= before.HighWater) throw new InvalidOperationException("Stale guide acknowledgement");
            if (hintId != GuideIds.Completion && !before.IsAcknowledged(hintId))
            {
                var current = Query(before, context).Suggestion;
                if (current == null || current.HintId != hintId) throw new InvalidOperationException("Guide hint is not currently relevant");
            }
            int bits = before.AcknowledgementBits, tutorial = before.TutorialBits;
            if (hintId != GuideIds.Completion) bits |= GuideIds.Bit(hintId);
            if (hintId == GuideIds.FirstRepair && before.Origin == GuideOrigin.Fresh) tutorial |= (int)TutorialSteps.AutoInformed;
            long completionAck = hintId == GuideIds.Completion ? expectedCompletionSequence : before.CompletionAcknowledgedSequence;
            var after = new GuideState(before.Origin, bits, tutorial, before.LastObservedSequence, before.LastObservationKey, before.LastCompletion, sequence, hintId, completionAck);
            return new GuideProposal(before, after, sequence);
        }

        public static GuideEvaluation Query(GuideState state, GuideContext context)
        {
            if (state == null || context == null) throw new ArgumentNullException(nameof(state));
            ValidateOrigin(state, context);
            GuideSuggestion suggestion = null;
            if (state.Origin == GuideOrigin.LegacyExpanded && !state.IsAcknowledged(GuideIds.ExpandedR7))
                suggestion = New(GuideIds.ExpandedR7, "The chapter has expanded", context.ChapterComplete
                    ? "Your earlier prototype completion and saved progress are preserved. You have now completed the expanded two-era chapter."
                    : "Your earlier prototype completion and saved progress are preserved. The chapter now includes more families and a second era; the remaining projects still need to be built.");
            else if (context.ChapterComplete && !state.IsAcknowledged(GuideIds.ChapterEnd))
                suggestion = New(GuideIds.ChapterEnd, "Chapter complete", !context.OrdersAvailable
                    ? (context.SkillCount == 6 ? "Both eras are complete and all skills are owned. You may finish here; there is no further era." : "Both eras are complete. Remaining skills are optional. You may finish here; there is no further era.")
                    : context.SkillCount == 6
                    ? "Both eras are complete. Optional repeatable orders remain available. You may finish here or keep playing; there is no further era."
                    : "Both eras are complete. Remaining skills and repeatable orders are optional. You may finish here; there is no further era.");
            else if (context.EraReady && !state.IsAcknowledged(GuideIds.EraReady))
                suggestion = New(GuideIds.EraReady, "Workshop Yard is ready", "All Junkyard projects are complete. Enter Workshop Yard explicitly when ready. Your coins, skills and acquired effects are preserved.");
            else if (state.Origin == GuideOrigin.Fresh && context.Stages.AbsoluteLevel(FamilyIds.House) == 0 && context.SelectedProjectId == null && (state.TutorialBits & ((int)TutorialSteps.Previewed | (int)TutorialSteps.Selected)) == 0 && !state.IsAcknowledged(GuideIds.Welcome))
                suggestion = New(GuideIds.Welcome, "Look around", "Your starting coins are already saved. Inspect the house: preview is free and does not select or purchase a project.");
            else if (state.Origin == GuideOrigin.Fresh && context.Stages.AbsoluteLevel(FamilyIds.House) == 0 && !state.IsAcknowledged(GuideIds.ChooseProject))
                suggestion = New(GuideIds.ChooseProject, context.SelectedProjectId == null ? "Choose one project" : "Spin for your project",
                    context.SelectedProjectId == null ? "Use Start Project to select the repair explicitly, then spin manually. Only that selected project can be purchased when affordable." : "Spin manually to gather coins. The selected project is paid once when affordable, and no successor is selected automatically.");
            else if (state.Origin == GuideOrigin.Fresh && context.Stages.AbsoluteLevel(FamilyIds.House) >= 1 && !state.IsAcknowledged(GuideIds.FirstRepair))
                suggestion = FirstRepairSuggestion(context);
            else if (context.OrdersAvailable && !state.IsAcknowledged(GuideIds.Orders))
                suggestion = OrderSuggestion(context);
            else if (context.Stages.AbsoluteLevel(FamilyIds.House) >= 1 && context.SkillCount < 6 && !state.IsAcknowledged(GuideIds.Skills))
                suggestion = New(GuideIds.Skills, "Optional skills", "Inspect the skill tree when useful. Skills are optional purchases, never a requirement for chapter completion, and previews do not spend coins.");
            return new GuideEvaluation(suggestion, state.LastCompletion);
        }

        static GuideSuggestion New(string id, string title, string body) { return new GuideSuggestion(id, title, body); }
        static GuideSuggestion FirstRepairSuggestion(GuideContext context)
        {
            switch(FirstRepairMode(context))
            {
                case FirstRepairGuidanceMode.EnterWorkshopYard:
                    return New(GuideIds.FirstRepair,"The Junkyard is restored","The repaired house and all Junkyard projects are complete. Enter Workshop Yard explicitly for free; your coins, skills and acquired effects are preserved.");
                case FirstRepairGuidanceMode.ChapterComplete:
                    return New(GuideIds.FirstRepair,"Both yards are restored","Both eras are complete. You may finish here or use manual spins; skills and orders are optional, and there is no further era.");
                default:
                    return New(GuideIds.FirstRepair,"Your first improvement","The house has improved. AUTO is available after repair; choose when to use it. Each next project still needs your explicit selection.");
            }
        }
        static GuideSuggestion OrderSuggestion(GuideContext context)
        {
            switch (OrderMode(context))
            {
                case OrderGuidanceMode.Claim:
                    return New(GuideIds.Orders, "An order is ready", "The active order is ready to claim explicitly. Its reward is not automatic. Orders can be repeated as new instances.");
                case OrderGuidanceMode.Continue:
                    return New(GuideIds.Orders, "An order is in progress", "Continue qualifying foreground spins for the active order. Its progress is preserved; another order requires completing or explicitly cancelling this instance. Orders remain optional.");
                default:
                    return New(GuideIds.Orders, "Optional orders", "After the chapter, you can accept repeatable orders optionally. Only future qualifying foreground spins count; each reward needs an explicit claim.");
            }
        }
        static void ValidateOrigin(GuideState state, GuideContext context)
        {
            if ((state.Origin == GuideOrigin.LegacyExpanded) != context.LegacyExpanded) throw new InvalidOperationException("Guide origin contradicts factual migration provenance");
        }
        static void ValidateEvent(GuideObservation o)
        {
            var a = o.BeforeContext; var b = o.AfterContext;
            if (a.LegacyExpanded != b.LegacyExpanded) throw new InvalidOperationException("Observation cannot change migration origin");
            if (((o.ObservedTutorialBits & (int)TutorialSteps.ManualSpin) != 0) && !o.ManualSpin) throw new InvalidOperationException("Manual-spin step lacks manual action");
            if (((o.ObservedTutorialBits & (int)TutorialSteps.AutoInformed) != 0) && b.Stages.AbsoluteLevel(FamilyIds.House) < 1) throw new InvalidOperationException("AUTO information before repair");
            if (((o.ObservedTutorialBits & (int)TutorialSteps.VisibleImprovement) != 0) && !HasImprovement(b.Stages)) throw new InvalidOperationException("Improvement step lacks a public improved stage");
            if (((o.ObservedTutorialBits & (int)TutorialSteps.Selected) != 0) && o.Kind != GuideOperationKind.ProjectSelection && a.SelectedProjectId == null && b.SelectedProjectId == null) throw new InvalidOperationException("Selection step lacks selected project");
            if (o.Kind == GuideOperationKind.EraEntry)
            {
                if (o.ActionId != ChapterIds.UnlockWorkshopYard || o.CompletedProjectId != null || a.Stages.EraId != EraIds.Junkyard || b.Stages.EraId != EraIds.WorkshopYard || !a.EraReady || a.SelectedProjectId != null || b.SelectedProjectId != null || a.SkillCount != b.SkillCount) throw new InvalidOperationException("Invalid era-entry observation");
                foreach (var stage in b.Stages.Entries) if (stage.AbsoluteLevel != 3) throw new InvalidOperationException("Era entry must preserve lifetime2->3 stages");
                return;
            }
            if (a.Stages.EraId != b.Stages.EraId) throw new InvalidOperationException("Unexpected era change");
            if (o.Kind == GuideOperationKind.SkillPurchase)
            {
                if (!KnownSkill(o.ActionId) || o.CompletedProjectId != null || a.SkillCount + 1 != b.SkillCount || a.SelectedProjectId != b.SelectedProjectId || !SameStages(a.Stages, b.Stages)) throw new InvalidOperationException("Invalid skill-purchase observation");
            }
            else
            {
                if (a.SkillCount != b.SkillCount) throw new InvalidOperationException("Unexpected skill change");
                if (o.CompletedProjectId == null)
                {
                    if (!SameStages(a.Stages, b.Stages)) throw new InvalidOperationException("Stage advanced without completed project");
                    if (o.Kind == GuideOperationKind.ProjectSelection ? !ChapterIds.IsProject(o.ActionId, a.Stages.EraId) || b.SelectedProjectId != o.ActionId : a.SelectedProjectId != b.SelectedProjectId) throw new InvalidOperationException("Invalid selected-project observation");
                }
                else
                {
                    string family; int target;
                    Resolve(o.CompletedProjectId, a.Stages.EraId, out family, out target);
                    if (o.Kind == GuideOperationKind.ProjectSelection ? o.ActionId != o.CompletedProjectId : a.SelectedProjectId != o.CompletedProjectId) throw new InvalidOperationException("Completion was not explicitly selected");
                    if (b.SelectedProjectId != null || a.Stages.AbsoluteLevel(family) + 1 != target || b.Stages.AbsoluteLevel(family) != target) throw new InvalidOperationException("Invalid completed stage");
                    foreach (string id in FamilyIds.All) if (id != family && a.Stages.AbsoluteLevel(id) != b.Stages.AbsoluteLevel(id)) throw new InvalidOperationException("Multiple projects completed");
                }
            }
            if (o.Kind != GuideOperationKind.ForegroundSpin)
            {
                if (a.OrderActive != b.OrderActive || a.OrderReady != b.OrderReady) throw new InvalidOperationException("Unrelated active-order facts changed");
                if (a.OrdersAvailable != b.OrdersAvailable && !( !a.ChapterComplete && b.ChapterComplete && !a.OrdersAvailable && b.OrdersAvailable)) throw new InvalidOperationException("Unrelated order availability changed");
            }
        }
        static bool HasImprovement(FamilyStages stages) { foreach (var stage in stages.Entries) if (stage.AbsoluteLevel > 0) return true; return false; }
        static bool SameStages(FamilyStages a, FamilyStages b)
        {
            if (a.EraId != b.EraId) return false;
            foreach (string family in FamilyIds.All) if (a.AbsoluteLevel(family) != b.AbsoluteLevel(family)) return false;
            return true;
        }
        static void Resolve(string projectId, string era, out string family, out int absoluteTarget)
        {
            foreach (string id in FamilyIds.All) for (int level = 1; level <= 2; level++) if (ChapterIds.Project(era, id, level) == projectId) { family = id; absoluteTarget = era == EraIds.Junkyard ? level : level + 3; return; }
            throw new InvalidOperationException("Unknown completed project");
        }
        static bool KnownSkill(string id) { return id == SkillIds.Universal1 || id == SkillIds.Universal2 || id == SkillIds.Recycling1 || id == SkillIds.Recycling2 || id == SkillIds.Delivery1 || id == SkillIds.Delivery2; }
        static string ObservationKey(GuideObservation observation)
        {
            var fields = new[] { observation.Sequence.ToString(CultureInfo.InvariantCulture), ((int)observation.Kind).ToString(CultureInfo.InvariantCulture),
                observation.BeforeContext.Identity(), observation.AfterContext.Identity(), observation.ActionId, observation.CompletedProjectId,
                observation.ManualSpin ? "1" : "0", observation.ObservedTutorialBits.ToString(CultureInfo.InvariantCulture) };
            var text = new StringBuilder();
            foreach (string field in fields) { if (field == null) text.Append("-1:"); else text.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field); }
            using (var sha = SHA256.Create())
            {
                var key = new StringBuilder(64); const string hex = "0123456789abcdef";
                foreach (byte value in sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))) key.Append(hex[value >> 4]).Append(hex[value & 15]);
                return key.ToString();
            }
        }
    }
}
