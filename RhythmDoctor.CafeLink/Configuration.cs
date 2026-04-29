using BepInEx.Configuration;

namespace RhythmDoctor.CafeLink;

internal class Configuration
{
  // Security
  internal ConfigEntry<bool> AllowRemoteRequests;

  // Experimental
#if DEBUG
  internal ConfigEntry<bool> UseVirtualFilesystem;
#endif

  internal Configuration(ConfigFile config)
  {
    Plugin.Logger.LogDebug($"[{nameof(Configuration)}] Binding configuration");

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
