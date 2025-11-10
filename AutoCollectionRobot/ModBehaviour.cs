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
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public Harmony harmony;

        public static ModBehaviour Instance { get; private set; }

        private void OnEnable()
        {
            Debug.Log("Mod AutoCollectionRobot OnEnable!");

            Instance = this;

            harmony = new Harmony("AutoCollectionRobot");
            harmony.PatchAll();

            if (ModConfigAPI.IsAvailable())
            {
                Debug.Log("AutoCollectRobot: ModConfig already available!");
                ModConfigSupport.SetupModConfig();
                ModConfigSupport.LoadConfigFromModConfig();
            }

            Init();
        }

        private void OnDisable()
        {
            Debug.Log("Mod AutoCollectionRobot OnDisable!");

            harmony.UnpatchAll("AutoCollectionRobot");

            ModManager.OnModActivated -= OnModActivated;
            ModConfigAPI.SafeRemoveOnOptionsChangedDelegate(ModConfigSupport.OnModConfigOptionsChanged);

            Instance = null;
        }

        private void OnModActivated(ModInfo info, Duckov.Modding.ModBehaviour behaviour)
        {
            if (info.name == ModConfigAPI.ModConfigName)
            {
                Debug.Log("AutoCollectRobot: ModConfig activated!");
                ModConfigSupport.SetupModConfig();
                ModConfigSupport.LoadConfigFromModConfig();
            }
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
        public AutoCollectionRobotConfig config = new AutoCollectionRobotConfig();

        private float _nextCollectTime = 0f;

        public const int RobotID = 121;
        public bool bIsCollecting = false;

        private InteractableLootbox _robotLootbox;

        private void AutoCollectUpdate()
        {
            if (bIsCollecting && _nextCollectTime + config.collectInterval <= Time.time)
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
                Debug.Log("AutoCollectRobot: CheckRobotLootboxAndTryCreateIfNone: no existing lootbox, creating one now.");
                CreateRobotLootbox(config.robotInventoryNeedInspect, config.robotInventoryCapacity);
                if (_robotLootbox == null)
                {
                    Debug.LogError("AutoCollectRobot: CheckRobotLootboxAndTryCreateIfNone: failed to create lootbox.");
                    CharacterMainControl.Main.PopText("机器人背包创建失败！");
                    return;
                }
            }
        }

        public void OnConfigChanged()
        {
            //Debug.Log($"AutoCollectRobot: Capacity is {config.robotInventoryCapacity}, Inspect is {config.robotInventoryNeedInspect}");
            SetRobotLootboxProps(config.robotInventoryCapacity, config.robotInventoryNeedInspect);
        }

        private void CreateRobotLootbox(bool bNeedInspect = false, int capacity = 512)
        {
            try
            {
                CharacterMainControl player = CharacterMainControl.Main;
                _robotLootbox = UnityEngine.Object.Instantiate(InteractableLootbox.Prefab);
                SetLootboxShowSortButton(_robotLootbox, true);
                SetRobotLootboxProps(capacity, bNeedInspect);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void SetRobotLootboxProps(int capacity, bool bNeedInspect)
        {
            _robotLootbox.needInspect = bNeedInspect;
            Inventory inv = InteractableLootbox.GetOrCreateInventory(_robotLootbox);
            inv.SetCapacity(capacity);
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
                    Debug.LogWarning("AutoCollectRobot: SetLootboxShowSortButton: Cant find showSortButton");
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
                    Debug.LogError("AutoCollectRobot: OpenLootInventory: lootbox inventory is null.");
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

            float overlapRadius = config.collectRadius;
            int num = Physics.OverlapSphereNonAlloc(
                player.transform.position,
                overlapRadius,
                cols
            );

            //绘制搜索范围
            if (config.debugDrawCollectRadius)
            {
                List<Vector3> hitPositions = new List<Vector3>();
                foreach (Collider col in cols)
                {
                    if (col != null)
                    {
                        Vector3 closestPoint;
                        try
                        {
                            // 仅对受支持的碰撞体使用 Collider.ClosestPoint，以避免非凸 MeshCollider 等抛出异常
                            if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
                            {
                                closestPoint = col.ClosestPoint(player.transform.position);
                            }
                            else if (col is MeshCollider meshCol && meshCol.convex)
                            {
                                closestPoint = col.ClosestPoint(player.transform.position);
                            }
                            else
                            {
                                // 回退：对任意类型使用 bounds 的最近点（不会抛异常，作为可视化近似）
                                closestPoint = col.bounds.ClosestPoint(player.transform.position);
                            }
                        }
                        catch (Exception)
                        {
                            // 最后保险回退到 bounds
                            closestPoint = col.bounds.ClosestPoint(player.transform.position);
                        }
                        hitPositions.Add(closestPoint);
                    }
                }
                DebugTools.DrawDetectionSphere(
                    player.transform.position,
                    overlapRadius,
                    .5f,
                    48,
                    Color.cyan,
                    hitPositions,
                    Color.yellow,
                    true
                );
            }

            // 拾取
            for (int i = 0; i < num; i++)
            {
                try
                {
                    Collider collider = cols[i];
                    if (collider == null)
                    {
                        continue;
                    }
                    if (config.collectLootBox && collider.GetComponent<InteractableLootbox>() != null)
                    {
                        InteractableLootbox lootbox = collider.GetComponent<InteractableLootbox>();
                        //Debug.Log($"AutoCollectRobot: SearchAndPickUpItems: Found lootbox {lootbox.name}");
                        if (lootbox.name == "PlayerStorage") return;

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
                                        Debug.Log("AutoCollectRobot: SearchAndPickUpItems: Robot inventory is full, cannot pick up more items.");
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
                    else if (config.collectGroundItems && collider.GetComponent<InteractablePickup>() != null)
                    {
                        InteractablePickup groundItem = collider.GetComponent<InteractablePickup>();
                        if (groundItem && groundItem.ItemAgent && groundItem.ItemAgent.Item)
                        {
                            try
                            {
                                Item item = groundItem.ItemAgent.Item;
                                CheckRobotLootboxAndTryCreateIfNone();
                                PickupItemToLoot(item, _robotLootbox);
                            }
                            catch (Exception e)
                            {
                                var a = e;
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
                        Debug.Log("AutoCollectRobot: Inventory is full, cannot add item.");
                        CharacterMainControl.Main.PopText("机器人背包满了");
                        return false;
                    }
                }
                return true;
            }
            Debug.LogError("AutoCollectRobot: PickupItemToLoot:loot inventory is null");
            return false;
        }

        private void AddRobotToInv()
        {
            Item item = ItemAssetsCollection.InstantiateSync(RobotID);
            ItemUtilities.SendToPlayerCharacterInventory(item, false);
        }

        ////////////////////////////////////////////////////
        /// UI回调,由ItemOperationMenuAddButtonsPatch调用
        ////////////////////////////////////////////////////

        public void StartCollect(Item item)
        {
            if (item == null)
            {
                Debug.LogWarning("AutoCollectRobot: StartCollect: item is null");
                return;
            }

            if (item.TypeID != RobotID)
            {
                Debug.LogWarning("AutoCollectRobot: StartCollect: item is not robot");
                return;
            }

            CharacterMainControl.Main?.PopText("机器人开始收集...");
            bIsCollecting = true;
        }

        public void StopCollect(Item item)
        {
            if (item == null)
            {
                Debug.LogWarning("AutoCollectRobot: StopCollect: item is null");
                return;
            }
            if (item.TypeID != RobotID)
            {
                Debug.LogWarning("AutoCollectRobot: StopCollect: item is not robot");
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
