using BepInEx.Configuration;

namespace RhythmDoctor.CafeLink;

internal class Configuration
{
  // Daily Blend
  internal readonly ConfigEntry<bool> ShowPlayDailyBlendWardOption;
  internal readonly ConfigEntry<bool> CopyDailyBlendResult;
  internal readonly ConfigEntry<bool> ScreenshotDailyBlendResult;
  internal readonly ConfigEntry<int> DailyBlendNumber;
  internal readonly ConfigEntry<int> DailyBlendStreak;
  internal readonly ConfigEntry<int> LastCompletedDailyBlendTime;

  // Security
  internal readonly ConfigEntry<bool> AllowRemoteRequests;

  // Experimental
#if DEBUG
  internal readonly ConfigEntry<bool> UseVirtualFilesystem;
#endif

  internal Configuration(ConfigFile config)
  {
    Plugin.Logger.LogDebug($"[{nameof(Configuration)}] Binding configuration");

    #region Daily Blend
    ShowPlayDailyBlendWardOption = config.Bind(
      "Daily Blend",
      "ShowPlayDailyBlendWardOption",
      true,
      "Shows a ward option to play the current Daily Blend on the Custom Level Ward."
    );
    CopyDailyBlendResult = config.Bind(
      "Daily Blend",
      "CopyDailyBlendResult",
      true,
      "Copies the daily blend result into a formatted message that can be pasted into Rhythm Doctor Lounge's #daily-blend."
    );
    ScreenshotDailyBlendResult = config.Bind(
      "Daily Blend",
      "ScreenshotDailyBlendResult",
      true,
      "Takes a screenshot of the rank screen at the end of a blended level and places it on the Desktop."
    );
    DailyBlendNumber = config.Bind("Daily Blend", "DailyBlendNumber", 0, "The current blend number.");
    DailyBlendStreak = config.Bind(
      "Daily Blend",
      "DailyBlendStreak",
      0,
      "The current number of days the Daily Blend has been played in a row."
    );
    LastCompletedDailyBlendTime = config.Bind(
      "Daily Blend",
      "LastCompletedDailyBlendTime",
      0,
      "The last day the Daily Blend was completed. (Days since Unix epoch)"
    );
    #endregion

    AllowRemoteRequests = config.Bind(
      "Security",
      "AllowRemoteRequests",
      false,
      "Allows other devices on the network to import/play songs."
    );

#if DEBUG
    UseVirtualFilesystem = config.Bind(
      "Experimental",
      "UseVirtualFilesystem",
      false,
      $"Use {nameof(ZipVirtualFilesystem)} when playing levels transiently.\nCurrently very buggy (does not import sounds properly, etc.), do NOT use."
    );
#endif

    Plugin.Logger.LogDebug($"[{nameof(Configuration)}] Binding done");
  }
}
