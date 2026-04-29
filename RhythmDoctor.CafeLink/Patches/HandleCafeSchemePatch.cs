namespace RhythmDoctor.CafeLink.Patches;

[HarmonyPatch(typeof(LevelImporter))]
internal static class HandleCafeSchemePatch
{
  private static bool _hasPlayedInstallSound = false;

  [HarmonyPatch(nameof(LevelImporter.Install))]
  [HarmonyPostfix]
  private static IEnumerator FixupCafeSchemePatch(IEnumerator __result, LevelImporter __instance)
  {
    // FIXME: narration
    __instance.cls.CLSPlaySound("sndImportInstallButtonClick");
    _hasPlayedInstallSound = true;
    __instance.ToggleInsertUrlContainer(false);
    __instance.CurrentContentName = LevelImporter.ContentName.Installing;
    __instance.stoppedInstallCoroutine = false;
    __instance.CanToggleClearButton = false;
    foreach (ImportLevel item in __instance.toInstallIS.levels)
    {
      item.canToggleRemoveButton = false;
    }

    Plugin.Logger.LogDebug($"[{nameof(HandleCafeSchemePatch)}] Fixing up cafe schemes");
    for (int i = 0; i < __instance.toInstallIS.levels.Count; i++)
    {
      ImportLevel importLevel = __instance.toInstallIS.levels[i];
      if (!importLevel.isUrl || !importLevel.path.StartsWith("cafe://"))
        continue;

      string id = importLevel.path.Split('/')[2];

      if (!id.All(char.IsLetterOrDigit))
      {
        Plugin.Logger.LogError($"[{nameof(HandleCafeSchemePatch)}] Invalid ID {id}");
        continue;
      }

      Plugin.Logger.LogInfo($"[{nameof(HandleCafeSchemePatch)}] Getting level data for ID {id}");
      Task<LevelData?> getLevelDataTask = Cafe.GetLevelData(id);
      yield return new WaitUntil(() => getLevelDataTask.IsCompleted);

      if (getLevelDataTask.IsFaulted || getLevelDataTask.IsCanceled || !getLevelDataTask.Result.HasValue)
      {
        Plugin.Logger.LogError(
          $"[{nameof(HandleCafeSchemePatch)}] Failed to get level data for ID {id} (faulted: {getLevelDataTask.IsFaulted} - {getLevelDataTask.Exception}, cancelled: {getLevelDataTask.IsCanceled}, value: {getLevelDataTask.Result.HasValue})"
        );
        continue;
      }

      // Check if level is already imported - duplicated hash
      if (Plugin.TryFindLevelWithHash(getLevelDataTask.Result.Value.RDMD5, out _))
      {
        Plugin.Logger.LogWarning($"[{nameof(HandleCafeSchemePatch)}] Found duplicated hash for ID {id}");
        importLevel.customLevel._hash = getLevelDataTask.Result.Value.RDMD5;
        __instance.AddLevelToErrorSection(importLevel, RDString.Get("customLevelSelect.import.sameHashError"));
        __instance.toInstallIS.Remove(importLevel);
        i--;
        continue;
      }

      string url = getLevelDataTask.Result.Value.RDZipUrl.ToString();
      Plugin.Logger.LogInfo($"[{nameof(HandleCafeSchemePatch)}] Resolved RDZip url to {url} for ID {id}");
      importLevel.path = url;
    }

    Plugin.Logger.LogDebug($"[{nameof(HandleCafeSchemePatch)}] Running original Install coroutine");
    while (__result.MoveNext())
      yield return __result.Current;
  }

  [HarmonyPatch(typeof(scnCLS), nameof(scnCLS.CLSPlaySound))]
  [HarmonyPrefix]
  private static void DontDuplicateInstallButtonClickSoundPatch(string sound, ref bool __runOriginal)
  {
    if (sound == "sndImportInstallButtonClick" && _hasPlayedInstallSound)
    {
      __runOriginal = false;
      _hasPlayedInstallSound = false;
    }
  }
}
