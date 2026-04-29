using System.Text;

namespace RhythmDoctor.CafeLink.Server.Handlers;

internal class StatusHandler : IHandler
{
  public string Endpoint => "status";

  public IResponse HandleRequest(NameValueCollection parameters)
  {
    string status = "ok";
    string work = "";
    if (!RDStartup.hasInitialized)
    {
      status = "starting";
    }
    else if (Plugin.Status != BusyStatus.None)
    {
      status = "busy";
      foreach (BusyStatus enumStatus in Enum.GetValues(typeof(BusyStatus)))
      {
        work += $"\"{Enum.GetName(typeof(BusyStatus), enumStatus)!.ToLowerInvariant()}\",";
      }
    }
    work = work.TrimEnd(',');

    return new StatusResponse()
    {
      Data = Encoding.UTF8.GetBytes(
        $"{{\"status\":\"{status}\",\"work\":[{work}],\"protocol\":{Server.PROTOCOL_VERSION},\"version\":\"{MyPluginInfo.PLUGIN_VERSION}\"}}"
      ),
    };
  }
}
