using Duckov.ItemUsage;
using Duckov.Modding;
using Duckov.UI;
using Duckov.Utilities;
using HarmonyLib;
using ItemStatsSystem;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static UnityEngine.UI.Image;

namespace AutoCollectionRobot
{
    [System.Serializable]
    public class AutoCollectionRobotConfig
    {
        /// <summary>
        /// -检测间隔
        /// -机器人背包搜索动画
        /// -机器人背包容量
        /// -地面物品收集开关
        /// -容器物品收集开关
        /// -收集范围
        /// -收集范围调试开关
        /// </summary>
        public float collectInterval = 2f;

        public bool robotInventoryNeedInspect = false;

        public int robotInventoryCapacity = 512;

        public bool collectGroundItems = false;

        public bool collectLootBox = true;

        public float collectRadius = 10f;

        public bool debugDrawCollectRadius = false;

        // 强制更新配置文件token
        public string configToken = "auto_collection_robot_v1";
    }

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public Harmony harmony;

        public static ModBehaviour Instance { get; private set; }

        private void OnDestroy()
        {
            if (_text != null)
            {
                Destroy(_text);
            }
        }

        private void OnEnable()
        {
            Debug.Log("Mod AutoCollectionRobot OnEnable!");

            Instance = this;

            harmony = new Harmony("AutoCollectionRobot");
            harmony.PatchAll();

            if (ModConfigAPI.IsAvailable())
            {
                Debug.Log("AutoCollectRobot: ModConfig already available!");
                SetupModConfig();
                LoadConfigFromModConfig();
            }

            Init();
        }

        private void OnModActivated(ModInfo info, Duckov.Modding.ModBehaviour behaviour)
        {
            if (info.name == ModConfigAPI.ModConfigName)
            {
                Debug.Log("AutoCollectRobot: ModConfig activated!");
                SetupModConfig();
                LoadConfigFromModConfig();
            }
        }


        private void OnDisable()
        {
            Debug.Log("Mod AutoCollectionRobot OnDisable!");

            harmony.UnpatchAll("AutoCollectionRobot");

            ModManager.OnModActivated -= OnModActivated;
            ModConfigAPI.SafeRemoveOnOptionsChangedDelegate(OnModConfigOptionsChanged);

            Instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                Debug.Log("Q Pressed!");
                CharacterMainControl.Main.PopText("QQQQ!");
                AddRobotToInv();
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                Debug.Log("X Pressed!");
                CharacterMainControl.Main.PopText("XXXX!");
            }

