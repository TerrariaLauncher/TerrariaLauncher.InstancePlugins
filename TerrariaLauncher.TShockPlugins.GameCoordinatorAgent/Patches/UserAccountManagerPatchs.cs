namespace TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.Patches
{
    class UserAccountManagerPatchs
    {
        [HarmonyLib.HarmonyPatch(typeof(TShockAPI.DB.UserAccountManager))]
        [HarmonyLib.HarmonyPatch(HarmonyLib.MethodType.Constructor)]
        [HarmonyLib.HarmonyPatch(new[] { typeof(System.Data.IDbConnection) })]
        class ConstructorPatch
        {
            static bool Prefix(TShockAPI.DB.UserAccountManager __instance)
            {
                return true;
            }

            static void Postfix(TShockAPI.DB.UserAccountManager __instance)
            {

            }
        }
    }
}
