namespace JunkToNeon.Core
{
    // Stateless presentation only. Catalog IDs resolve exact offers; resource/category/
    // section/action keys are UI copy keys, never economic commands. Unknowns return
    // null so callers can preserve their authoritative fallback. Prices, amounts,
    // ownership, caps and prerequisite status must come from the current quote/state.
    public static class ExpansionCopy
    {
        public static string FirstUse => "Main spins are free. Parts buy modules and equipment; Potatoes buy upgrades or trade for Coins; Energy powers boosts. Resources come from new spins or explicit Diamond purchases of Parts or Potatoes, never retroactively or on a timer.";

        public static string Label(string id)
        {
            switch (id)
            {
                case "resource.coins": return "Coins";
                case "resource.parts": return "Parts";
                case "resource.potatoes": return "Potatoes";
                case "resource.energy": return "Energy";
                case "section.shop": return "Shop";
                case "section.loadout": return "Slot Setup";
                case "section.resources": return "Resources";
                case "section.equipment": case "category.equipment": return "Equipment";
                case "category.module": return "Modules";
                case "category.socket": return "Support Slots";
                case "category.theme": return "Themes";
                case "category.bonus": return "Bonus Reel";
                case "category.yield": return "Potatoes";
                case "category.trade": return "Exchange";
                case "category.boost": return "Booster";
                case "category.speed": return "Spin Speed";
                case "category.workbench": return "Bonus Workbench";
                case "category.focus": return "Bonus Focus";
                case "category.preset": return "Module Presets";
                case "action.buy": return "Buy";
                case "action.equip": return "Equip";
                case "action.theme": return "Apply";
                case "action.boost": return "Activate Booster";
                case "action.speed": return "Choose Speed";
                case "action.focus": return "Choose Focus";
                case "action.preset": return "Apply Preset";
                case "module.salvage": return "Salvage Module";
                case "module.packing": return "Packing Module";
                case "module.harvest": return "Harvest Module";
                case "module.combo": return "Combo Container";
                case "module.dynamo": return "Dynamo";
                case "module.none": return "Remove Module";
                case "socket.2": case "socket.3": return "Support Slot";
                case "theme.neon": return "Neon";
                case "theme.amber": return "Amber";
                case "theme.mint": return "Mint";
                case "theme.violet": return "Violet";
                case "theme.frost": return "Frost";
                case "theme.rose": return "Rose";
                case "bonus.unlock": return "Bonus Reel";
                case "workbench.bonus.1": case "workbench.bonus.2": return "Bonus Workbench";
                case "focus.balanced": return "Balanced";
                case "focus.parts": return "Parts";
                case "focus.potatoes": return "Potatoes";
                case "preset.parts": return "Parts Preset";
                case "preset.harvest": return "Potato Preset";
                case "preset.energy": return "Energy Preset";
                case "guidance.loadout": return "Your Modules";
                case "guidance.resources": return "Reward Space";
                case "guidance.bonus": return "Bonus Order";
                case "boost.next-bonus": return "Bonus Booster";
                case "speed.normal": return "Normal Speed";
                case "speed.fast": return "Fast Spins";
                case "potato.yield.1": case "potato.yield.2": return "More Potatoes";
                case "trade.parts": return "Parts to Coins";
                case "trade.potatoes": return "Potatoes to Coins";
                case "house": return "House";
                case "workshop": return "Workshop";
                case "recycler": return "Recycler";
                case "pigeon-loft": return "Pigeon Loft";
                case "cart": return "Cart";
                case "power-source": return "Power Source";
                case "equipment.house.1": case "equipment.house.2": return "House · Resource Capacity";
                case "equipment.workshop.1": case "equipment.workshop.2": return "Workshop · Parts";
                case "equipment.recycler.1": case "equipment.recycler.2": return "Recycler · Parts";
                case "equipment.pigeon-loft.1": case "equipment.pigeon-loft.2": return "Pigeon Loft · Energy Capacity";
                case "equipment.cart.1": case "equipment.cart.2": return "Cart · Parts";
                case "equipment.power-source.1": case "equipment.power-source.2": return "Power Source · Energy";
                default: return null;
            }
        }

