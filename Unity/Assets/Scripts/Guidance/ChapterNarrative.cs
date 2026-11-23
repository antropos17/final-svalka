namespace JunkToNeon.Guidance
{
    public sealed class NarrativeBeat
    {
        public string ProjectId { get; }
        public string Title { get; }
        public string Body { get; }

        internal NarrativeBeat(string projectId, string title, string body)
        { ProjectId = projectId; Title = title; Body = body; }
    }

    // Presentation-only content. Callers choose the moment from existing committed facts.
    public static class ChapterNarrative
    {
        public const string CompletionPayoff="The yard is complete. Potato named it Neon.";
        public static string Opening => "Potato needs a roof. Start by repairing the house. Pigeon promises to watch the yard.";
        public static string EraReady => "The yard has shelter and a place to work. Potato suggests Workshop Yard. Pigeon volunteers to supervise.";
        public static string EraEntry => "The yard works. Now make it home: fit out each building for everyday life. Pigeon brought tools.";
        public static string ChapterEnd => "Forgotten things became a home. Potato named it Neon. Pigeon declared himself honorary neighbor. Chapter complete.";

        static readonly NarrativeBeat[] completions =
        {
            new NarrativeBeat("house.repair", "The roof is ready.", "Potato is out of the rain. Time to build up the yard; Pigeon is already choosing a workspace."),
            new NarrativeBeat("house.improve", "More room at home.", "Potato has a corner of her own. Required for the next era; no extra income. Pigeon took the credit."),
            new NarrativeBeat("workshop.build", "Finds have a purpose.", "Potato set up a workspace to reuse finds. Pigeon declared himself Workshop manager."),
            new NarrativeBeat("workshop.improve", "Work runs smoother.", "Every tool has its place. Except the one Pigeon borrowed again."),
            new NarrativeBeat("recycler.build", "Finds are sorted.", "Potato sorts the useful from the useless. Pigeon inspects every shiny thing."),
            new NarrativeBeat("recycler.improve", "Less junk, more use.", "Sorting is easier. Pigeon insists his bottle-cap collection stays untouched."),
            new NarrativeBeat("pigeon-loft.build", "Pigeon has an address.", "He immediately declared it the yard's main address. Deliveries now have a starting point."),
            new NarrativeBeat("pigeon-loft.improve", "Pigeon expands.", "More space. Pigeon calls it a branch office with a reception room."),
            new NarrativeBeat("cart.build", "Finds have wheels.", "Potato no longer carries things one by one. Pigeon prefers riding on top."),
            new NarrativeBeat("cart.improve", "The Cart can do more.", "Hauling finds is easier. Pigeon added logistics to his job title."),
            new NarrativeBeat("power-source.build", "Power for the yard.", "Potato built a power source from found Parts. Pigeon insists it was all planned."),
            new NarrativeBeat("power-source.improve", "More reliable power.", "Potato finished the assembly. Pigeon put himself in charge of looking important."),
            new NarrativeBeat("house.workshop-yard.upgrade-1", "Making the house home.", "Potato is settling in for good. Pigeon helped with a shelf, but still visits without knocking."),
            new NarrativeBeat("house.workshop-yard.upgrade-2", "Ready for everyday life.", "A place to stay, not just shelter. Pigeon brought a find for the house and promptly demanded praise."),
            new NarrativeBeat("workshop.workshop-yard.upgrade-1", "Workshop gains momentum.", "Potato improves the workspace. Pigeon holds a meeting with its only attendee: himself."),
            new NarrativeBeat("workshop.workshop-yard.upgrade-2", "Workshop knows its work.", "Old things are useful again. Pigeon brought a tool, helped assemble, then demanded a break."),
            new NarrativeBeat("recycler.workshop-yard.upgrade-1", "Putting finds to use.", "Potato improves sorting. Pigeon finally agrees to separate spare Parts from his collection."),
            new NarrativeBeat("recycler.workshop-yard.upgrade-2", "Order becomes a habit.", "Useful things no longer vanish in the pile. Pigeon makes an exception for his favorite cap."),
            new NarrativeBeat("pigeon-loft.workshop-yard.upgrade-1", "Pigeon has big plans.", "He fits out his place and lists important flights. Potato asks him to start with useful ones."),
            new NarrativeBeat("pigeon-loft.workshop-yard.upgrade-2", "The main address is ready.", "Pigeon reinforced his perch and fitted out his place. A home now, not just storage for finds."),
            new NarrativeBeat("cart.workshop-yard.upgrade-1", "The Cart keeps pace.", "Potato readies it for more finds. Pigeon practices supervising the loading."),
            new NarrativeBeat("cart.workshop-yard.upgrade-2", "A route for every find.", "The Cart is fully fitted out. Pigeon still believes the most valuable cargo rides on top."),
            new NarrativeBeat("power-source.workshop-yard.upgrade-1", "Power serves the yard.", "Potato improves the power source. Pigeon suggests naming it after himself."),
            new NarrativeBeat("power-source.workshop-yard.upgrade-2", "Power has its place.", "Assembly complete. Potato is pleased with the work; Pigeon is pleased he witnessed another success.")
        };

        public static bool TryCompletion(string projectId, out NarrativeBeat beat)
        {
            foreach (var candidate in completions)
                if (candidate.ProjectId == projectId) { beat = candidate; return true; }
            beat = null;
            return false;
        }

        public static string OrderIntroduction(string orderId)
        {
            switch (orderId)
            {
                case "order.sort-salvage": return "Potato suggests sorting the finds. Pigeon asks to keep his favorite cap. Optional order.";
                case "order.neighborhood-supply": return "The neighbors need deliveries. Pigeon promises to fly as well as supervise. Optional order.";
                case "order.gather-potatoes": return "Pigeon will count Potatoes on the reels. Potato agrees to pose. Optional order.";
                case "order.pack-boxes": return "Pigeon calls counting Boxes packing. Potato asks him to count symbols, not promises. Optional order.";
                default: return null;
            }
        }

        // Ready means the target was reached, not that its reward was collected.
        public static string OrderReady(string orderId)
        {
            switch (orderId)
            {
                case "order.sort-salvage": return "Sorting done. Pigeon's favorite cap is safe. Collect your reward; continuing is optional.";
                case "order.neighborhood-supply": return "Deliveries done. Pigeon requests a review of his importance. Collect your reward; continuing is optional.";
                case "order.gather-potatoes": return "Required Potato appearances counted. Pigeon lost count only afterward. Collect your reward; continuing is optional.";
                case "order.pack-boxes": return "Required Box appearances counted. Pigeon adds an imaginary stamp. Collect your reward; continuing is optional.";
                default: return null;
            }
        }

        // Use only for a newly committed, matching claim receipt. The lookup is not proof
        // of payment; Ready, replay, reload, cancellation and recovery must not trigger it.
        public static string OrderClaimed(string orderId)
        {
            switch (orderId)
            {
                case "order.sort-salvage": return "Sorting paid. Potato likes the order; Pigeon likes his cap. Another order is optional.";
                case "order.neighborhood-supply": return "Deliveries paid. Pigeon is ready to sign his own thank-you note. Another order is optional.";
                case "order.gather-potatoes": return "Potato count paid. Potato asks not to be counted twice. Another order is optional.";
                case "order.pack-boxes": return "Box count paid. Pigeon declares accounting a fine art. Another order is optional.";
                default: return null;
            }
        }

        // Family purpose across the existing two eras, suitable before purchase. Exact
        // next-stage effects, prices and current ownership remain the caller's facts.
        public static string FamilyOverview(string familyId)
        {
            switch (familyId)
            {
                case "house": return "The house starts the yard: its first repair unlocks AUTO. Workshop Yard upgrades boost Potato symbols.";
                case "workshop": return "Workshop adds spin Coins and offline income; its upgrades boost Box symbols. Pigeon already wants to manage it.";
                case "recycler": return "Recycler adds Coins for Trash symbols. Potato seeks useful finds; Pigeon protects his cap.";
                case "pigeon-loft": return "Pigeon Loft unlocks delivery Coins; upgrades boost Pigeon symbols. Pigeon gets an address for important flights.";
                case "cart": return "Cart adds Coins for Box symbols. Finds for Potato; a reason for Pigeon to supervise loading.";
                case "power-source": return "Power Source adds Coins for Potato symbols. Potato handles the Parts; Pigeon handles the grand name.";
                default: return null;
            }
        }
    }
}
