using System;
using System.IO;
using System.Text;
using System.Reflection;
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

        // 强制更新配置文件token
        public string configToken = "auto_collection_robot_v1";
    }

    public static class LocalConfig
    {
        private const string FileName = "AutoCollectRobotLocalConfig.json";

        private static string GetFolderPath()
        {
            try
            {
                string assemblyLocation = Path.GetDirectoryName(typeof(LocalConfig).Assembly.Location);
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    return assemblyLocation;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AutoCollectRobot: LocalConfig.GetFolderPath: failed to get assembly location: {e.Message}");
            }

            string fallback = Path.Combine(Application.persistentDataPath, "Mods", "AutoCollectionRobot");
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

                // 首次运行：创建默认配置文件并返回默认配置
                if (!File.Exists(FilePath))
                {
                    var def = new AutoCollectionRobotConfig();
                    SaveDefault(def);
                    Debug.Log($"AutoCollectRobot: LocalConfig.Load: created default config at {FilePath}");
                    return def;
                }

                // 文件为空 — 覆盖为默认（视为首次或损坏），并写入默认文件以便用户可以编辑
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    var def = new AutoCollectionRobotConfig();
                    SaveDefault(def);
                    Debug.LogWarning($"AutoCollectRobot: LocalConfig.Load: existing file empty, recreated default at {FilePath}");
                    return def;
                }

                var cfg = JsonUtility.FromJson<AutoCollectionRobotConfig>(json);
                if (cfg == null)
                {
                    cfg = new AutoCollectionRobotConfig();
                    SaveDefault(cfg);
                    Debug.LogWarning($"AutoCollectRobot: LocalConfig.Load: deserialization returned null, recreated default at {FilePath}");
                }
                else
                {
                    Debug.Log($"AutoCollectRobot: LocalConfig.Load: loaded config from {FilePath}");
                }

                return cfg;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new AutoCollectionRobotConfig();
            }
        }

        private static void SaveDefault(AutoCollectionRobotConfig config)
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
                Debug.Log($"AutoCollectRobot: LocalConfig.SaveDefault: saved default config to {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
