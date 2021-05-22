using DataPackChecker.Shared;
using DataPackChecker.Shared.Data;
using DataPackChecker.Shared.Util;
using System.Collections.Generic;
using System.Text.Json;

namespace Core.Update {
    public class To_1_17 : CheckerRule {
#warning Check for AEC and Marker Armor stands, suggest markers.
#warning Loot table score condition now has a "target" parameter instead of "entity".
#warning Statistic and scoreboard objective minecraft.custom:minecraft.play_one_minute has been renamed to minecraft.custom:minecraft.play_time
#warning Dimension types now have parameters for min and max height.
#warning /replaceitem is replaced by /item replace

        public override string Title => "Data packs made in 1.16 should work in 1.17.";

        public override string Description => "This rule will report issues and suggestions to help updating 1.16 data packs for 1.17.";

        public override List<string> GoodExamples => new List<string>() { "summon marker ~ ~ ~ {...}" };

        public override List<string> BadExamples => new List<string>() { "summon armor_stand ~ ~ ~ {Marker:1b,...}" };

        public override void Run(DataPack pack, JsonElement? config, Output output) {
            CheckFormat(pack, output);
        }

        private void CheckFormat(DataPack pack, Output output) {
            if (pack.Meta.TryAsObject("pack", out JsonElement metaPack) &&
                metaPack.TryAsInt("pack_format", out int metaFormat)) {
                if (metaFormat < 6) {
                    output.Error($"This rule does not support data pack format {metaFormat}. Consider updating it to be compatible with format 6 first.");
                } else if (metaFormat > 7) {
                    output.Error($"The specified data pack is made for a newer format ({metaFormat}) and should not be used with this rule.");
                }
                if (metaFormat != 7) {
                    output.Error("The data pack format must be set to 7 in order to be compatible with Minecraft 1.17.");
                }
            }
        }
    }
}
