using DataPackChecker.Shared;
using DataPackChecker.Shared.Data;
using DataPackChecker.Shared.Util;
using System.Collections.Generic;
using System.Text.Json;
using DataPackChecker.Shared.Data.Resources;
using System.Text.RegularExpressions;
using System;

namespace Core.Update {
    public class To_1_17 : CheckerRule {
        public override string Title => "Data packs made in 1.16 should work in 1.17.";

        public override string Description => "This rule will report issues and suggestions to help updating 1.16 data packs for 1.17.";

        public override List<string> GoodExamples => new List<string>() { "summon marker ~ ~ ~ {...}" };

        public override List<string> BadExamples => new List<string>() { "summon armor_stand ~ ~ ~ {Marker:1b,...}" };

        // Armor stand and area effect cloud regexes.
        private static readonly Regex markerASRegex = new Regex(@"^summon (minecraft:)?armor_stand \S+ \S+ \S+ .*?Marker\s*?:\s*?1.*$", RegexOptions.ExplicitCapture | RegexOptions.Compiled);
        private static readonly Regex aecRegex = new Regex(@"^summon (minecraft:)?area_effect_cloud.*$", RegexOptions.ExplicitCapture | RegexOptions.Compiled);
        private static readonly Regex emptyAECRegex = new Regex(@"^summon (minecraft:)?area_effect_cloud \S+ \S+ \S+ (?!.*?(Effects|Potion|Particle)\s*?:).*$", RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        // Rename regexes.
        private static readonly Regex BlockCondition = new Regex(@"""block""\s*?:\s*?""", RegexOptions.Compiled);
        private static readonly Regex ItemCondition = new Regex(@"""item""\s*?:\s*?""", RegexOptions.Compiled);

        public override void Run(DataPack pack, JsonElement? config, Output output) {
            CheckFormat(pack, output);
            CheckFunctions(pack, output);
            CheckDimensionTypes(pack, output);
            CheckAdvancements(pack, output);
            CheckPredicates(pack, output);
            CheckLootTables(pack, output);
            CheckRecipes(pack, output);
            CheckItemModifiers(pack, output);
            CheckTags(pack, output);
            CheckWorldGen(pack, output);
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

        private void CheckFunctions(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var f in ns.Functions) {
                    foreach (var c in f.CommandsFlat) {
                        if (c.ContentType != Command.Type.Command) continue;
                        if (markerASRegex.IsMatch(c.Raw) || emptyAECRegex.IsMatch(c.Raw) || (aecRegex.IsMatch(c.Raw) && c.Arguments.Count < 5)) {
                            output.Error(c, "Armor stands and area effect clouds without visual appearance should be replaced by the new marker entity.\nMarkers are never ticked or transmitted to the client, resulting in better performance. ");
                        }
                        if (c.CommandKey == "replaceitem") {
                            output.Error(c, "The 'replaceitem ...' command is replaced by 'item replace ...'.");
                        }
                        if (c.Raw.StartsWith("scoreboard objectives add") && c.Raw.Contains("minecraft.custom:minecraft.play_one_minute")) {
                            output.Error(c, "The minecraft.custom:minecraft.play_one_minute objective has been replaced by minecraft.custom:minecraft.play_time.");
                        }
                        CheckBlockReplacement(c.Raw, m => output.Error(c, m));
                    }
                }
            }
        }

        private void CheckDimensionTypes(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var dimType in ns.DimensionData.DimensionTypes) {
                    CheckDimensionType(dimType, dimType.Content, output);
                }
                foreach (var dim in ns.DimensionData.Dimensions) {
                    var content = dim.Content.ToString();
                    CheckBlockReplacement(content, m => output.Error(dim, m));
                    if (!dim.Content.TryAsObject("type", out var dimType)) continue;
                    CheckDimensionType(dim, dimType, output);
                }
            }
        }

        private void CheckDimensionType(JsonResource resource, JsonElement element, Output output) {
            if (!element.TryAsInt("min_y", out _)) {
                output.Error(resource, element, "New required property 'min_y' is missing here.");
            }
            if (!element.TryAsInt("height", out _)) {
                output.Error(resource, element, "New required property 'height' is missing here.");
            }
        }

        private void CheckAdvancements(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var advancement in ns.Advancements) {
                    var content = advancement.Content.ToString();
                    CheckConditionReplacement(advancement, content, output);
                    CheckBlockReplacement(content, m => output.Error(advancement, m));
                }
            }
        }

        private void CheckPredicates(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var predicate in ns.Predicates) {
                    var content = predicate.Content.ToString();
                    CheckConditionReplacement(predicate, content, output);
                    CheckBlockReplacement(content, m => output.Error(predicate, m));
                }
            }
        }

        private void CheckLootTables(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var lootTable in ns.LootTables) {
                    var content = lootTable.Content.ToString();
                    CheckConditionReplacement(lootTable, content, output);
                    CheckBlockReplacement(content, m => output.Error(lootTable, m));
                }
            }
        }

        private void CheckRecipes(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var recipe in ns.Recipes) {
                    var content = recipe.Content.ToString();
                    CheckBlockReplacement(content, m => output.Error(recipe, m));
                }
            }
        }

        private void CheckTags(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var blockTag in ns.TagData.BlockTags) {
                    blockTag.Entries.ForEach(e => CheckBlockReplacement(e.Identifier, m => output.Error(blockTag, e.Identifier, m)));
                }
                foreach (var itemTag in ns.TagData.ItemTags) {
                    itemTag.Entries.ForEach(e => CheckBlockReplacement(e.Identifier, m => output.Error(itemTag, e.Identifier, m)));
                }
            }
        }

        private void CheckItemModifiers(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var itemModifier in ns.ItemModifiers) {
                    var content = itemModifier.Content.ToString();
                    CheckBlockReplacement(content, m => output.Error(itemModifier, m));
                }
            }
        }

        private void CheckWorldGen(DataPack pack, Output output) {
            foreach (var ns in pack.Namespaces) {
                foreach (var configuredFeature in ns.WorldGenData.ConfiguredFeatures) {
                    var content = configuredFeature.Content.ToString();
                    CheckBlockReplacement(content, m => output.Error(configuredFeature, m));
                }
                foreach (var configuredSurfaceBuilder in ns.WorldGenData.ConfiguredSurfaceBuilders) {
                    var content = configuredSurfaceBuilder.Content.ToString();
                    CheckBlockReplacement(content, m => output.Error(configuredSurfaceBuilder, m));
                }
                foreach (var processor in ns.WorldGenData.ProcessorLists) {
                    var content = processor.Content.ToString();
                    CheckBlockReplacement(content, m => output.Error(processor, m));
                }
            }
        }

        private void CheckConditionReplacement(JsonResource resource, string rawText, Output output) {
            if (BlockCondition.IsMatch(rawText)) {
                output.Error(resource, "This item contains a string 'block' key, which might indicate a condition that checks a block. If so, replace the 'block' string field by a 'blocks' string list of block ids.");
            }
            if (ItemCondition.IsMatch(rawText)) {
                output.Error(resource, "This item contains a string 'item' key, which might indicate a condition that checks an item. If so, replace the 'item' string field by an 'items' string list of item ids.");
            }
        }

        private void CheckBlockReplacement(string rawText, Action<string> onError) {
            if (rawText.Contains("grass_path")) {
                onError("Contains 'grass_path', which might refer to the block/item id. If so, replace with 'dirt_path'.");
            }
        }
    }
}
