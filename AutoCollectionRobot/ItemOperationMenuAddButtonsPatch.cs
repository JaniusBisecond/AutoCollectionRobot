using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using Duckov.UI;
using ItemStatsSystem;
using System.Reflection;
using TMPro;
using SodaCraft.Localizations;
using Unity.VisualScripting;

namespace AutoCollectionRobot
{
    [HarmonyPatch(typeof(ItemOperationMenu), "MShow")]
    public class ItemOperationMenuAddButtonsPatch
    {
        // 模板按钮,在 Postfix 第一次执行时初始化
        private static Button templateBtn;

        // 缓存已创建的按钮,偷懒使用i18nkey作为key
        private static readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();

        [HarmonyPostfix]
        public static void Postfix(ItemOperationMenu __instance, Button ___btn_Wishlist, Item ___displayingItem)
        {
            try
            {
                if (templateBtn == null)
                {
                    if (___btn_Wishlist != null)
                    {
                        templateBtn = ___btn_Wishlist;
                    }
                    if (templateBtn == null)
                    {
                        Debug.LogError("ItemOperationMenuAddButtonsPatch: cant get btn_Wishlist template");
                        return;
                    }
                }

                if (___displayingItem == null)
                {
                    Debug.Log("ItemOperationMenuAddButtonsPatch: displayingItem is null");
                    return;
                }

                bool isCollecting = ModBehaviour.Instance != null && ModBehaviour.Instance.bIsCollecting;

                //创建按钮
                bool bShowBtn = ___displayingItem.TypeID == ModBehaviour.RobotID;
                if (bShowBtn)
                {
                    if (isCollecting)
                    {
                        var btnStopPickup = GetOrCreateButton(
                            ___displayingItem,
                            ModBehaviour.i18n_Key_StopCollect,
                            new Color(1.0f, 0.65f, 0.0f),
                            (item) => { ModBehaviour.Instance?.StopCollect(item); },
                            0);
                        if (btnStopPickup == null)
                        {
                            Debug.LogError("btnStopPickup Get or Create failed");
                            return;
                        }
                    }
                    else
                    {
                        var btnPickup = GetOrCreateButton(
                            ___displayingItem,
                            ModBehaviour.i18n_Key_StartCollect,
                            new Color(0.2f, 0.8f, 0.12f),
                            (item) => { ModBehaviour.Instance?.StartCollect(item); },
                            0);
                        if (btnPickup == null)
                        {
                            Debug.LogError("btnPickup Get or Create failed");
                            return;
                        }
                    }

                    var btnOpenRobotInv = GetOrCreateButton(___displayingItem, 
                        ModBehaviour.i18n_Key_OpenInv, 
                        new Color(0.2f, 0.6f, 0.9f),
                        (item) => { ModBehaviour.Instance?.OpenRobotInventory();},
                        1);
                    if (btnOpenRobotInv == null)
                    {
                        Debug.LogError("btnOpenRobotInv Get or Create failed");
                        return;
                    }
                }
                
                //控制按钮可见
                foreach (var kv in _buttons)
                {
                    try
                    {
                        var btn = kv.Value;
                        if (btn == null) continue; // 按钮可能被销毁或为 null（Unity 特殊 null）
                        if (kv.Key == ModBehaviour.i18n_Key_StartCollect)
                        {
                            btn.gameObject.SetActive(bShowBtn && !isCollecting);
                        }
                        else if (kv.Key == ModBehaviour.i18n_Key_StopCollect)
                        {
                            btn.gameObject.SetActive(bShowBtn && isCollecting);
                        }
                        else
                        {
                            btn.gameObject.SetActive(bShowBtn);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static Button GetOrCreateButton(Item item, string i18key, Color buttonColor,Action<Item> onClick, int siblingIndex = -1)
        {
            if (templateBtn == null)
            {
                Debug.LogWarning("ItemOperationMenuAddButtonsPatch.AddNewButton: templateBtn not initialized");
                return null;
            }

            string key = i18key;
            if (_buttons.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            var btn = GameObject.Instantiate(templateBtn, templateBtn.transform.parent);
            if (btn == null)
            {
                UnityEngine.Object.Destroy(btn);
                return null;
            }

            btn.name = $"AutoBtn_{item.TypeID}_{i18key}";

            UpdateButtonVisual(btn, i18key, buttonColor);
            if (siblingIndex == -1)
            {
                btn.transform.SetAsLastSibling();
            }
            else
            {
                btn.transform.SetSiblingIndex(siblingIndex);
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                try
                {
                    var menu = btn.GetComponentInParent<ItemOperationMenu>();
                    onClick?.Invoke(item);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });

            _buttons[key] = btn;
            return btn;
        }

        private static void UpdateButtonVisual(Button btn, string i18nKey, Color color)
        {
            var textComp = btn.GetComponentInChildren<TextLocalizor>();
            if (textComp != null)
            {
                textComp.Key = i18nKey;
            }

            try
            {
                var img = btn.transform.Find("BG").GetComponent<Image>();
                if (img != null) img.color = color;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}