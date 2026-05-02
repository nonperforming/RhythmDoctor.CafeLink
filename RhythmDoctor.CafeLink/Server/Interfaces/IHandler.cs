using System.Net.Http;

namespace RhythmDoctor.CafeLink.Server.Interfaces;

internal interface IHandler
{
  internal string Endpoint { get; }

  HttpMethod AcceptedMethod { get; }

  internal IResponse HandleRequest(NameValueCollection parameters);
}
