using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace RhythmDoctor.CafeLink;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(PulseLib.MyPluginInfo.PLUGIN_GUID, PulseLib.MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  // ReSharper disable once NullableWarningSuppressionIsUsed
  internal static new ManualLogSource Logger = null!;
  internal static Configuration Configuration = null!;

  private static volatile int _status = (int)BusyStatus.None;
  internal static BusyStatus Status => (BusyStatus)_status;

  private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);
  private Server.Server _server = null!;

  private static List<CustomLevelData> LevelsData
  {
    get
    {
      if (scnBase.instance is scnCLS cls)
      {
        // TODO: check performance
        field = new List<CustomLevelData>(cls.levelsData);
      }
      return field;
    }
    set;
  } = null!;

  private void Awake()
  {
    Logger = base.Logger;
    Logger.LogInfo($"[{nameof(Plugin)}] Loading {MyPluginInfo.PLUGIN_NAME} version {MyPluginInfo.PLUGIN_VERSION}");

    Configuration = new Configuration(Config);

    Logger.LogInfo($"[{nameof(Plugin)}] Cleaning up any prior transient plays");
    Task.Run(CleanupTransientPlay);

    Logger.LogInfo($"[{nameof(Plugin)}] Creating {nameof(Server.Server)}");
    _server = new Server.Server(2615); // ☕

    Logger.LogInfo($"[{nameof(Plugin)}] Applying {nameof(DirectImportPatch)}");
    _harmony.PatchAll(typeof(DirectImportPatch));
    Logger.LogInfo($"[{nameof(Plugin)}] Applying {nameof(HandleCafeSchemePatch)}");
    _harmony.PatchAll(typeof(HandleCafeSchemePatch));
    Logger.LogInfo($"[{nameof(Plugin)}] Applying {nameof(HandleStatusPatch)}");
    _harmony.PatchAll(typeof(HandleStatusPatch));
#if DEBUG
    if (Configuration.UseVirtualFilesystem.Value)
    {
      Logger.LogWarning($"[{nameof(Plugin)}] Applying {nameof(ZipVirtualFilesystemPatch)}");
      _harmony.PatchAll(typeof(ZipVirtualFilesystemPatch));
    }
#endif

    Logger.LogInfo($"[{nameof(Plugin)}] {MyPluginInfo.PLUGIN_NAME} loaded!");
  }

  private void Start()
  {
    Logger.LogInfo($"[{nameof(Plugin)}] Building {nameof(LevelsData)}");
    // DesktopLevelLoader adds to scnCLS.instance.levelsData
    // fake a scnCLS instance so it doesn't error
    StubbedScnCLS stubbedCLS = gameObject.AddComponent<StubbedScnCLS>();
    stubbedCLS.levelsData = new List<CustomLevelData>();

    // save the proper scnLogo so we can restore it later
    scnBase properScn = scnBase._instance;
    scnBase._instance = stubbedCLS;
    DesktopLevelLoader.InitializeCacheLevelsDict();
    DesktopLevelLoader.LoadLevelsPreviewFromPath(LevelValidation.CustomLevelsPath);
    LevelsData = new List<CustomLevelData>(stubbedCLS.levelsData);

    // restore proper scnLogo and clean up
    scnBase._instance = properScn;
    DestroyImmediate(stubbedCLS);

    Logger.LogInfo($"[{nameof(Plugin)}] Built {nameof(LevelsData)}");
  }

  internal static async Task PlayTransient(string uri, bool twoPlayer)
  {
    if (!TrySetStatusFlag(BusyStatus.ProcessingTransient))
    {
      Logger.LogWarning(
        $"[{nameof(Plugin)}] Ignoring request to play transient {uri} because we are processing one already"
      );
      return;
    }

    Logger.LogWarning($"[{nameof(Plugin)}] Waiting for prior transient play to be cleaned up...");
    _ = Task.Run(CleanupTransientPlay);
    await Task.Run(async () =>
    {
      while (Status.HasFlag(BusyStatus.CleaningUpTransientPlay))
      {
        await Task.Delay(500);
      }
    });
    Logger.LogWarning($"[{nameof(Plugin)}] Prior play cleaned up");

    // extracted directory for non-vfs transient play
    DirectoryInfo? transientDir = null;

    try
    {
      Logger.LogInfo($"[{nameof(Plugin)}] Playing {uri} transiently... (2P: {twoPlayer})");

      using HttpClient client = new();
      client.DefaultRequestHeaders.UserAgent.Add(
        ProductInfoHeaderValue.Parse($"RhythmDoctor.CafeLink/{MyPluginInfo.PLUGIN_VERSION}")
      );

      if (uri.StartsWith("cafe://"))
      {
        string id = uri.Split('/').Last();
        Logger.LogInfo($"[{nameof(Plugin)}] Resolving rdzip url of cafe scheme...");
        LevelData? levelData = await Cafe.GetLevelData(id);

        if (!levelData.HasValue)
        {
          Logger.LogError($"[{nameof(Plugin)}] Could not get level data (is level ID valid?)");
          throw new ArgumentException("Could not get level data");
        }

        if (TryFindLevelWithHash(levelData.Value.RDMD5, out CustomLevelData matchedCustomLevelData))
        {
          Logger.LogWarning($"[{nameof(Plugin)}] Found already imported level, going to that instead");
          scnBase.currentLevelSelect = GC.SceneCustomLevelSelect;
          scnBaseExtensions.GoToLevelWithCustomLevelData(matchedCustomLevelData, twoPlayer);
          return;
        }

        uri = levelData.Value.RDZipUrl.AbsoluteUri;
      }

      Stream stream = null!;
      try
      {
        Logger.LogInfo($"[{nameof(Plugin)}] Trying to get stream");
        stream = await client.GetStreamAsync(uri);
      }
      catch
      {
        Logger.LogError($"[{nameof(Plugin)}] Failed to get stream, abandoning");
        if (stream is not null) // for some reason ? syntax not working here
        {
          await stream.DisposeAsync();
        }

        throw;
      }

      Logger.LogInfo($"[{nameof(Plugin)}] Got stream");

      string rootPath = "";
#if DEBUG
      if (Configuration.UseVirtualFilesystem.Value)
      {
        Logger.LogWarning($"[{nameof(Plugin)}] Creating virtual filesystem");
        ZipVirtualFilesystemPatch.UseVirtualFilesystem(stream);
      }
      else
      {
#endif
        rootPath = Path.Combine(LevelValidation.TempLevelsFolder, "cafelink-transient");
        transientDir = Directory.CreateDirectory(rootPath);
        Logger.LogInfo($"[{nameof(Plugin)}] Extracting to {rootPath}");

        using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
        {
          archive.ExtractToDirectory(rootPath);
        }

        Logger.LogInfo($"[{nameof(Plugin)}] Extracted to {rootPath}");
#if DEBUG
      }
#endif

      // If the level has a separate 2P level we need to load that instead.
      Logger.LogDebug($"[{nameof(Plugin)}] Attempting to get {nameof(RDLevelData)}");
      RDLevelData? customLevelData = LevelBase.DecodeLevelData(RDFile.ReadAllText(Path.Join(rootPath, "main.rdlevel")));

      if (customLevelData is null)
      {
        Logger.LogError($"[{nameof(Plugin)}] Could not get {nameof(RDLevelData)}");
        _ = Task.Run(CleanupTransientPlay);
        return;
      }

      // FIXME: This won't match for legacy levels - check with old hash also
      string newHash = RDUtils.GetHash(
        customLevelData.settings.author,
        customLevelData.settings.artist,
        customLevelData.settings.song
      );
      if (TryFindLevelWithHash(newHash, out CustomLevelData data))
      {
        // Go to the already imported level instead.
        Logger.LogWarning($"[{nameof(Plugin)}] Found already imported level, going to that instead");
        _ = Task.Run(CleanupTransientPlay);
        scnBase.currentLevelSelect = GC.SceneCustomLevelSelect;
        scnBaseExtensions.GoToLevelWithCustomLevelData(data, twoPlayer);
        return;
      }

      // TODO: clean this up
      string path =
        twoPlayer
        && customLevelData.settings.canBePlayedOn == LevelPlayMode.BothModes
        && !string.IsNullOrEmpty(customLevelData.settings.separate2PLevelFilename)
          ? Path.Join(rootPath, customLevelData.settings.separate2PLevelFilename)
          : Path.Join(rootPath, "main.rdlevel");
      Logger.LogInfo($"[{nameof(Plugin)}] Going to {path}");
      scnGame.goToScnMenuAfterHelpingHands = true; // go to main menu after level
      scnBase.GoToLevelWithExternalPath(path);
    }
    catch (Exception exception)
    {
      Logger.LogError(exception);
      transientDir?.Delete(true);
      throw;
    }
    finally
    {
      ClearStatusFlag(BusyStatus.ProcessingTransient);
    }
  }

  internal static void CleanupTransientPlay()
  {
    if (!TrySetStatusFlag(BusyStatus.CleaningUpTransientPlay))
    {
      Logger.LogWarning($"[{nameof(Plugin)}] Transient play is already being cleaned up");
      return;
    }

    try
    {
#if DEBUG
      ZipVirtualFilesystemPatch.DisposeVirtualFilesystem();
#endif
      DirectoryInfo info = new(Path.Combine(LevelValidation.TempLevelsFolder, "cafelink-transient"));
      try
      {
        Logger.LogInfo($"[{nameof(Plugin)}] Cleaning up cafelink-transient folder");
        info.Delete(true);
        Logger.LogInfo($"[{nameof(Plugin)}] Cleaned up cafelink-transient folder");
      }
      catch (DirectoryNotFoundException) { }
    }
    catch (Exception exception)
    {
      Logger.LogError(exception);
      throw;
    }
    finally
    {
      ClearStatusFlag(BusyStatus.CleaningUpTransientPlay);
    }
  }

  /// <summary>
  /// Tries to set a status flag.
  /// </summary>
  /// <param name="flag">Busy flag to set.</param>
  /// <returns>True if the flag was set, false if it was already set</returns>
  internal static bool TrySetStatusFlag(BusyStatus flag)
  {
    Logger.LogDebug($"[{nameof(Plugin)}] Setting status flag {flag}");

    while (true)
    {
      BusyStatus current = (BusyStatus)_status;

      // already set
      if ((current & flag) != 0)
        return false;

      BusyStatus updated = current | flag;
      if (Interlocked.CompareExchange(ref _status, (int)updated, (int)current) == (int)current)
        return true;

      // some other thread changed it... retry...
    }
  }

  /// <summary>
  /// Clears a status flag.
  /// </summary>
  /// <param name="flag">Busy flag to clear.</param>
  internal static void ClearStatusFlag(BusyStatus flag)
  {
    Logger.LogDebug($"[{nameof(Plugin)}] Clearing status flag {flag}");

    while (true)
    {
      BusyStatus current = (BusyStatus)_status;
      BusyStatus updated = current & ~flag;
      if (Interlocked.CompareExchange(ref _status, (int)updated, (int)current) == (int)current)
        return;

      // some other thread changed it... retry...
    }
  }

  internal static bool TryFindLevelWithHash(string hash, out CustomLevelData customLevelData)
  {
    customLevelData = LevelsData.Find(levelData => levelData.Hash == hash);
    return customLevelData != null;
  }

  internal static bool TryGoToLevelWithHash(string hash, bool twoPlayer = false)
  {
    if (!TryFindLevelWithHash(hash, out CustomLevelData customLevelData))
      return false;

    scnBaseExtensions.GoToLevelWithCustomLevelData(customLevelData, twoPlayer);
    return true;
  }
}
