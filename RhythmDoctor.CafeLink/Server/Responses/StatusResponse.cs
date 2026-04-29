namespace RhythmDoctor.CafeLink.Server.Responses;

internal class StatusResponse : IResponse
{
  public HttpStatusCode StatusCode => HttpStatusCode.OK;
  public required IReadOnlyCollection<byte> Data { get; init; }
}
