namespace RhythmDoctor.CafeLink.Server.Interfaces;

internal interface IHandler
{
  internal string Endpoint { get; }

  internal IResponse HandleRequest(NameValueCollection parameters);
}
