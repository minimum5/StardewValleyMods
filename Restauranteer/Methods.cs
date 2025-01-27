using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;
using System.Collections.Generic;
using System.Linq;
using Object = StardewValley.Object;

namespace Restauranteer
{
    public partial class ModEntry
    {
        private void UpdateOrders()
        {
            foreach(var c in Game1.player.currentLocation.characters)
            {

                if (c.IsVillager && !Config.IgnoredNPCs.Contains(c.Name))
                {
                    CheckOrder(c, Game1.player.currentLocation);
                }
                else
                {
                    c.modData.Remove(orderKey);
                }
            }
        }

        private void CheckOrder(NPC npc, GameLocation location)
        {
            if (npc.modData.TryGetValue(orderKey, out string orderData))
            {
                //npc.modData.Remove(orderKey);
                UpdateOrder(npc, JsonConvert.DeserializeObject<OrderData>(orderData));
                return;
            }
            if (!Game1.NPCGiftTastes.ContainsKey(npc.Name) || npcOrderNumbers.TryGetValue(npc.Name, out int amount) && amount >= Config.MaxNPCOrdersPerNight)
                return;
            if(Game1.random.NextDouble() < Config.OrderChance)
            {
                StartOrder(npc, location);
            }
        }

        private void UpdateOrder(NPC npc, OrderData orderData)
        {
            if (!npc.IsEmoting)
            {
                npc.doEmote(424242, false);
            }
        }

        private void StartOrder(NPC npc, GameLocation location)
        {
            List<string> loves = new();
            foreach (var str in Game1.NPCGiftTastes["Universal_Love"].Split(' '))
            {
                if (Game1.objectData.TryGetValue(str, out var data) && data != null && CraftingRecipe.cookingRecipes.ContainsKey(data.Name))
                {
                    loves.Add(str);
                }
            }
            foreach (var str in Game1.NPCGiftTastes[npc.Name].Split('/')[1].Split(' '))
            {
                if (Game1.objectData.TryGetValue(str, out var data) && data != null && CraftingRecipe.cookingRecipes.ContainsKey(data.Name))
                {
                    loves.Add(str);
                }
            }
            List<string> likes = new();
            foreach (var str in Game1.NPCGiftTastes["Universal_Like"].Split(' '))
            {
                if (Game1.objectData.TryGetValue(str, out var data) && data != null && CraftingRecipe.cookingRecipes.ContainsKey(data.Name))
                {
                    likes.Add(str);
                }
            }
            foreach (var str in Game1.NPCGiftTastes[npc.Name].Split('/')[3].Split(' '))
            {
                if (Game1.objectData.TryGetValue(str, out var data) && data != null && CraftingRecipe.cookingRecipes.ContainsKey(data.Name))
                {
                    likes.Add(str);
                }
            }
            List<string> neutral = new();
            foreach (var str in Game1.NPCGiftTastes["Universal_Neutral"].Split(' '))
            {
                if (str != null && Game1.objectData.ContainsKey(str) && CraftingRecipe.cookingRecipes.ContainsKey(Game1.objectData[str].Name))
                {
                    neutral.Add(str);
                }
            }
            foreach (var str in Game1.NPCGiftTastes[npc.Name].Split('/')[9].Split(' '))
            {
                if (str != null && Game1.objectData.ContainsKey(str) && CraftingRecipe.cookingRecipes.ContainsKey(Game1.objectData[str].Name))
                {
                    neutral.Add(str);
                }
            }


            if (!loves.Any() && !likes.Any() && !neutral.Any())
                return;
            string dish = "240";
            string loved = "like";
            if (loves.Any() && (Game1.random.NextDouble() < Config.LovedDishChance || !likes.Any() && !neutral.Any()))
            {
                loved = "love";
                dish = loves[Game1.random.Next(loves.Count)];
            }
            else if (likes.Any() && (Game1.random.NextDouble() < Config.LovedDishChance || !loves.Any() && !neutral.Any()))
            {
                loved = "like";
                dish = likes[Game1.random.Next(likes.Count)];
            }
            else if (neutral.Any())
            {
                loved = "neutral";
                dish = neutral[Game1.random.Next(neutral.Count)];
            }
            var name = Game1.objectData[dish].Name;
            var displayName = TokenParser.ParseText(Game1.objectData[dish].DisplayName);
            int price = Game1.objectData[dish].Price;
            Monitor.Log($"{npc.Name} is going to order {name}");
            npc.modData[orderKey] = JsonConvert.SerializeObject(new OrderData(dish, name, displayName, price, loved));
            if (Config.AutoFillFridge)
            {
                FillFridge(location, npc);
            }
        }

