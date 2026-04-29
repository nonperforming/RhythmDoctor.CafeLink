namespace RhythmDoctor.CafeLink.Patches;

[HarmonyPatch]
internal static class DirectImportPatch
{
  enum DirectPlayStatus
  {
    None,
    ImportAndPlay,
  }

  private static List<string> _urisToImport = [];
  private static DirectPlayStatus _directPlayStatus = DirectPlayStatus.None;
  private static bool _directPlayTwoPlayer = false;

  [HarmonyPatch(typeof(scnCLS), nameof(scnCLS.LateUpdate))]
  [HarmonyPostfix]
  private static void HandleDirectImportAndPlayPatch(scnCLS __instance)
  {
    if (_urisToImport.Count == 0)
      return;

    Plugin.Logger.LogInfo($"[{nameof(DirectImportPatch)}] Handling direct import and play");

    __instance.ChangeToImportOption();
    __instance.WardOptionSelectedFeedback();
    __instance.levelImporter.Showing = true;
  }

  [HarmonyPatch(typeof(LevelImporter), nameof(LevelImporter.LateUpdate))]
  [HarmonyPostfix]
  private static void AutoImportLevelsPatch(LevelImporter __instance)
  {
    if (_urisToImport.Count == 0)
      return;

    // We're essentially faking what would be the user inputting URLs to the import window.
    __instance.urlInput.text = string.Join('\n', _urisToImport); // Putting in URLs...
    __instance.ValidateUrl(); // Hitting return key/Install button

    // Start download
    __instance.Install_Public();

    // Cleanup
    _urisToImport.Clear();
  }

  [HarmonyPatch(typeof(LevelImporter), nameof(LevelImporter.Install))]
  [HarmonyPostfix]
  private static IEnumerator DirectPlayAfterInstallPatch(IEnumerator __result, LevelImporter __instance)
  {
    // run original enumerator
    while (__result.MoveNext())
      yield return __result.Current;

    if (_directPlayStatus != DirectPlayStatus.ImportAndPlay)
      yield break;
    _directPlayStatus = DirectPlayStatus.None;

    Plugin.Logger.LogInfo($"[{nameof(DirectImportPatch)}] Handling direct play - after install");

    if (__instance.errorsIS.levels.Count == 1 && __instance.installedIS.levels.Count == 0)
    {
      if (__instance.errorsIS.levels[0].errorToken == RDString.Get("customLevelSelect.import.sameHashError"))
      {
        // hash matched - find the level
        CustomLevelData levelData = __instance.cls.levelsData.Find(levelData =>
          levelData.Hash == __instance.errorsIS.levels[0].customLevel.Hash
        );

        scnBaseExtensions.GoToLevelWithCustomLevelData(levelData, _directPlayTwoPlayer);
      }
      // otherwise some other error occured when installing
    }
    else if (__instance.errorsIS.levels.Count == 0 && __instance.installedIS.levels.Count == 1)
    {
      // TODO: , __instance.installedIS.levels[0].path
      Plugin.Logger.LogWarning(__instance.installedIS.levels[0].customLevel.path);
      Plugin.Logger.LogWarning(__instance.installedIS.levels[0].path);
      scnBaseExtensions.GoToLevelWithCustomLevelData(
        __instance.installedIS.levels[0].customLevel,
        _directPlayTwoPlayer
      );
    }
    else
    {
      Plugin.Logger.LogError(
        $"[{nameof(DirectImportPatch)}] More than one imported/error: {__instance.errorsIS.levels.Count}, {__instance.installedIS.levels.Count}"
      );
    }
  }

  [HarmonyPatch(typeof(scnBase), nameof(scnBase.GoToScene))]
  [HarmonyPostfix]
  internal static void CleanupAfterTransientPlayPatch(string name)
  {
    if (name == GC.SceneGame)
      return;

    Task.Run(Plugin.CleanupTransientPlay);
  }

  internal static void AddUrisToImport(params IEnumerable<string> uris)
  {
    foreach (string uri in uris)
    {
      Plugin.Logger.LogInfo($"[{nameof(DirectImportPatch)}] Adding {uri} to import list");
      _urisToImport.Add(uri);
    }
  }

  internal static void AddUrisToImport(params IEnumerable<Uri> uris)
  {
    // URIs abandon capitalization - which are important for cafe level IDs
    AddUrisToImport(uris.Select((uri => uri.OriginalString)));
  }

  internal static void AddUrisToImportImmediate(params IEnumerable<string> uris)
  {
    AddUrisToImport(uris);
    Plugin.Logger.LogDebug($"[{nameof(DirectImportPatch)}] Import immediate - going to custom level select");
    scnBase.GoToCustomLevelSelect();
  }

  internal static void AddUrisToImportImmediate(params IEnumerable<Uri> uris)
  {
    // URIs abandon capitalization - which are important for cafe level IDs
    AddUrisToImportImmediate(uris.Select((uri => uri.OriginalString)));
  }

  internal static void SetUriToPlay(string uri, bool transient = false, bool twoPlayer = false)
  {
    Plugin.Logger.LogInfo($"[{nameof(DirectImportPatch)}] Setting to play {uri} (t: {transient}, 2p: {twoPlayer})");

    if (transient)
    {
      Task.Run(() => Plugin.PlayTransient(uri, twoPlayer));
      return;
    }

    // Go to the custom level ward to import and play our level.
    if (_urisToImport.Count != 0 && _urisToImport.Count > 1)
    {
      Plugin.Logger.LogWarning($"[{nameof(DirectImportPatch)}] _urisToPlay already exists - overwriting with {uri}");
      _urisToImport.Clear();
    }

    AddUrisToImport(uri);
    _directPlayStatus = DirectPlayStatus.ImportAndPlay;
    _directPlayTwoPlayer = twoPlayer;
    scnBase.GoToCustomLevelSelect();
  }

  // URIs abandon capitalization - which are important for cafe level IDs
  internal static void SetUriToPlay(Uri uri, bool transient = false, bool twoPlayer = false) =>
    SetUriToPlay(uri.OriginalString, transient, twoPlayer);
}
