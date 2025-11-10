using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using UnityEngine;

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

        // 强制更新配置文件token，用于判断是否需要迁移/补全新字段
        public string configToken = LocalConfig.CurrentConfigToken;
    }

    public static class LocalConfig
    {
        // 当前配置版本 token
        public const string CurrentConfigToken = "auto_collection_robot_v1";

        private const string FileName = "AutoCollectRobotLocalConfig.json";

        /// <summary>
        /// 配置目录优先逻辑：
        /// 1. 尝试../Escape from Duckov/Duckov_Data/Mods/AutoCollectRobot
        /// 2. 如果上面失败，则使用 DLL 所在目录
        /// 3. 最后回退到 Application.persistentDataPath/Mods/AutoCollectionRobot
        /// </summary>
        private static string GetFolderPath()
        {
            // 1) 尝试通过进程主模块获取 exe 路径
            try
            {
                string exePath = null;
                try
                {
                    using (Process proc = Process.GetCurrentProcess())
                    {
                        exePath = proc?.MainModule?.FileName;
                    }
                }
                catch (Exception exProc)
                {
                    UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: cannot get process main module: {exProc.Message}");
                }

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    string exeDir = Path.GetDirectoryName(exePath);
                    if (!string.IsNullOrEmpty(exeDir))
                    {
                        string candidate = Path.Combine(exeDir, "Duckov_Data", "Mods", "AutoCollectionRobot");
                        try
                        {
                            if (!Directory.Exists(candidate))
                                Directory.CreateDirectory(candidate);
                            return candidate;
                        }
                        catch (Exception exCreate)
                        {
                            UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: cannot use path based on exe ('{candidate}'): {exCreate.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: exe-path check failed: {e.Message}");
            }

            // 2) 尝试 DLL（assembly）所在目录
            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(LocalConfig).Assembly.Location);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    try
                    {
                        if (!Directory.Exists(assemblyDir))
                            Directory.CreateDirectory(assemblyDir);
                        return assemblyDir;
                    }
                    catch (Exception exAsm)
                    {
                        UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: cannot use assembly dir ('{assemblyDir}'): {exAsm.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: assembly-path check failed: {e.Message}");
            }

            // 3) 回退到 persistentDataPath
            string fallback = Path.Combine(Application.persistentDataPath, "Mods", "AutoCollectionRobot");
            try
            {
                if (!Directory.Exists(fallback))
                    Directory.CreateDirectory(fallback);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: cannot create fallback path ('{fallback}'): {e.Message}");
            }
            return fallback;
        }

        private static string FilePath => Path.Combine(GetFolderPath(), FileName);

        public static AutoCollectionRobotConfig Load()
        {
            try
            {
                string folder = GetFolderPath();

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (!File.Exists(FilePath))
                {
                    var def = new AutoCollectionRobotConfig();
                    def.configToken = CurrentConfigToken;
                    SaveToFile(def);
                    UnityEngine.Debug.Log($"AutoCollectRobot: LocalConfig.Load: created default config at {FilePath}");
                    return def;
                }

                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    var def = new AutoCollectionRobotConfig();
                    def.configToken = CurrentConfigToken;
                    SaveToFile(def);
                    UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig.Load: existing file empty, recreated default at {FilePath}");
                    return def;
                }

                var cfg = JsonUtility.FromJson<AutoCollectionRobotConfig>(json);
                if (cfg == null)
                {
                    cfg = new AutoCollectionRobotConfig();
                    cfg.configToken = CurrentConfigToken;
                    SaveToFile(cfg);
                    UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig.Load: deserialization returned null, recreated default at {FilePath}");
                    return cfg;
                }

                ValidateConfig(cfg);

                // 若 token 不匹配，说明配置结构或版本发生变化 —— 执行迁移：将用户已有值合并到新的默认配置，
                // 并写回文件以补全新字段（仅在版本变化时写回一次）。
                if (string.IsNullOrEmpty(cfg.configToken) || cfg.configToken != CurrentConfigToken)
                {
                    try
                    {
                        var migrated = MigrateConfig(cfg);
                        UnityEngine.Debug.Log($"AutoCollectRobot: LocalConfig.Load: migrated config and saved to {FilePath} (old token='{cfg.configToken}')");
                        cfg = migrated;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig.Load: migration failed: {e.Message}");
                    }
                }

                UnityEngine.Debug.Log($"AutoCollectRobot: LocalConfig.Load: loaded config from {FilePath}");
                return cfg;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return new AutoCollectionRobotConfig();
            }
        }

        // 将配置写入文件（用于首次创建、修复或迁移时）。内部统一使用，避免在普通加载过程中覆盖文件。
        private static void SaveToFile(AutoCollectionRobotConfig config)
        {
            try
            {
                if (config == null) return;

                string folder = GetFolderPath();
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(FilePath, json, Encoding.UTF8);
                UnityEngine.Debug.Log($"AutoCollectRobot: LocalConfig.SaveToFile: saved config to {FilePath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private static AutoCollectionRobotConfig MigrateConfig(AutoCollectionRobotConfig loaded)
        {
            if (loaded == null) return new AutoCollectionRobotConfig() { configToken = CurrentConfigToken };

            var merged = new AutoCollectionRobotConfig();

            // 需手动，可能存在缺少字段的情况
            merged.collectInterval = loaded.collectInterval;
            merged.robotInventoryNeedInspect = loaded.robotInventoryNeedInspect;
            merged.robotInventoryCapacity = loaded.robotInventoryCapacity;
            merged.collectGroundItems = loaded.collectGroundItems;
            merged.collectLootBox = loaded.collectLootBox;
            merged.collectRadius = loaded.collectRadius;
            merged.debugDrawCollectRadius = loaded.debugDrawCollectRadius;

            merged.configToken = CurrentConfigToken;

            ValidateConfig(merged);

            SaveToFile(merged);

            return merged;
        }

        private static void ValidateConfig(AutoCollectionRobotConfig cfg)
        {
            if (cfg == null) return;

            // collectInterval: [0.5, 10]
            float oldCollectInterval = cfg.collectInterval;
            if (float.IsNaN(oldCollectInterval) || float.IsInfinity(oldCollectInterval))
            {
                cfg.collectInterval = 2f;
            }
            else
            {
                cfg.collectInterval = Mathf.Clamp(oldCollectInterval, 0.5f, 10f);
            }
            if (!Mathf.Approximately(oldCollectInterval, cfg.collectInterval))
            {
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: collectInterval invalid ({oldCollectInterval}), clamped to {cfg.collectInterval}");
            }

            // collectRadius: [1, 50]
            float oldCollectRadius = cfg.collectRadius;
            if (float.IsNaN(oldCollectRadius) || float.IsInfinity(oldCollectRadius))
            {
                cfg.collectRadius = 10f;
            }
            else
            {
                cfg.collectRadius = Mathf.Clamp(oldCollectRadius, 1f, 50f);
            }
            if (!Mathf.Approximately(oldCollectRadius, cfg.collectRadius))
            {
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: collectRadius invalid ({oldCollectRadius}), clamped to {cfg.collectRadius}");
            }

            // robotInventoryCapacity: [10, 2048]
            int oldCapacity = cfg.robotInventoryCapacity;
            int minCap = 10, maxCap = 2048;
            if (oldCapacity < minCap)
            {
                cfg.robotInventoryCapacity = minCap;
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: robotInventoryCapacity too small ({oldCapacity}), set to {cfg.robotInventoryCapacity}");
            }
            else if (oldCapacity > maxCap)
            {
                cfg.robotInventoryCapacity = maxCap;
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: robotInventoryCapacity too large ({oldCapacity}), set to {cfg.robotInventoryCapacity}");
            }

            // configToken 检查（已在 Load 中处理迁移），此处仅记录提示
            if (string.IsNullOrEmpty(cfg.configToken))
            {
                UnityEngine.Debug.LogWarning($"AutoCollectRobot: LocalConfig: configToken missing. Consider regenerating config to pick up new defaults.");
            }
        }
    }
}
