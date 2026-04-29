namespace RhythmDoctor.CafeLink.Server.Responses;

internal class SuccessResponse : IResponse
{
  public HttpStatusCode StatusCode => HttpStatusCode.OK;
  public IReadOnlyCollection<byte> Data => "{\"status\":\"ok\"}"u8.ToArray();
}
