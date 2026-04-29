namespace RhythmDoctor.CafeLink.Patches;

[HarmonyPatch]
internal static class HandleStatusPatch
{
  [HarmonyPatch(typeof(LevelImporter), nameof(LevelImporter.Install))]
  [HarmonyPostfix]
  [HarmonyPriority(Priority.VeryHigh)]
  private static void SetImportingStatusPatch()
  {
    Plugin.TrySetStatusFlag(BusyStatus.Importing);
  }

  [HarmonyPatch(typeof(LevelImporter), nameof(LevelImporter.Install))]
  [HarmonyPostfix]
  [HarmonyPriority(Priority.VeryLow)]
  private static void ClearImportingStatusPatch()
  {
    Plugin.ClearStatusFlag(BusyStatus.Importing);
  }
}
