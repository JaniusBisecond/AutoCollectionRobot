using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

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
}
