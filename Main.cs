using KitchenMods;
using PreferenceSystem;
using UnityEngine;

// Namespace should have "Kitchen" in the beginning
namespace KitchenMakeItYourself
{
    public class Main : IModInitializer
    {
        public const string MOD_GUID = $"IcedMilo.PlateUp.{MOD_NAME}";
        public const string MOD_NAME = "Make It Yourself!";
        public const string MOD_VERSION = "0.1.0";

        internal const string CUSTOMERS_GRAB_INGREDIENTS_ENABLED_ID = "customersGrabIngredientsEnabled";

        internal static PreferenceSystemManager PrefManager;

        public void PostActivate(KitchenMods.Mod mod)
        {
            LogWarning($"{MOD_GUID} v{MOD_VERSION} in use!");

            PrefManager = new PreferenceSystemManager(MOD_GUID, MOD_NAME);
            PrefManager
                .AddLabel("Customers Grab Ingredients")
                .AddOption<bool>(
                    CUSTOMERS_GRAB_INGREDIENTS_ENABLED_ID,
                    true,
                    new bool[] { false, true },
                    new string[] { "Disabled", "Enabled" })
                .AddSpacer()
                .AddSpacer();
        }

        public void PreInject()
        {
        }

        public void PostInject()
        {
        }

        #region Logging
        public static void LogInfo(string _log) { Debug.Log($"[{MOD_NAME}] " + _log); }
        public static void LogWarning(string _log) { Debug.LogWarning($"[{MOD_NAME}] " + _log); }
        public static void LogError(string _log) { Debug.LogError($"[{MOD_NAME}] " + _log); }
        public static void LogInfo(object _log) { LogInfo(_log.ToString()); }
        public static void LogWarning(object _log) { LogWarning(_log.ToString()); }
        public static void LogError(object _log) { LogError(_log.ToString()); }
        #endregion
    }
}
