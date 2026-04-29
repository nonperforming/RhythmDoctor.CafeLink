#if DEBUG
using System.IO;
using System.Text;
using NAudio.Vorbis;
using NAudio.Wave;

namespace RhythmDoctor.CafeLink.Patches;

[HarmonyPatch(typeof(RDFile_Default))]
internal static class ZipVirtualFilesystemPatch
{
  private static ZipVirtualFilesystem? _virtualFilesystem;

  internal static void UseVirtualFilesystem(ZipVirtualFilesystem virtualFilesystem)
  {
    if (_virtualFilesystem is not null)
    {
      Plugin.Logger.LogWarning($"[{nameof(ZipVirtualFilesystemPatch)}] Disposing of previous virtual filesystem");
      _virtualFilesystem.Dispose();
    }

    Plugin.Logger.LogInfo($"[{nameof(ZipVirtualFilesystemPatch)}] Using virtual filesystem");
    _virtualFilesystem = virtualFilesystem;
  }

  internal static void UseVirtualFilesystem(Stream stream) => UseVirtualFilesystem(new ZipVirtualFilesystem(stream));

  internal static void DisposeVirtualFilesystem()
  {
    Plugin.Logger.LogInfo($"[{nameof(ZipVirtualFilesystemPatch)}] Disposing of virtual filesystem");
    _virtualFilesystem?.Dispose();
    _virtualFilesystem = null;
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalWriteAllText))]
  [HarmonyPrefix]
  private static void InternalWriteAllTextPatch(string path, string data, Encoding encoding, ref bool __runOriginal)
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    _virtualFilesystem.InternalWriteAllText(path, data, encoding);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalWriteAllBytes))]
  [HarmonyPrefix]
  private static void InternalWriteAllBytesPatch(string path, byte[] bytes, ref bool __runOriginal)
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    _virtualFilesystem.InternalWriteAllBytes(path, bytes);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalReadAllText))]
  [HarmonyPrefix]
  private static void InternalReadAllTextPatch(
    string path,
    Encoding encoding,
    ref bool __runOriginal,
    ref string __result
  )
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    __result = _virtualFilesystem.InternalReadAllText(path, encoding);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalReadAllBytes))]
  [HarmonyPrefix]
  private static void InternalReadAllBytesPatch(string path, ref bool __runOriginal, ref byte[] __result)
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    __result = _virtualFilesystem.InternalReadAllBytes(path);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalExists))]
  [HarmonyPrefix]
  private static void InternalExistsPatch(string path, ref bool __runOriginal, ref bool __result)
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    __result = _virtualFilesystem.InternalExists(path);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalCopy))]
  [HarmonyPrefix]
  private static void InternalCopyPatch(
    string sourceFileName,
    string destFileName,
    bool overwrite,
    ref bool __runOriginal
  )
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    _virtualFilesystem.InternalCopy(sourceFileName, destFileName, overwrite);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalDelete))]
  [HarmonyPrefix]
  private static void InternalDeletePatch(string path, ref bool __runOriginal)
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    _virtualFilesystem.InternalDelete(path);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalMove))]
  [HarmonyPrefix]
  private static void InternalMovePatch(string sourceFileName, string destFileName, ref bool __runOriginal)
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    _virtualFilesystem.InternalMove(sourceFileName, destFileName);
  }

  [HarmonyPatch(nameof(RDFile_Default.InternalCreate))]
  [HarmonyPrefix]
  private static void InternalCreatePatch(string path, ref bool __runOriginal)
  {
    if (_virtualFilesystem is null)
      return;

    __runOriginal = false;
    string fixedPath = path.TrimStart('/', '\\');
    _virtualFilesystem.InternalCreate(fixedPath);
  }

  [HarmonyPatch(typeof(Resources), nameof(Resources.Load), typeof(string), typeof(Type))]
  [HarmonyPrefix]
  private static void RedirectResourcesLoadPatch(
    string path,
    Type systemTypeInstance,
    ref UnityEngine.Object __result,
    ref bool __runOriginal
  )
  {
    if (_virtualFilesystem is null)
      return;

    if (path.StartsWith("Sfx"))
      return;

    if (systemTypeInstance == typeof(AudioClip))
    {
      Plugin.Logger.LogInfo(
        $"[{nameof(ZipVirtualFilesystemPatch)}] Handling loading resource at {path}: {systemTypeInstance.FullName}"
      );
      __result = GetAudioClipFromFilename(path) ?? throw new InvalidOperationException();
      __runOriginal = false;
    }
    else
    {
      Plugin.Logger.LogWarning(
        $"[{nameof(ZipVirtualFilesystemPatch)}] Don't know how to handle type {systemTypeInstance.FullName}"
      );
    }
  }

  [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.FindOrLoadAudioClipExternal))]
  [HarmonyPostfix]
  private static IEnumerator FixLoadAudioClipExternalPatch(IEnumerator __result, string path, AudioManager __instance)
  {
    if (_virtualFilesystem is null)
    {
      while (__result.MoveNext())
        yield return __result.Current;
      yield break;
    }

    Plugin.Logger.LogDebug(
      $"[{nameof(ZipVirtualFilesystemPatch)}] Handling {nameof(AudioManager.FindOrLoadAudioClipExternal)}"
    );

    string filename = Path.GetFileName(path);
    if (__instance.audioLib.ContainsKey(filename + "*external"))
    {
      Plugin.Logger.LogDebug($"[{nameof(ZipVirtualFilesystemPatch)}] Hit cache for {filename}");
      yield return new RDAudioLoadResult(
        RDAudioLoadType.SuccessExternalClipLoaded,
        __instance.audioLib[filename + "*external"]
      );
      yield break;
    }

    Plugin.Logger.LogDebug(
      $"[{nameof(ZipVirtualFilesystemPatch)}] Missed cache for {filename}, creating new {nameof(AudioClip)}"
    );
    AudioType audioType = RDUtils.GetAudioType(Path.GetExtension(path));

    if (audioType is not (AudioType.AIFF or AudioType.OGGVORBIS or AudioType.WAV or AudioType.MPEG))
    {
      Plugin.Logger.LogError($"[{nameof(ZipVirtualFilesystemPatch)}] Unsupported audio type {audioType}");
      yield return new RDAudioLoadResult(RDAudioLoadType.ErrorFormatNotSupported, null);
      yield break;
    }

    // (Yes, we handle MP3 here in a different way from RD's.)
    // (RD's MP3 implementation is buggy anyway; this should help make it more consistent)

    // Deviation from vanilla coroutine
    // Instead of loading from disk, load from our virtual filesystem
    AudioClip? clip = GetAudioClipFromFilename(path);

    // Vanilla logic...
    string conductorName = (clip.name = filename + "*external");
    yield return new RDAudioLoadResult(RDAudioLoadType.SuccessExternalClipLoaded, clip);
    __instance.audioLib.TryAdd(conductorName, clip);
  }

  private static AudioClip? GetAudioClipFromFilename(string path)
  {
    static WaveStream GetWaveStream(MemoryStream audioStream, AudioType audioType, ref int sampleCount)
    {
      switch (audioType)
      {
        case AudioType.AIFF:
          return new AiffFileReader(audioStream);
        case AudioType.OGGVORBIS:
          return new VorbisWaveReader(audioStream);
        case AudioType.WAV:
          WaveFileReader wavFileReader = new(audioStream);
          sampleCount = (int)wavFileReader.SampleCount * wavFileReader.WaveFormat.Channels;
          return wavFileReader;
        case AudioType.MPEG:
          return new Mp3FileReader(audioStream);
        default:
          // should not be able to get here but compiler complains if this isn't here
          throw new ArgumentOutOfRangeException();
      }
    }

    if (_virtualFilesystem is null)
      return null;

    string filename = Path.GetFileName(path);
    AudioType audioType = RDUtils.GetAudioType(Path.GetExtension(path));

    // Unfortunately UnityWebRequestMultimedia.GetAudioClip takes a URI, and doesn't accept bytes.
    // To create an audio clip manually, we need the individual samples, channels, and sample rate of the audio.
    using MemoryStream audioStream = new(_virtualFilesystem.InternalReadAllBytes(path));

    // Get samples...
    int sampleCount = -1;
    using WaveStream waveStream = GetWaveStream(audioStream, audioType, ref sampleCount);

    int channels = waveStream.WaveFormat.Channels;
    int sampleRate = waveStream.WaveFormat.SampleRate;

    ISampleProvider sampleProvider = waveStream.ToSampleProvider();
    if (sampleCount == -1)
    {
      sampleCount = (int)(waveStream.Length / waveStream.BlockAlign * waveStream.WaveFormat.Channels);
    }
    Plugin.Logger.LogDebug(
      $"[{nameof(ZipVirtualFilesystemPatch)}] {channels} channels, {sampleRate}sample/s, {sampleCount} samples"
    );

    float[] samples = new float[sampleCount];
    sampleProvider.Read(samples, 0, sampleCount);

    Plugin.Logger.LogInfo($"[{nameof(ZipVirtualFilesystemPatch)}] Finished constructing {nameof(AudioClip)}");
    return AudioClip.Create(filename, samples.Length, channels, sampleRate, false);
  }
}
#endif
