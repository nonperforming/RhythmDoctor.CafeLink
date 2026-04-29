namespace RhythmDoctor.CafeLink.Server.Responses;

public class ImportResponse : IResponse
{
  public HttpStatusCode StatusCode => _results.Count == _badCount ? HttpStatusCode.BadRequest : HttpStatusCode.Accepted;

  public IReadOnlyCollection<byte> Data
  {
    get
    {
      // csharpier-ignore
      List<byte> result = [.."["u8];
      foreach (IResponse response in _results)
      {
        result.AddRange(response.Data);
        result.AddRange(","u8.ToArray());
      }
      // ReSharper disable once GrammarMistakeInComment
      result[^1] = 0x5d; // overwrite , with ]

      return result;
    }
  }

  private readonly IList<IResponse> _results = [];
  private int _badCount;

  internal void AddResult(IResponse result)
  {
    _results.Add(result);
    if (result is ErrorResponse)
    {
      _badCount++;
    }
  }
}
