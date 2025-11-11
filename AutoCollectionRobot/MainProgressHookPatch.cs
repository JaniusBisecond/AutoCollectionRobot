using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using Duckov.Economy.UI;

namespace AutoCollectionRobot
{
    [HarmonyPatch(typeof(LevelManager), "NotifySaveBeforeLoadScene")]
    public static class LevelManagerNotifySaveBeforeLoadScenePatch
    {
        public static void Prefix(bool saveToFile)
        {
            try
            {
                if (ModBehaviour.Instance != null)
                {
                    ModBehaviour.Instance.OnBeforeLevelChange();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // 用于拦截 StockShopView.OnInteractionButtonClicked ，打印 Target 信息以便确认是哪一个商人
    [HarmonyPatch(typeof(StockShopView), "OnInteractionButtonClicked")]
    public static class StockShopView_OnInteractionButtonClicked_Patch
    {
        // 使用 Prefix 在方法执行前读取并打印信息
        public static void Prefix(object __instance)
        {
            try
            {
                if (__instance == null)
                {
                    Debug.LogWarning("AutoCollectRobot: StockShopView patch: __instance is null");
                    return;
                }

                // 尝试把实例强转为 StockShopView（如果编译环境中存在此类型）
                var view = __instance as StockShopView;
                if (view == null)
                {
                    Debug.LogWarning("AutoCollectRobot: StockShopView patch: __instance is not StockShopView");
                    return;
                }

                // 通过 public 属性获取 Target（StockShop）
                var target = view.Target;
                if (target == null)
                {
                    Debug.Log("AutoCollectRobot: StockShopView.OnInteractionButtonClicked: Target is null");
                    return;
                }

                Type targetType = target.GetType();

                // 尝试读取常见信息：MerchantID、DisplayName、entries 数量
                string merchantId = TryGetStringProperty(target, targetType, "MerchantID");
                string displayName = TryGetStringProperty(target, targetType, "DisplayName");

                int entriesCount = -1;
                try
                {
                    // entries 字段在 StockShop 中是 public List<Entry> entries
                    FieldInfo fi = targetType.GetField("entries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fi != null)
                    {
                        var val = fi.GetValue(target) as System.Collections.ICollection;
                        if (val != null) entriesCount = val.Count;
                    }
                }
                catch { }

                Debug.Log($"AutoCollectRobot: StockShopView.OnInteractionButtonClicked -> Target type={targetType.FullName}, MerchantID='{merchantId}', DisplayName='{displayName}', entriesCount={entriesCount}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static string TryGetStringProperty(object obj, Type type, string propName)
        {
            try
            {
                PropertyInfo pi = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null)
                {
                    var v = pi.GetValue(obj);
                    return v?.ToString() ?? "(null)";
                }

                FieldInfo fi = type.GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                {
                    var v = fi.GetValue(obj);
                    return v?.ToString() ?? "(null)";
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return "(not found)";
        }
    }
}
