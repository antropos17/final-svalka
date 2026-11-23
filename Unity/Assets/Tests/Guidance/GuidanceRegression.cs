using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Guidance;

namespace JunkToNeon.Tests
{
    public static class GuidanceRegression
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Fails<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new Exception(message);
        }
        static FamilyStages Stages(int house = 0, int others = 0, string era = EraIds.Junkyard)
        {
            var values = new List<FamilyStage>();
            foreach (string family in FamilyIds.All) values.Add(new FamilyStage(family, family == FamilyIds.House ? house : others));
            return new FamilyStages(era, values);
        }
        static GuideContext Context(int house = 0, string selected = null, int skills = 0, bool legacy = false)
        { return new GuideContext(Stages(house), selected, skills, legacy, false, false, false); }
        static GuideState Reload(GuideState state)
        {
            var raw = GuideStateCodec.Encode(state); var decoded = GuideStateCodec.Decode(raw);
            Require(GuideStateCodec.Encode(decoded) == raw, "Guide reload changed bytes/state"); return decoded;
        }
        static GuideObservation Selection(long sequence, GuideContext before, GuideContext after, string project, string completed = null, int steps = 0)
        { return new GuideObservation(sequence, GuideOperationKind.ProjectSelection, before, after, project, completed, false, steps); }
        static void Hint(GuideState state, GuideContext context, string id)
        {
            var result = GuideFlow.Query(state, context);
            Require(result.Suggestion != null && result.Suggestion.HintId == id && !result.Suggestion.BlocksActions, "Unexpected/nonblocking hint: " + id);
        }
        public static IReadOnlyList<string> RunLateFirstRepair()
        {
            var passed=new List<string>();
            foreach(int phase in new[]{0,1,2})
            {
                var context=phase==0?Context(1):phase==1?
                    new GuideContext(Stages(2,2),null,0,false,false,false,false):
                    new GuideContext(Stages(5,5,EraIds.WorkshopYard),null,0,false,true,false,false);
                var state=GuideState.Create(GuideOrigin.Fresh);long next=1;
                if(phase!=0)
                {
                    string higher=phase==1?GuideIds.EraReady:GuideIds.ChapterEnd;
                    Hint(state,context,higher);
                    state=Reload(GuideFlow.Acknowledge(state,context,higher,next++).Apply(state));
                }
                Hint(state,context,GuideIds.FirstRepair);var suggestion=GuideFlow.Query(state,context).Suggestion;
                if(phase==0)Require(suggestion.Body.Contains("AUTO is available"),"Ordinary repaired house lost honest AUTO advice");
                else
                {
                    Require(!suggestion.Title.Contains("AUTO")&&!suggestion.Body.Contains("AUTO is available"),"Overdue FirstRepair falsely promises AUTO at an era/chapter stop");
                    Require(phase==1?suggestion.Body.Contains("Enter Workshop Yard explicitly"):suggestion.Body.Contains("manual")&&suggestion.Body.Contains("optional")&&suggestion.Body.Contains("no further era"),"Overdue repair advice ignores the current boundary");
                }
                string before=GuideStateCodec.Encode(state);var ack=GuideFlow.Acknowledge(state,context,GuideIds.FirstRepair,next);
                var after=Reload(ack.Apply(state));
                Require(after.IsAcknowledged(GuideIds.FirstRepair)&&(after.TutorialBits&(int)TutorialSteps.AutoInformed)!=0,"Existing repair ACK identity/bits changed");
                Require(GuideStateCodec.Encode(state)==before&&!GuideFlow.Acknowledge(after,context,GuideIds.FirstRepair,next).Changed,"Repair inspection/replayed ACK changed twice");
                Require(GuideFlow.Query(after,context).Suggestion?.HintId!=GuideIds.FirstRepair,"Acknowledged repair hint returned");
                passed.Add(phase==0?"ordinary repaired house retains AUTO advice and exact ACK":phase==1?"overdue repair after era-ready ACK directs explicit free entry":"overdue repair after chapter-end ACK preserves optional finite/manual finish");
            }
            return passed.AsReadOnly();
        }
        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            var zero = Context(); var fresh = GuideState.Create(GuideOrigin.Fresh);
            Hint(fresh, zero, GuideIds.Welcome);
            Require(GuideFlow.Query(fresh, zero).Suggestion.Body.Contains("already saved") && GuideFlow.Query(fresh, zero).LastCompletion == null, "Starting grant presented as a new reward");
            foreach (var property in typeof(GuideProposal).GetProperties()) Require(property.Name != "Coins" && property.Name != "Wallet" && property.Name != "Reward", "Guidance exposes economy mutation");
            var welcome = GuideFlow.Acknowledge(fresh, zero, GuideIds.Welcome, 1);
            var state = Reload(welcome.Apply(fresh)); Hint(state, zero, GuideIds.ChooseProject);
            Require(!GuideFlow.Acknowledge(state, zero, GuideIds.Welcome, 1).Changed && state.TutorialBits == 0, "Welcome ACK invented preview or changed twice");
            passed.Add("Explicit fresh origin: starting coins already saved, free-preview welcome, nonblocking/no economy mutation");

            var selected = Context(0, GameIds.HouseRepair);
            var selection = Selection(2, zero, selected, GameIds.HouseRepair, null, (int)TutorialSteps.Previewed);
            state = Reload(GuideFlow.Observe(state, selection).Apply(state));
            Require(state.TutorialBits == 3 && state.LastCompletion == null, "Preview/selection facts lost in one observation");
            Hint(state, selected, GuideIds.ChooseProject);
            state = Reload(GuideFlow.Acknowledge(state, selected, GuideIds.ChooseProject, 3).Apply(state));
            var repaired = Context(1);
            var manual = new GuideObservation(4, GuideOperationKind.ForegroundSpin, selected, repaired, "spin.manual", GameIds.HouseRepair, true);
            state = Reload(GuideFlow.Observe(state, manual).Apply(state));
            Require(state.TutorialBits == 7 && state.LastCompletion.ProjectId == GameIds.HouseRepair && state.LastCompletion.Sequence == 4, "Committed repair claimed visibility before presentation or lost completion");
            Hint(state, repaired, GuideIds.FirstRepair);
            state = Reload(GuideFlow.Acknowledge(state, repaired, GuideIds.FirstRepair, 5).Apply(state));
            Require(state.TutorialBits == 23, "AUTO acknowledgement invented visible presentation or lost information");
            Hint(state, repaired, GuideIds.Skills);
            var visibilityObserved = new GuideObservation(6, GuideOperationKind.ForegroundSpin, repaired, repaired, "spin.auto", null,
                false, (int)TutorialSteps.VisibleImprovement);
            state = Reload(GuideFlow.Observe(state, visibilityObserved).Apply(state));
            Require(state.TutorialBits == 31 && state.LastCompletion.Sequence == 4, "Factual visibility did not resume after reload or invented a completion");
            passed.Add("Commit does not claim visibility; five factual tutorial steps survive reload and later observed presentation");

            var existing = GuideState.Create(GuideOrigin.Existing);
            Require(GuideFlow.Query(existing, zero).Suggestion == null && existing.TutorialBits == 0, "Existing player received fresh steps or invented tutorial history");
            Hint(existing, repaired, GuideIds.Skills);
            var expandedContext = Context(2, null, 0, true);
            var expanded = GuideState.Create(GuideOrigin.LegacyExpanded);
            Hint(expanded, expandedContext, GuideIds.ExpandedR7);
            var expandedComplete = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, 6, true, true, false, false);
            Hint(expanded, expandedComplete, GuideIds.ExpandedR7);
            Require(GuideFlow.Query(expanded, expandedComplete).Suggestion.Body.Contains("now completed") && !GuideFlow.Query(expanded, expandedComplete).Suggestion.Body.Contains("still need"), "Unacknowledged legacy notice invented unfinished projects at chapter end");
            expanded = Reload(GuideFlow.Acknowledge(expanded, expandedContext, GuideIds.ExpandedR7, 20).Apply(expanded));
            Require(GuideFlow.Query(expanded, expandedContext).Suggestion.HintId != GuideIds.ExpandedR7, "Expansion notice resurrected after ACK/reload");
            Fails<InvalidOperationException>(() => GuideFlow.Query(existing, expandedContext), "Context alone fabricated legacy origin");
            Fails<InvalidOperationException>(() => GuideFlow.Query(GuideState.Create(GuideOrigin.LegacyExpanded), repaired), "Legacy origin silently lost its factual marker");
            passed.Add("Existing players skip fresh steps; factual legacy expansion notice persists once and inconsistent provenance rejects");

            var firstCompletion = GuideFlow.Observe(existing, Selection(2, zero, repaired, GameIds.HouseRepair, GameIds.HouseRepair)).Apply(existing);
            firstCompletion = Reload(GuideFlow.Acknowledge(firstCompletion, repaired, GuideIds.Completion, 3, 2).Apply(firstCompletion));
            var improved = Context(2);
            var secondCompletion = Reload(GuideFlow.Observe(firstCompletion, Selection(4, repaired, improved, GameIds.HouseImprove, GameIds.HouseImprove)).Apply(firstCompletion));
            Require(secondCompletion.LastCompletion.ProjectId == GameIds.HouseImprove && secondCompletion.CompletionAcknowledgedSequence == 0 && secondCompletion.LastAckSequence == 3, "New completion retained old acknowledgement or discarded history");
            Fails<InvalidOperationException>(() => GuideFlow.Acknowledge(secondCompletion, improved, GuideIds.Completion, 5, 2), "Late A acknowledgement dismissed new B");
            Fails<InvalidOperationException>(() => GuideFlow.Acknowledge(secondCompletion, improved, GuideIds.Completion, 3, 2), "Replayed old A acknowledgement dismissed B");
            secondCompletion = Reload(GuideFlow.Acknowledge(secondCompletion, improved, GuideIds.Completion, 5, 4).Apply(secondCompletion));
            Require(secondCompletion.CompletionAcknowledgedSequence == 4 && secondCompletion.LastCompletion.EffectInfoKey == "house.improve.effect", "Completion target ACK/key wrong");
            passed.Add("Completion race A2->ACK3->B4: late ACKA rejected, B ACK5 succeeds without losing global history");

            var auto = new GuideObservation(6, GuideOperationKind.ForegroundSpin, improved, improved, "spin.auto", null);
            secondCompletion = Reload(GuideFlow.Observe(secondCompletion, auto).Apply(secondCompletion));
            var evaluation = GuideFlow.Query(secondCompletion, improved);
            Require(evaluation.LastCompletion.ProjectId == GameIds.HouseImprove && evaluation.LastCompletion.Sequence == 4 && secondCompletion.CompletionAcknowledgedSequence == 4, "AUTO erased stable completion");
            Require(evaluation.Suggestion != null && evaluation.Suggestion.HintId == GuideIds.Skills && !evaluation.Suggestion.BlocksActions, "Completion blocked unrelated suggestion/actions");
            var skillContext = Context(2, null, 1);
            secondCompletion = Reload(GuideFlow.Observe(secondCompletion, new GuideObservation(7, GuideOperationKind.SkillPurchase, improved, skillContext, SkillIds.Universal1, null)).Apply(secondCompletion));
            Require(GuideFlow.Query(secondCompletion, skillContext).LastCompletion.ProjectId == GameIds.HouseImprove, "Skill purchase erased completion or invented construction");
            passed.Add("Stable completion/effect key remains independent of hints through AUTO, ACK and skill purchase; no forced modal");

            var a = new GuideObservation(2, GuideOperationKind.ForegroundSpin, zero, zero, "spin.auto", null);
            var observed = GuideFlow.Observe(existing, a).Apply(existing);
            Require(!GuideFlow.Observe(observed, a).Changed, "Exact observed duplicate changed state");
            Fails<InvalidOperationException>(() => GuideFlow.Observe(observed, new GuideObservation(2, GuideOperationKind.ForegroundSpin, zero, zero, "spin.manual", null, true)), "Same-sequence event conflict accepted");
            var b = new GuideObservation(4, GuideOperationKind.ForegroundSpin, zero, zero, "spin.auto", null);
            observed = GuideFlow.Observe(observed, b).Apply(observed);
            string current = GuideStateCodec.Encode(observed);
            Require(GuideStateCodec.Encode(GuideFlow.Observe(observed, a).Apply(observed)) == current, "Older observation rewound new state");
            Require(GuideStateCodec.Encode(GuideFlow.Observe(secondCompletion, a).Apply(secondCompletion)) == GuideStateCodec.Encode(secondCompletion), "Old event lost completion/ACK history");
            passed.Add("Exact latest duplicate is a no-op, same-sequence conflict rejects, older valid events never rewind completion/ACK state");

            var acknowledged = GuideFlow.Acknowledge(fresh, zero, GuideIds.Welcome, 3).Apply(fresh);
            Fails<InvalidOperationException>(() => GuideFlow.Observe(acknowledged, new GuideObservation(3, GuideOperationKind.ForegroundSpin, zero, zero, "spin.auto", null)), "Observation reused ACK sequence");
            Fails<InvalidOperationException>(() => GuideFlow.Observe(acknowledged, a), "Unobserved event older than ACK highwater accepted");
            Fails<InvalidOperationException>(() => GuideFlow.Acknowledge(acknowledged, zero, GuideIds.ChooseProject, 3), "Different ACK at same sequence accepted");
            acknowledged = GuideFlow.Acknowledge(acknowledged, zero, GuideIds.Welcome, 5).Apply(acknowledged);
            Require(acknowledged.LastAckSequence == 5 && acknowledged.IsAcknowledged(GuideIds.Welcome), "Repeated hint ACK did not preserve monotonic identity");
            Fails<InvalidOperationException>(() => welcome.Apply(acknowledged), "Stale proposal replaced current progress");
            Require(!GuideFlow.Acknowledge(acknowledged, zero, GuideIds.Welcome, 5).Changed, "Exact ACK retry changed twice");
            passed.Add("ACK/observation highwater prevents cross-kind reuse, stale metadata and conflicting ACK; exact retry idempotent");

            var eraBefore = new GuideContext(Stages(2, 2), null, 2, false, false, false, false);
            Hint(existing, eraBefore, GuideIds.EraReady);
            var eraAfter = new GuideContext(Stages(3, 3, EraIds.WorkshopYard), null, 2, false, false, false, false);
            var entered = GuideFlow.Observe(existing, new GuideObservation(8, GuideOperationKind.EraEntry, eraBefore, eraAfter, ChapterIds.UnlockWorkshopYard, null)).Apply(existing);
            Require(entered.LastCompletion == null && entered.TutorialBits == 0, "Era entry fabricated project/tutorial completion");
            var chapter = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, 4, false, true, false, false);
            Hint(existing, chapter, GuideIds.ChapterEnd);
            var endText = GuideFlow.Query(existing, chapter).Suggestion.Body;
            Require(endText.Contains("optional") && endText.Contains("no further era"), "Chapter end promised missing successor");
            var endAck = GuideFlow.Acknowledge(existing, chapter, GuideIds.ChapterEnd, 9).Apply(existing);
            Hint(endAck, chapter, GuideIds.Orders);
            var activeOrder = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, 4, false, true, true, false);
            var activeAdvice = GuideFlow.Query(endAck, activeOrder).Suggestion;
            Require(activeAdvice.HintId == GuideIds.Orders && activeAdvice.Body.Contains("Continue") && !activeAdvice.Body.Contains("accept"), "Active order guidance asks acceptance instead of continuing foreground progress");
            var allSkills = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, 6, false, true, true, true);
            string beforeOrderQueries = GuideStateCodec.Encode(endAck);
            foreach (int skills in new[] { 0, 6 })
            {
                var available = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, skills, false, true, false, false);
                var progressing = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, skills, false, true, true, false);
                var ready = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, skills, false, true, true, true);
                Require(GuideFlow.OrderMode(available) == OrderGuidanceMode.Choose && GuideFlow.Query(endAck, available).Suggestion.Body.Contains("accept"), "Idle orders lost optional acceptance advice");
                Require(GuideFlow.OrderMode(progressing) == OrderGuidanceMode.Continue && GuideFlow.Query(endAck, progressing).Suggestion.Body.Contains("Continue") && !GuideFlow.Query(endAck, progressing).Suggestion.Body.Contains("accept"), "Active order advice ignores its existing instance");
                Require(GuideFlow.OrderMode(ready) == OrderGuidanceMode.Claim && GuideFlow.Query(endAck, ready).Suggestion.Body.Contains("claim explicitly") && !GuideFlow.Query(endAck, ready).Suggestion.Body.Contains("Continue"), "Ready order advice asks more spins instead of explicit claim");
                var ordersAck = Reload(GuideFlow.Acknowledge(endAck, progressing, GuideIds.Orders, 10).Apply(endAck));
                foreach (var orderContext in new[] { available, progressing, ready })
                    Require(GuideFlow.Query(ordersAck, orderContext).Suggestion?.HintId != GuideIds.Orders, "Changed order state resurrected the finite acknowledged hint");
                Require(GuideFlow.Query(existing, ready).Suggestion.HintId == GuideIds.ChapterEnd, "Order readiness displaced the honest chapter ending");
            }
            Require(GuideStateCodec.Encode(endAck) == beforeOrderQueries, "Order guidance inspection mutated acknowledgement/history");
            Require(GuideFlow.Query(existing, allSkills).Suggestion.Body.Contains("repeatable") && !GuideFlow.Query(existing, allSkills).Suggestion.Body.Contains("all orders are finished"), "Repeated orders incorrectly terminal");
            var noOrders = new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, 6, false, false, false, false);
            Fails<InvalidOperationException>(() => GuideFlow.OrderMode(noOrders), "Order advice invented unavailable orders");
            Fails<ArgumentNullException>(() => GuideFlow.OrderMode(null), "Order advice accepted missing immutable facts");
            Require(!GuideFlow.Query(existing, noOrders).Suggestion.Body.Contains("orders") && GuideFlow.Query(existing, noOrders).Suggestion.Body.Contains("no further era"), "Chapter end invented unavailable repeat orders");
            passed.Add("Explicit era2->3 observation invents no project; honest finite chapter end keeps optional repeat orders/skills separate");

            Fails<ArgumentException>(() => new GuideContext(Stages(1), GameIds.HouseRepair, 0, false, false, false, false), "Already-built target accepted");
            Fails<ArgumentException>(() => new GuideContext(Stages(), null, 0, false, true, false, false), "Orders unlocked before chapter");
            Fails<ArgumentException>(() => new GuideContext(Stages(5, 5, EraIds.WorkshopYard), null, 7, false, false, false, false), "Unbounded skill count accepted");
            Fails<InvalidOperationException>(() => GuideFlow.Observe(existing, new GuideObservation(2, GuideOperationKind.ForegroundSpin, zero, repaired, "spin.auto", GameIds.HouseRepair)), "Unselected project completion accepted");
            Fails<InvalidOperationException>(() => GuideFlow.Observe(existing, Selection(2, zero, repaired, GameIds.HouseImprove, GameIds.HouseRepair)), "Selection action and completion conflict accepted");
            Fails<InvalidOperationException>(() => GuideFlow.Observe(existing, new GuideObservation(2, GuideOperationKind.SkillPurchase, zero, repaired, SkillIds.Universal1, GameIds.HouseRepair)), "Skill fabricated construction");
            Fails<InvalidOperationException>(() => GuideFlow.Observe(existing, new GuideObservation(2, GuideOperationKind.ForegroundSpin, zero, zero, "spin.auto", null, false, (int)TutorialSteps.AutoInformed)), "AUTO informed before repair");
            Fails<InvalidOperationException>(() => GuideFlow.Observe(existing, new GuideObservation(2, GuideOperationKind.ForegroundSpin, zero, zero, "spin.auto", null, false, (int)TutorialSteps.ManualSpin)), "AUTO fabricated manual step");
            passed.Add("Typed pre/post observations reject fabricated completions, wrong targets, invalid skills/orders and false manual/AUTO facts");

            foreach (var origin in new[] { GuideOrigin.Fresh, GuideOrigin.Existing, GuideOrigin.LegacyExpanded }) Reload(GuideState.Create(origin));
            foreach (var checkpoint in new[] { state, expanded, firstCompletion, secondCompletion, observed, acknowledged, entered, endAck }) Reload(checkpoint);
            string good = GuideStateCodec.Encode(secondCompletion);
            var bad = new[] { "", "g2|0|0|0|0|-|-|0|0|-|0", good + "|extra", good.Replace("g1|", "g1| "), good.Replace("g1|1|", "g1|01|"),
                "g1|1|256|0|0|-|-|0|0|-|0", "g1|1|0|32|0|-|-|0|0|-|0", "g1|1|0|1|0|-|-|0|0|-|0",
                "g1|1|0|0|0|-|unknown.project|1|0|-|0", "g1|1|0|0|1|-|-|0|0|-|0", "g1|1|0|0|0|-|-|1|0|-|0", new string('x', 4097) };
            foreach (string payload in bad) Fails<FormatException>(() => GuideStateCodec.Decode(payload), "Malformed codec payload accepted");
            Fails<FormatException>(() => GuideStateCodec.Decode(null), "Present null payload accepted");
            Require(GuideStateCodec.DecodeOrDefault(null, GuideOrigin.Existing).Origin == GuideOrigin.Existing, "Missing payload inferred fresh origin");
            Require(good.Length < 1024, "Bounded normal guidance payload unexpectedly large");
            passed.Add("Strict bounded codec preserves origin/steps/ACK/completion and rejects malformed/version/size/null/noncanonical fields");
            return passed;
        }
    }
}