        internal bool ActivateKitchen(GameLocation location, string[] args, Farmer player, Point point)
        {
            if (!Config.ModEnabled || !Config.RestaurantLocations.Contains(location.Name))
                return false;


            location.ActivateKitchen();
            return true;
        }

        private static Chest GetMiniFridge(GameLocation location)
        {
            foreach (Object value in location.objects.Values)
                if (value != null && value.bigCraftable.Value && value is Chest chest && chest.fridge.Value && chest.modData.ContainsKey(fridgeKey) && chest.modData[fridgeKey] == "true")
                    return chest;

            return null;
        }

        private void FillFridge(GameLocation __instance, NPC npc)
        {
            var fridge = __instance.GetFridge();
            var miniFridge = GetMiniFridge(__instance);


                if (npc.modData.TryGetValue(orderKey, out string dataString))
                {
                    OrderData data = JsonConvert.DeserializeObject<OrderData>(dataString);
                    CraftingRecipe r = new CraftingRecipe(data.dishName, true);
                    if (r is not null)
                    {
                        foreach (var key in r.recipeList.Keys)
                        {
                            if (Game1.objectData.ContainsKey(key))
                            {
                                var obj = new Object(key, r.recipeList[key]);
                                if (fridge != null)
                                {
                                    SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to fridge");
                                    fridge.addItem(obj);
                                }

                                if (miniFridge != null)
                                {
                                    SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to mini-fridge");
                                    miniFridge.addItem(obj);
                                }
                            }
                            else
                            {
                                List<string> list = new List<string>();
                                foreach (var kvp in Game1.objectData)
                                    if (kvp.Value.Category.ToString() == key && !kvp.Value.ContextTags.Contains("fish_legendary"))
                                    {
                                        list.Add(kvp.Key);
                                    }

                                if (list.Any())
                                {
                                    var obj = new Object(list[Game1.random.Next(list.Count)], r.recipeList[key]);
                                    if (fridge != null)
                                    {
                                        SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to fridge");
                                        fridge.addItem(obj);
                                    }

                                    if (miniFridge != null)
                                    {
                                        SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to mini-fridge");
                                        miniFridge.addItem(obj);
                                    }
                                }
                            }
                        }
                    }
                }           
        }


        private void RefreshIngredients(GameLocation __instance)
        {
            var fridge = __instance.GetFridge();
            var miniFridge = GetMiniFridge(__instance);

            if (fridge != null)
                fridge.Items.Clear();

            if (miniFridge != null)
                miniFridge.Items.Clear();

            foreach (var c in __instance.characters)
            if (c.modData.TryGetValue(orderKey, out string dataString))
            {
                OrderData data = JsonConvert.DeserializeObject<OrderData>(dataString);
                CraftingRecipe r = new CraftingRecipe(data.dishName, true);
                if (r is not null)
                {
                    foreach (var key in r.recipeList.Keys)
                    {
                        if (Game1.objectData.ContainsKey(key))
                        {
                            var obj = new Object(key, r.recipeList[key]);
                            if (fridge != null)
                            {
                                SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to fridge");
                                fridge.addItem(obj);
                            }

                            if (miniFridge != null)
                            {
                                SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to mini-fridge");
                                miniFridge.addItem(obj);
                            }
                        }
                        else
                        {
                            List<string> list = new List<string>();
                            foreach (var kvp in Game1.objectData)
                                if (kvp.Value.Category.ToString() == key && !kvp.Value.ContextTags.Contains("fish_legendary"))
                                {
                                    list.Add(kvp.Key);
                                }

                            if (list.Any())
                            {
                                var obj = new Object(list[Game1.random.Next(list.Count)], r.recipeList[key]);
                                if (fridge != null)
                                {
                                    SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to fridge");
                                    fridge.addItem(obj);
                                }

                                if (miniFridge != null)
                                {
                                    SMonitor.Log($"Adding {obj.Name} ({obj.ParentSheetIndex}) x{obj.Stack} to mini-fridge");
                                    miniFridge.addItem(obj);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
