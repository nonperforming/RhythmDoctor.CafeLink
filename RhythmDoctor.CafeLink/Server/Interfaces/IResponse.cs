using System.Net.Http;

namespace RhythmDoctor.CafeLink.Server.Interfaces;

public interface IResponse
{
  HttpStatusCode StatusCode { get; }
  string ContentType => "application/json";
  IReadOnlyCollection<byte> Data { get; }
}
