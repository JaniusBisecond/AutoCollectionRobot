using Cysharp.Threading.Tasks;
using Duckov;
using Duckov.Economy;
using Duckov.Modding;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Data;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AutoCollectionRobot
{
    public class SellItems
    {

        public static readonly string merchantID_Fo = "Merchant_Fo";                 //佛哥
        public static readonly string merchantID_Ming = "Merchant_Ming";             //小明
        public static readonly string merchantID_Weapon = "Merchant_Weapon";         //老吴
        public static readonly string merchantID_Equipment = "Merchant_Equipment";   //橘子
        public static readonly string merchantID_Mud = "Merchant_Mud";               //泥巴
        public static readonly string merchantID_Normal = "Merchant_Normal";         //售货机

        public static void SellRobotItemsToShop()
        {
            try
            {
                StockShop shop = TryFindShopByMerchanID(merchantID_Fo);
                if (shop == null)
                {
                    Debug.LogWarning($"AutoCollectRobot: SellRobotItemsToShop: cant found any shop.");
                    return;
                }

                MethodInfo sellFunc = typeof(StockShop).GetMethod("Sell", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sellFunc == null)
                {
                    Debug.LogError("AutoCollectRobot: SellRobotItemsToShop: can't find StockShop.Sell method via reflection.");
                    return;
                }

                if (ModBehaviour.Instance._robotLootbox == null || ModBehaviour.Instance._robotLootbox.Inventory == null)
                {
                    Debug.LogWarning("AutoCollectRobot: SellRobotItemsToShop: robot lootbox or inventory is null.");
                    return;
                }

                // 收集要出售的物品快照，避免在循环中修改集合
                List<Item> toSell = new List<Item>();
                foreach (Item it in ModBehaviour.Instance._robotLootbox.Inventory)
                {
                    try
                    {
                        if (it == null) continue;
                        if (!it.CanBeSold) continue;
                        if (it.TypeID == 451) continue; //跳过现金

                        // 跳过愿望单物品
                        var wishInfo = ItemWishlist.GetWishlistInfo(it.TypeID);
                        if (wishInfo.isManuallyWishlisted || wishInfo.isBuildingRequired || wishInfo.isQuestRequired) continue;

                        toSell.Add(it);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                if (toSell.Count == 0)
                {
                    Debug.Log("AutoCollectRobot: SellRobotItemsToShop: no eligible items to sell.");
                    return;
                }

                Debug.Log($"AutoCollectRobot: Selling {toSell.Count} items to shop '{shop.MerchantID}'");

                foreach (Item item in toSell)
                {
                    try
                    {
                        object task = sellFunc.Invoke(shop, new object[] { item });
                        if (task == null)
                        {
                            Debug.LogWarning($"AutoCollectRobot: Sell invocation returned null for item {item.DisplayName} (type {item.TypeID}).");
                            continue;
                        }
                        if (task is UniTask ut)
                        {
                            ut.Forget();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                AudioManager.Post("UI/sell");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }


        //尝试找到给定 MerchantID 的 StockShop 实例
        //若给定 MerchantID 不存在，优先返回普通售货机
        public static StockShop TryFindShopByMerchanID(string merchantId)
        {
            try
            {
                if (string.IsNullOrEmpty(merchantId))
                {
                    return null;
                }

                var shops = UnityEngine.Object.FindObjectsOfType<StockShop>();
                if (shops == null || shops.Length == 0)
                {
                    Debug.Log("AutoCollectRobot: TryFindShopByMerchanID: No StockShop instances found in scene.");
                    return null;
                }

                var merchant = shops.FirstOrDefault(s => s.MerchantID.Equals(merchantId, StringComparison.OrdinalIgnoreCase));
                if (merchant != null)
                {
                    return merchant;
                }

                Debug.Log($"AutoCollectRobot: Cant find StockShop with MerchantID='{merchantId}', Try find merchant_Normal");

                merchant = shops.FirstOrDefault(s => s.MerchantID.Equals(merchantID_Normal, StringComparison.OrdinalIgnoreCase));
                if (merchant != null)
                {
                    return merchant;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return null;
        }

    }
}
