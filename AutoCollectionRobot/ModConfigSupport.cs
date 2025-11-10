using Duckov.Utilities;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace AutoCollectionRobot
{
    public class ModConfigSupport
    {
        public static string MOD_NAME = "AutoCollectionRobotMod";

        private static string persistentConfigPath => Path.Combine(Application.streamingAssetsPath, "AutoCollectionRobotConfig.json");

        public static void SetupModConfig()
        {
            if (!ModConfigAPI.IsAvailable())
            {
                Debug.LogWarning("AutoCollectRobot: ModConfig not available");
                return;
            }

            // 添加配置变更监听
            ModConfigAPI.SafeAddOnOptionsChangedDelegate(OnModConfigOptionsChanged);

            // 根据当前语言设置描述文字
            SystemLanguage[] chineseLanguages = {
                SystemLanguage.Chinese,
                SystemLanguage.ChineseSimplified,
                SystemLanguage.ChineseTraditional
            };

            bool isChinese = chineseLanguages.Contains(LocalizationManager.CurrentLanguage);

            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "debugDrawCollectRadius",
                isChinese ? "调试显示收集范围" : "Debug Draw Collect Radius",
                ModBehaviour.Instance.config.debugDrawCollectRadius
            );

            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "collectInterval",
                isChinese ? "收集间隔(秒)" : "Collect Interval (seconds)",
                typeof(float),
                ModBehaviour.Instance.config.collectInterval,
                new Vector2(0.5f, 10f)
            );

            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "collectRadius",
                isChinese ? "收集范围" : "Collect Radius",
                typeof(float),
                ModBehaviour.Instance.config.collectRadius,
                new Vector2(1f, 50f)
            );

            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "robotInventoryCapacity",
                isChinese ? "机器人背包容量" : "Robot Inventory Capacity",
                typeof(int),
                ModBehaviour.Instance.config.robotInventoryCapacity,
                new Vector2(10, 2048)
            );

            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "collectGroundItems",
                isChinese ? "收集地面物品" : "Collect Ground Items",
                ModBehaviour.Instance.config.collectGroundItems
            );

            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "collectLootBox",
                isChinese ? "收集容器物品" : "Collect Loot Box Items",
                ModBehaviour.Instance.config.collectLootBox
            );

            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "robotInventoryNeedInspect",
                isChinese ? "机器人背包搜索动画" : "Robot Inventory Needs Inspect Animation",
                ModBehaviour.Instance.config.robotInventoryNeedInspect
            );

            Debug.Log("AutoCollectRobot: ModConfig setup completed");
        }

        public static void OnModConfigOptionsChanged(string key)
        {
            if (!key.StartsWith(MOD_NAME + "_"))
                return;

            // 使用新的 LoadConfig 方法读取配置
            LoadConfigFromModConfig();

            // 保存到本地配置文件
            SaveConfig(ModBehaviour.Instance.config);

            ModBehaviour.Instance.OnConfigChanged();

            Debug.Log($"AutoCollectRobot: ModConfig updated - {key}");
        }

        public static void LoadConfigFromModConfig()
        {
            ModBehaviour.Instance.config.collectInterval = ModConfigAPI.SafeLoad<float>(MOD_NAME, "collectInterval", ModBehaviour.Instance.config.collectInterval);
            ModBehaviour.Instance.config.robotInventoryNeedInspect = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "robotInventoryNeedInspect", ModBehaviour.Instance.config.robotInventoryNeedInspect);
            ModBehaviour.Instance.config.robotInventoryCapacity = ModConfigAPI.SafeLoad<int>(MOD_NAME, "robotInventoryCapacity", ModBehaviour.Instance.config.robotInventoryCapacity);
            ModBehaviour.Instance.config.collectGroundItems = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "collectGroundItems", ModBehaviour.Instance.config.collectGroundItems);
            ModBehaviour.Instance.config.collectLootBox = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "collectLootBox", ModBehaviour.Instance.config.collectLootBox);
            ModBehaviour.Instance.config.collectRadius = ModConfigAPI.SafeLoad<float>(MOD_NAME, "collectRadius", ModBehaviour.Instance.config.collectRadius);
            ModBehaviour.Instance.config.debugDrawCollectRadius = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "debugDrawCollectRadius", ModBehaviour.Instance.config.debugDrawCollectRadius);
        }

        private static void SaveConfig(AutoCollectionRobotConfig config)
        {
            try
            {
                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(persistentConfigPath, json);
                Debug.Log("AutoCollectRobot: Config saved");
            }
            catch (Exception e)
            {
                Debug.LogError($"AutoCollectRobot: Failed to save config: {e}");
            }
        }
    }

}
