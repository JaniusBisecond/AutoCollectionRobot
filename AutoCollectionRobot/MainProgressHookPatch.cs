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

    [HarmonyPatch(typeof(Button_LoadMainMenu), "BeginQuitting")]
    public static class ButtonLoadMainMenuBeginQuittingPatch
    {
        private static void Prefix()
        {
            try
            {
                ModBehaviour.Instance?.OnBeforeLevelChange();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

}
