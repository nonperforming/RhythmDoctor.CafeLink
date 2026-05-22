using System.IO;
using NodaTime;
using TextCopy;
using SystemClock = NodaTime.SystemClock;

namespace RhythmDoctor.CafeLink.Patches;

[HarmonyPatch(typeof(Rankscreen))]
internal static class CopyDailyBlendResultsPatch
{
  private static bool _playingDailyBlend;

  [HarmonyPatch(nameof(Rankscreen.ShowAndSaveRank))]
  [HarmonyPostfix]
  private static void CopyBlendResultPatch(Rankscreen __instance)
  {
    if (!_playingDailyBlend)
      return;

    Plugin.Logger.LogInfo($"[{nameof(CopyDailyBlendResultsPatch)}] Completed Daily Blend level");
    _playingDailyBlend = false;

    // Check if we've already cleared the Daily Blend for the day
    // Daily Blend refreshes at 4AM GMT. GMT is UTC+0, so we can just use that.
    LocalDate unixEpochDate = new LocalDate(1970, 1, 1);
    LocalDate currentBlendDate = SystemClock.Instance.GetCurrentInstant().InUtc().Minus(Duration.FromHours(4)).Date; // current day at 4AM.
    int currentDay = Period.DaysBetween(unixEpochDate, currentBlendDate);
    Plugin.Logger.LogInfo(
      $"[{nameof(CopyDailyBlendResultsPatch)}] Saved day: {Plugin.Configuration.LastCompletedDailyBlendTime.Value}, current day: {currentDay}"
    );
    if (Plugin.Configuration.LastCompletedDailyBlendTime.Value >= currentDay)
      return; // We've already done the daily blend for the day.

    // We've just completed the daily blend for the day.
    // Update our stats and copy the completion text if applicable.
    bool streakAlive;

    // Blend number
    Plugin.Configuration.DailyBlendNumber.Value++;

    // Streak
    int daysSinceLastCompletion = Period.DaysBetween(
      unixEpochDate.PlusDays(Plugin.Configuration.LastCompletedDailyBlendTime.Value),
      currentBlendDate
    );
    if (daysSinceLastCompletion > 1)
    {
      // Streak is dead
      streakAlive = false;
      Plugin.Logger.LogInfo(
        $"[{nameof(CopyDailyBlendResultsPatch)}] {daysSinceLastCompletion} days since last completion, resetting streak"
      );
      Plugin.Configuration.DailyBlendStreak.Value++;
    }
    else
    {
      // Streak is alive
      streakAlive = true;
      Plugin.Logger.LogInfo(
        $"[{nameof(CopyDailyBlendResultsPatch)}] {daysSinceLastCompletion} day since last completion, continuing streak"
      );
      Plugin.Configuration.DailyBlendStreak.Value = 0;
    }
    Plugin.Configuration.LastCompletedDailyBlendTime.Value = currentDay;

    Plugin.Logger.LogInfo(
      $"[{nameof(CopyDailyBlendResultsPatch)}] Updated: blend number - {Plugin.Configuration.DailyBlendNumber.Value}, streak - {Plugin.Configuration.DailyBlendStreak.Value} (alive: {streakAlive}), current day - {currentDay}"
    );

    if (Plugin.Configuration.CopyDailyBlendResult.Value)
    {
      string rank = __instance.game.currentLevel.GetRankFromMistakes().ToString();
      float mistakes = __instance.game.mistakesManager.mistakes;
      _ = SetTextToCopy(rank, mistakes, streakAlive);
    }
    if (Plugin.Configuration.ScreenshotDailyBlendResult.Value)
      SaveScreenshot();
  }

  private static async Task SetTextToCopy(string rank, float mistakes, bool streakAlive)
  {
    static string GetStreak(bool streakAlive) =>
      streakAlive ? $"🔥{Plugin.Configuration.DailyBlendStreak.Value}" : "🧯1";

    string text =
      $"#{Plugin.Configuration.DailyBlendNumber.Value} / {GetStreak(streakAlive)}\n-# {rank} rank ({mistakes} mistakes)";
    Plugin.Logger.LogInfo($"[{nameof(CopyDailyBlendResultsPatch)}] Copying '{text}'");

    await ClipboardService.SetTextAsync(text);
  }

  private static void SaveScreenshot()
  {
    Plugin.Logger.LogInfo($"[{nameof(CopyDailyBlendResultsPatch)}] Saving screenshot of results");
    string path = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
      $"{Plugin.Configuration.DailyBlendNumber.Value}.png"
    );
    ScreenCapture.CaptureScreenshot(path);
  }

  internal static void PlayingDailyBlend()
  {
    _playingDailyBlend = true;
  }
}
