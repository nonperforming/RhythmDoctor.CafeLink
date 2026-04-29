namespace RhythmDoctor.CafeLink.Server.Handlers;

internal class ImportHandler : IHandler
{
  public string Endpoint => "import";

  public IResponse HandleRequest(NameValueCollection parameters)
  {
    if (!RDStartup.hasInitialized)
      return new ErrorResponse(HttpStatusCode.ServiceUnavailable, "Not initialized.");

    if (Plugin.Status.HasFlag(BusyStatus.Importing))
      return new ErrorResponse(HttpStatusCode.ServiceUnavailable, "Already importing something.");

    if (Plugin.Status.HasFlag(BusyStatus.ProcessingTransient))
      return new ErrorResponse(
        HttpStatusCode.ServiceUnavailable,
        "Currently processing prior request to play level transiently."
      );

    string[] uris = parameters["uri"].Split(',');

    ImportResponse response = new();
    foreach (string uri in uris)
    {
      Plugin.Logger.LogDebug($"[{nameof(Server)}] Checking URI {uri}");

      if (string.IsNullOrEmpty(uri))
      {
        Plugin.Logger.LogError($"[{nameof(Server)}] URI {uri} is empty");
        response.AddResult(new ErrorResponse(HttpStatusCode.BadRequest, "URI is empty."));
        continue;
      }

      if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri properUri))
      {
        Plugin.Logger.LogError($"[{nameof(Server)}] URI {uri} is bad");
        response.AddResult(new ErrorResponse(HttpStatusCode.BadRequest, "Bad URI given."));
        continue;
      }

      if (properUri.Scheme is not ("http" or "https" or "cafe"))
      {
        Plugin.Logger.LogError($"[{nameof(Server)}] URI {uri}'s scheme is not http[s]/cafe");
        response.AddResult(new ErrorResponse(HttpStatusCode.BadRequest, "Bad URI scheme."));
        continue;
      }
      Plugin.Logger.LogDebug($"[{nameof(Server)}] URI {uri} is OK");

      // Valid URI that we can handle!
      DirectImportPatch.AddUrisToImport(properUri);

      response.AddResult(new SuccessResponse());
    }

    if (response.StatusCode == HttpStatusCode.Accepted)
    {
      Plugin.TrySetStatusFlag(BusyStatus.Importing);
      // Go to Custom Level Select so the Level Importer can import our levels
      scnBase.GoToCustomLevelSelect();
    }

    return response;
  }
}
