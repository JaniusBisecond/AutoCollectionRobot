using Cysharp.Threading.Tasks;
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

            ModManager.OnModActivated += OnModActivated;
            if (ModConfigAPI.IsAvailable())
            {
                Debug.Log("AutoCollectRobot: ModConfig already available!");
                ModConfigSupport.SetupModConfig();
                ModConfigSupport.LoadConfigFromModConfig();
            }
            else
            {
                Debug.Log("AutoCollectRobot: ModConfig not available, loading local config.");
                try
                {
                    config = LocalConfig.Load();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            LevelManager.OnAfterLevelInitialized += OnAfterLevelChanged;

            Init();
        }

        private void OnDisable()
        {
            Debug.Log("Mod AutoCollectionRobot OnDisable!");

            LevelManager.OnAfterLevelInitialized -= OnAfterLevelChanged;

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
            //if (Input.GetKeyDown(KeyCode.Z))
            //{
            //    Item item = ItemAssetsCollection.InstantiateSync((int)RobotID);
            //    ItemUtilities.SendToPlayerCharacterInventory(item, false);
            //}

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
        public const string i18n_Key_SellAllItem = "SellAllItem";

        public const string i18n_Key_RobotBagFull = "RobotBagFull";
        public const string i18n_Key_RobotBagCreateFaild = "RobotBagCreateFaild";
        public const string i18n_Key_RobotStartInspect = "RobotStartInspect";
        public const string i18n_Key_RobotStopInspect = "RobotStopInspect";

        private void i18nDataInit()
        {

            if (LocalizationManager.CurrentLanguage == SystemLanguage.ChineseSimplified ||
                LocalizationManager.CurrentLanguage == SystemLanguage.ChineseTraditional)
            {
                LocalizationManager.SetOverrideText(i18n_Key_StartCollect, "开始收集");
                LocalizationManager.SetOverrideText(i18n_Key_StopCollect, "停止收集");
                LocalizationManager.SetOverrideText(i18n_Key_OpenInv, "打开背包");
                LocalizationManager.SetOverrideText(i18n_Key_SellAllItem, "一键出售");
                
                LocalizationManager.SetOverrideText(i18n_Key_RobotBagFull, "机器人背包满了");
                LocalizationManager.SetOverrideText(i18n_Key_RobotBagCreateFaild, "机器人背包创建失败");
                LocalizationManager.SetOverrideText(i18n_Key_RobotStartInspect, "机器人开始收集...");
                LocalizationManager.SetOverrideText(i18n_Key_RobotStopInspect, "机器人停止收集!");
            }
            else
            {
                LocalizationManager.SetOverrideText(i18n_Key_StartCollect, "Start Collecting");
                LocalizationManager.SetOverrideText(i18n_Key_StopCollect, "Stop Collecting");
                LocalizationManager.SetOverrideText(i18n_Key_OpenInv, "Open Inventory");
                LocalizationManager.SetOverrideText(i18n_Key_SellAllItem, "Sell All Items");

                LocalizationManager.SetOverrideText(i18n_Key_RobotBagFull, "Robot Inventory Full");
                LocalizationManager.SetOverrideText(i18n_Key_RobotBagCreateFaild, "Robot Inventory Creation Failed");
                LocalizationManager.SetOverrideText(i18n_Key_RobotStartInspect, "Robot Started Collecting...");
                LocalizationManager.SetOverrideText(i18n_Key_RobotStopInspect, "Robot Stopped Collecting!");
            }
        }

        ////////////////////////////////////////////
        /// 机器人背包,拾取
        ////////////////////////////////////////////

        public AutoCollectionRobotConfig config = new AutoCollectionRobotConfig();

        private float _nextCollectTime = 0f;

        public const int RobotID = 121;
        public bool bIsCollecting = false;

        internal InteractableLootbox _robotLootbox;
        private InventoryData _invSnapshot;

        private UniTask _inventoryLoadTask = UniTask.CompletedTask;

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

        public void OnBeforeLevelChange()
        {
            bIsCollecting = false;
            if (config.saveRobotInv && _robotLootbox != null)
            {
                Debug.Log("Level will change! Save Inv snapshot");
                _invSnapshot = InventoryData.FromInventory(_robotLootbox.Inventory);
            }
        }

        public void OnAfterLevelChanged()
        {
            CheckRobotLootboxAndTryCreateIfNone();
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
                    CharacterMainControl.Main.PopText(LocalizationManager.GetPlainText(i18n_Key_RobotBagCreateFaild));
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
                _robotLootbox = UnityEngine.Object.Instantiate(InteractableLootbox.Prefab);
                SetLootboxShowSortButton(_robotLootbox, true);
                SetRobotLootboxProps(capacity, bNeedInspect);

                if (config.saveRobotInv && _invSnapshot != null)
                {
                    try
                    {   
                        // 加载完 _invSnapshot 会清楚，解决子场景 Bug
                        Debug.Log("Load saved robot inv from snapshot.");
                        _inventoryLoadTask = LoadSnapshotIntoInventoryAsync(_invSnapshot, _robotLootbox.Inventory);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        _inventoryLoadTask = UniTask.CompletedTask;
                    }
                }
                else
                {
                    _inventoryLoadTask = UniTask.CompletedTask;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _inventoryLoadTask = UniTask.CompletedTask;
            }
        }

        private async UniTask LoadSnapshotIntoInventoryAsync(InventoryData snapshot, Inventory inv)
        {
            if (snapshot == null || inv == null)
            {
                // 确保字段被清空以避免重用（即使传入 snapshot 为 null，也安全）
                _invSnapshot = null;
                return;
            }

            try
            {
                await InventoryData.LoadIntoInventory(snapshot, inv);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                // 无论成功或失败，清除快照，确保它只被使用一次
                _invSnapshot = null;
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

        private async UniTask OpenLootInventory()
        {
            try
            {
                CheckRobotLootboxAndTryCreateIfNone();

                try
                {
                    await _inventoryLoadTask;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

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
                        if (IsMainCharacterDeadLoot(lootbox)) continue;

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
                                        CharacterMainControl.Main.PopText(LocalizationManager.GetPlainText(i18n_Key_RobotBagFull));
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

        private bool IsMainCharacterDeadLoot(InteractableLootbox lootbox)
        {
            if (lootbox == null)
            {
                return false;
            }

            try
            {
                string displayNameKey = null;
                FieldInfo fi = typeof(InteractableLootbox).GetField("displayNameKey", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    try
                    {
                        object val = fi.GetValue(lootbox);
                        displayNameKey = val as string;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                if (displayNameKey == "UI_Interact_Tomb")
                {
                    Debug.Log("AutoCollectRobot: IsMainCharacterDeadLoot: Found main character tomb lootbox.");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return false;
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
                        CharacterMainControl.Main.PopText(LocalizationManager.GetPlainText(i18n_Key_RobotBagFull));
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

            CharacterMainControl.Main?.PopText(LocalizationManager.GetPlainText(i18n_Key_RobotStartInspect));
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
            CharacterMainControl.Main?.PopText(LocalizationManager.GetPlainText(i18n_Key_RobotStopInspect));
            bIsCollecting = false;
        }

        public void OpenRobotInventory()
        {
            OpenLootInventory().Forget();
        }

        public void SellAllItem()
        {
            SellItems.SellRobotItemsToShop();
        }
    }
}
