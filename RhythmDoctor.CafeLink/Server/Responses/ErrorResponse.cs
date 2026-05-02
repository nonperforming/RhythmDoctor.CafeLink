using System.Net.Http;
using System.Text;

namespace RhythmDoctor.CafeLink.Server.Responses;

internal class ErrorResponse : IResponse
{
  public HttpStatusCode StatusCode { get; } = HttpStatusCode.BadRequest;
  public IReadOnlyCollection<byte> Data { get; }

  internal ErrorResponse()
  {
    Data = "{\"status\":\"error\",\"reason\":\"null\"}"u8.ToArray();
  }

  internal ErrorResponse(HttpStatusCode statusCode, string reason)
  {
    StatusCode = statusCode;
    Data = Encoding.UTF8.GetBytes($"{{\"status\":\"error\",\"reason\":\"{reason}\"}}");
  }

  internal ErrorResponse(string reason)
  {
    Data = Encoding.UTF8.GetBytes($"{{\"status\":\"error\",\"reason\":\"{reason}\"}}");
  }
}
