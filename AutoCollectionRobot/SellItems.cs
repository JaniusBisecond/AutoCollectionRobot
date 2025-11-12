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
using System.Reflection;
using UnityEngine;

namespace AutoCollectionRobot
{
    public class SellItems
    {

        private const string merchantID_Fo = "Merchant_Fo";                 //佛哥
        private const string merchantID_Ming = "Merchant_Ming";             //小明
        private const string merchantID_Weapon = "Merchant_Weapon";         //老吴
        private const string merchantID_Equipment = "Merchant_Equipment";   //橘子
        private const string merchantID_Mud = "Merchant_Mud";               //泥巴
        private const string merchantID_Normal = "Merchant_Normal";         //售货机

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

                if (ModBehaviour.Instance._robotLootbox == null || ModBehaviour.Instance._robotLootbox == null)
                {
                    Debug.LogWarning("AutoCollectRobot: SellRobotItemsToShop: robot lootbox or inventory is null.");
                    return;
                }

                // 收集要出售的物品快照，避免在循环中修改集合
                List<Item> toSell = new List<Item>();
                try
                {
                    foreach (Item it in ModBehaviour.Instance._robotLootbox.Inventory)
                    {
                        if (it == null) continue;
                        try
                        {
                            if (!it.CanBeSold) continue;
                            if (ItemWishlist.GetWishlistInfo(it.TypeID).isManuallyWishlisted) continue; //跳过愿望单物品
                            if (it.TypeID == 451) continue; //跳过现金
                        }
                        catch
                        {
                            continue;
                        }
                        toSell.Add(it);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                if (toSell.Count == 0)
                {
                    Debug.Log("AutoCollectRobot: SellRobotItemsToShop: no eligible items to sell.");
                    return;
                }

                MethodInfo sellFunc = typeof(StockShop).GetMethod("Sell", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sellFunc == null)
                {
                    Debug.LogError("AutoCollectRobot: SellRobotItemsToShop: can't find StockShop.Sell method via reflection.");
                    return;
                }

                Debug.Log($"AutoCollectRobot: Selling {toSell.Count} items to shop '{shop.MerchantID}'");

                foreach (Item item in toSell)
                {
                    if (item == null) continue;
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
        private static StockShop TryFindShopByMerchanID(string merchantId)
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
                    Debug.LogError("AutoCollectRobot: TryFindShopByMerchanID: No StockShop instances found in scene.");
                    return null;
                }
                foreach (var s in shops)
                {
                    try
                    {
                        if (string.Equals(s.MerchantID, merchantId, StringComparison.OrdinalIgnoreCase))
                        {
                            return s;
                        }
                    }
                    catch { }
                }
                Debug.Log($"AutoCollectRobot: Cant find StockShop with MerchantID='{merchantId}', Try find merchant_Normal");
                foreach (var s in shops)
                {
                    try
                    {
                        if (string.Equals(s.MerchantID, merchantID_Normal, StringComparison.OrdinalIgnoreCase))
                        {
                            return s;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return null;
        }

    }
}
