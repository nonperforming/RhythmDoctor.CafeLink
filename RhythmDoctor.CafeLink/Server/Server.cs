using System.Net.Http;
using System.Text;

namespace RhythmDoctor.CafeLink.Server;

internal class Server
{
  internal const uint PROTOCOL_VERSION = 0;

  private static readonly string[] ValidHosts = ["127.0.0.1"];

  private readonly HttpListener _listener;
  private ushort _port;
  private readonly Dictionary<string, IHandler> _handlers = [];

  internal Server(ushort port)
  {
    Plugin.Logger.LogInfo($"[{nameof(Server)}] Creating server for port {port}");
    _port = port;

    if (!HttpListener.IsSupported)
    {
      Plugin.Logger.LogError($"[{nameof(Server)}] HttpListener is not supported");
      throw new NotSupportedException("HttpListener is not supported.");
    }

    _listener = new HttpListener();
    foreach (string validHost in ValidHosts)
    {
      Plugin.Logger.LogInfo($"[{nameof(Server)}] Binding to http:{validHost}:{port}/");
      _listener.Prefixes.Add($"http://{validHost}:{port}/");
    }

    IHandler[] handlers = [new ImportHandler(), new PlayHandler(), new StatusHandler()];
    foreach (IHandler handler in handlers)
    {
      Plugin.Logger.LogInfo($"[{nameof(Server)}] Adding handler for {handler.Endpoint}");
      _handlers.Add(handler.Endpoint, handler);
    }

    Plugin.Logger.LogInfo($"[{nameof(Server)}] Started");
    Task.Run(HandleRequest);
  }

  private async Task HandleRequest()
  {
    Plugin.Logger.LogInfo($"[{nameof(Server)}] Waiting for request...");
    _listener.Start();

    HttpListenerContext httpContext = await _listener.GetContextAsync();
    HttpListenerRequest httpRequest = httpContext.Request;
    HttpListenerResponse httpResponse = httpContext.Response;
    httpResponse.KeepAlive = false;
    httpResponse.ContentEncoding = Encoding.UTF8;

    Plugin.Logger.LogInfo($"[{nameof(Server)}] Handling request...");

#if DEBUG
    Plugin.Logger.LogDebug($"[{nameof(Server)}] --- Request information ---");
    Plugin.Logger.LogDebug($"[{nameof(Server)}] Accept-Types: {string.Join(',', httpRequest.AcceptTypes)}");
    Plugin.Logger.LogDebug($"[{nameof(Server)}] Query strings:");
    foreach (string o in httpRequest.QueryString)
    {
      Plugin.Logger.LogDebug($"[{nameof(Server)}]   k: {o}");
      Plugin.Logger.LogDebug($"[{nameof(Server)}]   v: {httpRequest.QueryString[o]}");
    }

    Plugin.Logger.LogDebug($"[{nameof(Server)}] URL: {httpRequest.Url}");
    Plugin.Logger.LogDebug($"[{nameof(Server)}] ---------------------------");
#endif

    if (!httpRequest.IsLocal && !Plugin.Configuration.AllowRemoteRequests.Value)
    {
      httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;
      goto SendResponseAndGetReadyForNextRequest;
    }

    if (httpRequest.AcceptTypes?.Contains("application/json") != true)
    {
      httpResponse.StatusCode = (int)HttpStatusCode.NotAcceptable;
      goto SendResponseAndGetReadyForNextRequest;
    }

    string? endpoint = null;
    foreach (string urlSegment in httpRequest.Url.Segments)
    {
      if (urlSegment == "/")
        continue;

      endpoint = urlSegment.Replace("/", "");
      break;
    }

    IResponse response;
    if (string.IsNullOrEmpty(endpoint))
    {
      response = new ErrorResponse(HttpStatusCode.BadRequest, "No endpoint specified.");
      goto SendResponseWithDataAndGetReadyForNextRequest;
    }

    Plugin.Logger.LogInfo($"[{nameof(Server)}] Handling '{endpoint}' request...");
    if (_handlers.TryGetValue(endpoint, out IHandler handler))
    {
      response =
        handler.AcceptedMethod.Method != httpRequest.HttpMethod
          ? new ErrorResponse(HttpStatusCode.MethodNotAllowed, $"Expected {handler.AcceptedMethod.Method} method.")
          : handler.HandleRequest(httpContext.Request.QueryString);
    }
    else
    {
      response = new ErrorResponse(HttpStatusCode.NotFound, "Unknown endpoint.");
    }

    SendResponseWithDataAndGetReadyForNextRequest:
    IReadOnlyCollection<byte> data = response.Data;
    httpResponse.StatusCode = (int)response.StatusCode;
    httpResponse.ContentType = response.ContentType;
    httpResponse.ContentLength64 = data.Count;
    await httpResponse.OutputStream.WriteAsync(data.ToArray(), 0, data.Count);

    SendResponseAndGetReadyForNextRequest:
    httpResponse.Close();
    _listener.Stop();

    _ = Task.Run(HandleRequest);
  }
}