            AutoCollectUpdate();
        }

        private void Init()
        {
            i18nDataInit();
        }

        ////////////////////////////////////////////
        /// ModConfig
        ////////////////////////////////////////////
        public static string MOD_NAME = "AutoCollectionRobotMod";

        AutoCollectionRobotConfig config = new AutoCollectionRobotConfig();

        private static string persistentConfigPath => Path.Combine(Application.streamingAssetsPath, "AutoCollectionRobotConfig.json");

        TextMeshProUGUI _text = null;
        TextMeshProUGUI Text
        {
            get
            {
                if (_text == null)
                {
                    _text = Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
                    _text.gameObject.SetActive(false);
                }
                return _text;
            }
        }

        private void SetupModConfig()
        {
            if (!ModConfigAPI.IsAvailable())
            {
                Debug.LogWarning("AutoCollectRobot: ModConfig not available");
                return;
            }

            Debug.Log("准备添加ModConfig配置项");

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
                config.debugDrawCollectRadius
            );

            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "collectInterval",
                isChinese ? "收集间隔(秒)" : "Collect Interval (seconds)",
                typeof(float),
                config.collectInterval,
                new Vector2(0.5f, 10f)
            );

            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "collectRadius",
                isChinese ? "收集范围" : "Collect Radius",
                typeof(float),
                config.collectRadius,
                new Vector2(1f, 50f)
            );

            ModConfigAPI.SafeAddInputWithSlider(
                MOD_NAME,
                "robotInventoryCapacity",
                isChinese ? "机器人背包容量" : "Robot Inventory Capacity",
                typeof(int),
                config.robotInventoryCapacity,
                new Vector2(10, 2048)
            );

            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "collectGroundItems",
                isChinese ? "收集地面物品" : "Collect Ground Items",
                config.collectGroundItems
            );

            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "collectLootBox",
                isChinese ? "收集容器物品" : "Collect Loot Box Items",
                config.collectLootBox
            );

            ModConfigAPI.SafeAddBoolDropdownList(
                MOD_NAME,
                "robotInventoryNeedInspect",
                isChinese ? "机器人背包搜索动画" : "Robot Inventory Needs Inspect Animation",
                config.robotInventoryNeedInspect
            );

            Debug.Log("AutoCollectRobot: ModConfig setup completed");
        }

        private void OnModConfigOptionsChanged(string key)
        {
            if (!key.StartsWith(MOD_NAME + "_"))
                return;

            // 使用新的 LoadConfig 方法读取配置
            LoadConfigFromModConfig();

            // 保存到本地配置文件
            SaveConfig(config);

            //// 更新当前显示的文本样式（如果正在显示）
            //UpdateTextStyle();

            Debug.Log($"AutoCollectRobot: ModConfig updated - {key}");
        }

        private void LoadConfigFromModConfig()
        {
            config.collectInterval = ModConfigAPI.SafeLoad<float>(MOD_NAME, "collectInterval", config.collectInterval);
            config.robotInventoryNeedInspect = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "robotInventoryNeedInspect", config.robotInventoryNeedInspect);
            config.robotInventoryCapacity = ModConfigAPI.SafeLoad<int>(MOD_NAME, "robotInventoryCapacity", config.robotInventoryCapacity);
            config.collectGroundItems = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "collectGroundItems", config.collectGroundItems);
            config.collectLootBox = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "collectLootBox", config.collectLootBox);
            config.collectRadius = ModConfigAPI.SafeLoad<float>(MOD_NAME, "collectRadius", config.collectRadius);
            config.debugDrawCollectRadius = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "debugDrawCollectRadius", config.debugDrawCollectRadius);
        }

        private void SaveConfig(AutoCollectionRobotConfig config)
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

        //private void UpdateTextStyle()
        //{
        //    if (_text != null)
        //    {
        //        _text.fontSize = config.fontSize;

        //        // 解析颜色
        //        if (ColorUtility.TryParseHtmlString(config.textColor, out Color color))
        //        {
        //            _text.color = color;
        //        }
        //        else
        //        {
        //            _text.color = Color.white; // 默认颜色
        //        }
        //    }
        //}
        ////////////////////////////////////////////
        /// 本地化
        ////////////////////////////////////////////

        public const string i18n_Key_StartCollect = "StartCollect";
        public const string i18n_Key_StopCollect = "StopCollect";
        public const string i18n_Key_OpenInv = "OpenInv";

        private void i18nDataInit()
        {

            if (LocalizationManager.CurrentLanguage == SystemLanguage.ChineseSimplified ||
                LocalizationManager.CurrentLanguage == SystemLanguage.ChineseTraditional)
            {
                LocalizationManager.SetOverrideText(i18n_Key_StartCollect, "开始收集");
                LocalizationManager.SetOverrideText(i18n_Key_StopCollect, "停止收集");
                LocalizationManager.SetOverrideText(i18n_Key_OpenInv, "打开背包");
            }
            else
            {
                LocalizationManager.SetOverrideText(i18n_Key_StartCollect, "Start Collecting");
                LocalizationManager.SetOverrideText(i18n_Key_StopCollect, "Stop Collecting");
                LocalizationManager.SetOverrideText(i18n_Key_OpenInv, "Open Inventory");
            }
        }

        ////////////////////////////////////////////
        /// 机器人背包,拾取
        ////////////////////////////////////////////

        public float collectInterval = 2f;
        private float _nextCollectTime = 0f;

        public const int RobotID = 121;
        public bool bIsCollecting = false;

        private InteractableLootbox _robotLootbox;

        private void AutoCollectUpdate()
        {
            if (bIsCollecting && _nextCollectTime + collectInterval <= Time.time )
            {
                try
                {
                    SearchAndPickUpItems();
                }
                finally
                {
                    _nextCollectTime = Time.time;
                }
            }
        }

        private void CheckRobotLootboxAndTryCreateIfNone()
        {
            if (_robotLootbox == null)
            {
                Debug.Log("CheckRobotLootboxAndTryCreateIfNone: no existing lootbox, creating one now.");
                CreateRobotLootbox();
                if (_robotLootbox == null)
                {
                    Debug.LogError("CheckRobotLootboxAndTryCreateIfNone: failed to create lootbox.");
                    CharacterMainControl.Main.PopText("机器人背包创建失败！");
                    return;
                }
            }
        }

        private void CreateRobotLootbox(bool bNeedInspect = false, int capacity = 512)
        {
            try
            {
                CharacterMainControl player = CharacterMainControl.Main;
                _robotLootbox = UnityEngine.Object.Instantiate(InteractableLootbox.Prefab);
                SetLootboxShowSortButton(_robotLootbox, true);
                _robotLootbox.needInspect = bNeedInspect;
                Inventory inv = InteractableLootbox.GetOrCreateInventory(_robotLootbox);
                inv.SetCapacity(capacity);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void SetLootboxShowSortButton(InteractableLootbox lootbox, bool value)
        {
            if (lootbox == null) return;
            try
            {
                FieldInfo fi = typeof(InteractableLootbox).GetField("showSortButton", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    fi.SetValue(lootbox, value);
                }
                else
                {
                    Debug.LogWarning("SetLootboxShowSortButton: Cant find showSortButton");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OpenLootInventory()
        {
            try
            {
                CheckRobotLootboxAndTryCreateIfNone();

                if (_robotLootbox.Inventory == null)
                {
                    Debug.LogError("OpenLootInventory: lootbox inventory is null.");
                    return;
                }
                CharacterMainControl.Main.Interact(_robotLootbox);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void SearchAndPickUpItems()
        {
            CharacterMainControl player = CharacterMainControl.Main;
            if (player == null)
            {
                return;
            }

            Collider[] cols = new Collider[4096];

            float overlapRadius = 10f;
            int num = Physics.OverlapSphereNonAlloc(
                player.transform.position,
                overlapRadius,
                cols
            );

            //查看搜索范围
            //List<Vector3> hitPositions = new List<Vector3>();
            //foreach (Collider col in cols)
            //{
            //    if (col != null)
            //    {
            //        hitPositions.Add(col.ClosestPoint(player.transform.position));
            //    }
            //}
            //DebugTools.DrawDetectionSphere(
            //    player.transform.position,
            //    overlapRadius,
            //    2f,
            //    48,
            //    Color.cyan,
            //    hitPositions,
            //    Color.yellow,
            //    true
            //);

            for (int i = 0; i < num; i++)
            {
                try
                {
                    Collider collider = cols[i];
                    if (collider == null) continue;

                    InteractableLootbox lootbox = collider.GetComponent<InteractableLootbox>();
                    if (lootbox != null && lootbox.Inventory != null && lootbox.Inventory.Content != null &&
                        lootbox.Inventory.Content.Count > 0)
                    {
                        List<Item> itemList = new List<Item>();
                        foreach (Item item in lootbox.Inventory.Content)
                        {
                            try
                            {
                                if (item != null)
                                {
                                    itemList.Add(item);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }

                        foreach (Item item in itemList)
                        {
                            try
                            {
                                if (player.CharacterItem.Inventory.GetFirstEmptyPosition() < 0)
                                {
                                    Debug.Log("背包已满!");
                                    CharacterMainControl.Main.PopText("背包已满!");
                                    return;
                                }
                                CheckRobotLootboxAndTryCreateIfNone();
                                PickupItemToLoot(item, _robotLootbox);
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private bool PickupItemToLoot(Item item, InteractableLootbox lootbox)
        {
            if (item == null)
            {
                return false;
            }

            Inventory inventory = lootbox.Inventory;
            if (inventory != null)
            {
                item.AgentUtilities.ReleaseActiveAgent();
                item.Detach();
                if (!inventory.AddAndMerge(item))
                {
                    if (!inventory.AddItem(item))
                    {
                        Debug.Log("Inventory is full, cannot add item.");
                        CharacterMainControl.Main.PopText("机器人背包满了");
                        return false;
                    }
                }
            }
            Debug.LogError("loot inventory is null");
            return false;
        }

        private void AddRobotToInv()
        {
            Item item = ItemAssetsCollection.InstantiateSync(RobotID);
            ItemUtilities.SendToPlayerCharacterInventory(item, false);
            Debug.Log($"useDurability: {item.UsageUtilities.useDurability}");
            Debug.Log($"item.UseDurability: {item.UseDurability}");
            Debug.Log("Item.Durability: " + item.Durability + "item.UsageUtilities.durabilityUsage" + item.UsageUtilities.durabilityUsage);
        }

        ////////////////////////////////////////////////////
        /// UI回调,由ItemOperationMenuAddButtonsPatch调用
        ////////////////////////////////////////////////////

        public void StartCollect(Item item)
        {
            if (item == null)
            {
                Debug.LogWarning("StartCollect: item is null");
                return;
            }

            if (item.TypeID != RobotID)
            {
                Debug.LogWarning("StartCollect: item is not robot");
                return;
            }

            CharacterMainControl.Main?.PopText("机器人开始收集...");
            bIsCollecting = true;
        }

        public void StopCollect(Item item)
        {
            if (item == null)
            {
                Debug.LogWarning("StopCollect: item is null");
                return;
            }
            if (item.TypeID != RobotID)
            {
                Debug.LogWarning("StopCollect: item is not robot");
                return;
            }
            CharacterMainControl.Main?.PopText("机器人停止收集.");
            bIsCollecting = false;
        }

        public void OpenRobotInventory()
        {
            OpenLootInventory();
        }
    }
}