        public static string Description(string id)
        {
            switch (id)
            {
                case "resource.coins": return "Coins buy construction and skills. Exchange Parts and Potatoes for Coins in the shop.";
                case "resource.parts": return "Trash and Box symbols on new spins grant Parts. Use them in the shop for modules, equipment and other upgrades.";
                case "resource.potatoes": return "Potato symbols on new spins grant Potatoes. Use them for shop upgrades or exchange them for Coins.";
                case "resource.energy": return "Energy builds up on new spins to its cap and powers the bonus booster. Main spins are free.";
                case "section.shop": return "Spend earned resources on optional upgrades or exchanges. Browsing buys nothing.";
                case "section.loadout": return "Equip purchased modules in support slots and choose an available theme. Main reel rules stay the same.";
                case "section.resources": return "New spins grant resources up to storage caps. Resources do not accumulate offline and past spins are not recalculated.";
                case "section.equipment": case "category.equipment": return "Equipment requires its main building. Its levels are optional and do not count toward main chapter progress.";
                case "category.module": return "A module works in a support slot and improves collection of its resource. One module cannot occupy multiple slots.";
                case "category.socket": return "Extra slots allow more different purchased modules to be equipped.";
                case "category.theme": return "Themes change the slot appearance. Rewards and main symbol odds are unaffected.";
                case "category.bonus": return "A separate bonus reel grants resources after new spins. Its reward does not change main reel payouts.";
                case "category.yield": return "Adds Potatoes for Potato symbols on future spins. This is a shop upgrade, not a new building.";
                case "category.trade": return "An exchange spends the selected resource and grants Coins after confirmation. It does not pay for the selected construction.";
                case "category.boost": return Description("boost.next-bonus");
                case "category.speed": return "Speed changes spin presentation time. Symbol odds, payouts and resources per spin stay the same.";
                case "category.workbench": return "The workbench adds focus and a resource bonus increase. Main reel rules, Coins and symbol odds stay the same.";
                case "category.focus": return "Focus is free after buying the workbench. It changes only future resource bonus distribution, not Coins or symbol odds.";
                case "category.preset": return "Presets place purchased modules in open slots. They replace the current loadout only after explicit confirmation.";
                case "action.buy": return "A purchase requires explicit confirmation of the displayed cost and conditions.";
                case "action.equip": return "Equip a purchased module in the selected open support slot.";
                case "action.theme": return "Apply an available theme. This setting does not change payouts or symbol odds.";
                case "action.boost": return Description("boost.next-bonus");
                case "action.speed": return Description("category.speed");
                case "action.focus": return Description("category.focus");
                case "action.preset": return Description("category.preset");
                case "module.salvage": return "A module in a support slot adds Parts for each Trash symbol on new spins.";
                case "module.packing": return "A module in a support slot adds Parts for each Box symbol on new spins.";
                case "module.harvest": return "A module in a support slot adds Potatoes for Potato symbols on new spins.";
                case "module.combo": return "The equipped module adds Parts and Potatoes for a pair or triple of matching main symbols. Once per spin, not per symbol.";
                case "module.dynamo": return "The equipped module adds Energy if a Pigeon appears on the main reels. Once per spin, not per Pigeon.";
                case "module.none": return "Frees the selected slot; the module remains purchased. To move it, first free its previous slot.";
                case "socket.2": case "socket.3": return "Opens another slot for a purchased module. One module cannot occupy multiple slots.";
                case "theme.neon": case "theme.amber": case "theme.mint": case "theme.violet": return "Changes the slot to the selected theme. Rewards and symbol odds stay the same.";
                case "theme.frost": case "theme.rose": return Description("theme.neon");
                case "bonus.unlock": return "Unlocks a separate resource bonus reel. It triggers on new spins and does not change main reel payouts.";
                case "workbench.bonus.1": return "Unlocks free bonus focus selection. It redistributes the existing total between Parts and Potatoes before the booster and storage caps.";
                case "workbench.bonus.2": return "Adds Parts and Potatoes to the raw bonus reward. Focus, the booster and storage caps apply afterward.";
                case "focus.balanced": return "Keeps the resource bonus distribution. The workbench addition comes before the booster, then storage caps apply.";
                case "focus.parts": return "Combines bonus Parts and Potatoes into Parts before the booster. Excess is not shifted to Potatoes when storage fills.";
                case "focus.potatoes": return "Combines bonus Parts and Potatoes into Potatoes before the booster. Excess is not shifted to Parts when storage fills.";
                case "preset.parts": return "Prioritizes Parts. The shown preset replaces modules only after confirmation; it uses purchased modules and open slots.";
                case "preset.harvest": return "Prioritizes Potatoes. The shown preset replaces modules only after confirmation; it uses purchased modules and open slots.";
                case "preset.energy": return "Prioritizes Energy. The shown preset replaces modules only after confirmation; it uses purchased modules and open slots.";
                case "guidance.loadout": return "Current loadout: equipped modules. Compare them with your resource needs; a new preset replaces them only after confirmation.";
                case "guidance.resources": return "Storage caps can reduce the reward. Make room with an exchange or focus on the resource you need before the next bonus.";
                case "guidance.bonus": return "The workbench addition comes before focus; the booster follows focus and applies only once. Storage caps can reduce the credit.";
                case "boost.next-bonus": return "Boosts the next bonus reward within storage caps. Energy is spent on activation; you cannot activate it again before the bonus.";
                case "speed.normal": case "speed.fast": return Description("category.speed");
                case "potato.yield.1": case "potato.yield.2": return Description("category.yield");
                case "trade.parts": return "Exchanges Parts for Coins at the displayed rate. It does not buy the selected construction or change its saved price.";
                case "trade.potatoes": return "Exchanges Potatoes for Coins at the displayed rate. It does not buy the selected construction or change its saved price.";
                case "house": case "equipment.house.1": case "equipment.house.2": return "House equipment increases Parts and Potato storage capacity. Main House upgrades are counted separately.";
                case "workshop": case "equipment.workshop.1": case "equipment.workshop.2": return "Workshop equipment adds Parts on future spins. Main Workshop upgrades are counted separately.";
                case "recycler": case "equipment.recycler.1": case "equipment.recycler.2": return "Recycler equipment adds Parts for Trash symbols on future spins. Main upgrades are counted separately.";
                case "pigeon-loft": case "equipment.pigeon-loft.1": case "equipment.pigeon-loft.2": return "Pigeon Loft equipment increases Energy storage capacity. Main Pigeon Loft upgrades are counted separately.";
                case "cart": case "equipment.cart.1": case "equipment.cart.2": return "Cart equipment adds Parts for Box symbols on future spins. Main Cart upgrades are counted separately.";
                case "power-source": case "equipment.power-source.1": case "equipment.power-source.2": return "Power Source equipment adds Energy on future spins. Main Power Source upgrades are counted separately.";
                default: return null;
            }
        }
    }
}
