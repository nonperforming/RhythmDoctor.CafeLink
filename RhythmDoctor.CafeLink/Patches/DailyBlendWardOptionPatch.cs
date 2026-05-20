using DG.Tweening;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDoctor.CafeLink.Patches;

[HarmonyPatch(typeof(scnCLS))]
internal static class DailyBlendWardOptionPatch
{
  private const scnCLS.WardOptionName DAILY_BLEND_WARD_OPTION_NAME = (scnCLS.WardOptionName)43414645;

  internal const string DAILY_BLEND_SELECT_SOUND = "sndWardSelectDailyBlend";

  [HarmonyPatch(nameof(scnCLS.Start))]
  [HarmonyPostfix]
  private static void AddDailyBlendWardOptionPatch(scnCLS __instance)
  {
    Plugin.Logger.LogDebug($"[{nameof(DailyBlendWardOptionPatch)}] Creating Daily Blend Ward option");
    // Duplicate Exit option and modify it
    // duplicating UI element
    RectTransform exitRectTransform =
      __instance.wardOptionsContainer.Find("Exit") as RectTransform ?? throw new InvalidOperationException();
    RectTransform dailyBlendRectTransform = Object.Instantiate(exitRectTransform, exitRectTransform.parent);
    dailyBlendRectTransform.SetSiblingIndex(exitRectTransform.parent.childCount - 2);
    dailyBlendRectTransform.SetName("Daily Blend");
    dailyBlendRectTransform.Find("ExitSign Container/Button/Text").GetComponent<Text>().text =
      "customLevelSelect.dailyBlend"; // localized by RDStringToUIText

    // TODO: remove
    scnCLS.WardOption temp_ExitWardOption = __instance.wardOptions[3];

    // duplicating WardOption
    scnCLS.WardOption dailyBlendWardOption =
      new()
      {
        name = DAILY_BLEND_WARD_OPTION_NAME,
        rect = dailyBlendRectTransform,
        signSilhouette = dailyBlendRectTransform.Find("ExitSign Container/SignSilhouette Image").gameObject,
        // TODO: Preview for detailContainer
        detailContainer = temp_ExitWardOption.detailContainer,
        // detailTitleToken, detailDescriptionToken are not needed as preview overwrites this
        detailTitleToken = "TODO-title",
        detailDescriptionToken = "TODO-description",
        // TODO: signImage;
        signImage = temp_ExitWardOption.signImage,
        // TODO: signBaseImage;
        signBaseImage = temp_ExitWardOption.signBaseImage,
        // TODO: signFlashImage;
        signFlashImage = temp_ExitWardOption.signFlashImage,
        spriteAnimation = dailyBlendRectTransform.Find("ExitSign Container").GetComponent<SpriteAnimation>(),
        // TODO: DefaultSprite;
        DefaultSprite = temp_ExitWardOption.DefaultSprite,
        // TODO: dailyBlendWardOption.introAnimData = ;
        introAnimData = temp_ExitWardOption.introAnimData,
        // TODO: dailyBlendWardOption.idleAnimData = ;
        idleAnimData = temp_ExitWardOption.idleAnimData,
        // TODO: dailyBlendWardOption.idleTween = ;
        idleTween = temp_ExitWardOption.idleTween,
      };

    __instance.wardOptions.Insert(__instance.wardOptions.Count - 1, dailyBlendWardOption);
  }

  [HarmonyPatch(nameof(scnCLS.SelectWardOption))]
  [HarmonyPostfix]
  private static void SelectDailyBlendWardOptionPatch(scnCLS __instance)
  {
    static async Task GoToDailyBlend()
    {
      Plugin.Logger.LogInfo($"[{nameof(DailyBlendWardOptionPatch)}] Getting daily blend...");
      BlendData? data = await Cafe.GetDailyBlend();
      if (data is null)
      {
        Plugin.Logger.LogError($"[{nameof(DailyBlendWardOptionPatch)}] Daily Blend is null!?");
        throw new Exception();
      }

      Plugin.Logger.LogInfo($"[{nameof(DailyBlendWardOptionPatch)}] Got daily blend, going now!");
      // TODO: making two requests. why?
      DirectImportPatch.SetUriToPlay($"cafe://{data.Value.Blend.Id}", true); // TODO: 2P
    }

    if (__instance.CurrentWardOption.name == DAILY_BLEND_WARD_OPTION_NAME)
    {
      Plugin.Logger.LogInfo($"[{nameof(DailyBlendWardOptionPatch)}] Daily Blend ward option selected");
      __instance.fadeTransitionImage.DOKill();
      __instance
        .fadeTransitionImage.DOFade(1f, 0.6f)
        .SetDelay(0.3f)
        .SetEase(Ease.InCubic)
        .SetUpdate(isIndependentUpdate: true)
        .OnComplete(() => _ = GoToDailyBlend());
    }
  }

  // TODO patch WardOptionSelectedFeedback

  [HarmonyPatch(nameof(scnCLS.ChangeWardOption))]
  [HarmonyTranspiler]
  private static IEnumerable<CodeInstruction> AddDailyBlendSelectSoundPatch(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchForward(
        true,
        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(scnCLS), nameof(scnCLS.CLSPlaySound))),
        new CodeMatch(OpCodes.Pop)
      )
      .Advance(3) // point to instruction after ldloc.1, loc1 is 'string sound'
      .Insert(
        Transpilers.EmitDelegate<Func<string, string>>(sound =>
        {
          if (scnCLS.instance.CurrentWardOption.name == DAILY_BLEND_WARD_OPTION_NAME)
            sound = DAILY_BLEND_SELECT_SOUND;

          return sound;
        })
      )
      .InstructionEnumeration();
  }
}
