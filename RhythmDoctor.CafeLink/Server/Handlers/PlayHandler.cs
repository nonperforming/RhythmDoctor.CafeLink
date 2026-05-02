using System.Net.Http;

namespace RhythmDoctor.CafeLink.Server.Handlers;

internal class PlayHandler : IHandler
{
  public string Endpoint => "play";
  public HttpMethod AcceptedMethod => HttpMethod.Post;

  public IResponse HandleRequest(NameValueCollection parameters)
  {
    if (!RDStartup.hasInitialized)
      return new ErrorResponse(HttpStatusCode.ServiceUnavailable, "Not initialized.");

    if (Plugin.Status.HasFlag(BusyStatus.Importing))
      return new ErrorResponse(HttpStatusCode.ServiceUnavailable, "Currently importing something.");

    bool transient = parameters.AllKeys.Contains("transient");
    string? uri = parameters["uri"];
    bool twoPlayer = parameters.AllKeys.Contains("2p");

    if (Plugin.Status.HasFlag(BusyStatus.ProcessingTransient))
      return new ErrorResponse(
        HttpStatusCode.ServiceUnavailable,
        "Currently processing prior request to play level transiently."
      );

    if (Plugin.Status.HasFlag(BusyStatus.CleaningUpTransientPlay) && transient)
      return new ErrorResponse(HttpStatusCode.ServiceUnavailable, "Currently cleaning up.");

    if (string.IsNullOrWhiteSpace(uri))
      return new ErrorResponse(HttpStatusCode.BadRequest, "No URI given.");

    if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri properUri))
      return new ErrorResponse(HttpStatusCode.BadRequest, "Bad URI given.");

    if (properUri.Scheme is not ("http" or "https" or "cafe"))
      return new ErrorResponse(HttpStatusCode.BadRequest, "Bad URI scheme.");

    // Valid URI that we can handle!
    // Pass it onto the LevelImporter and let it do its thing...
    DirectImportPatch.SetUriToPlay(properUri, transient, twoPlayer);

    return new SuccessResponse();
  }
}
