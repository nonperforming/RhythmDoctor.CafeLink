#if DEBUG
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RhythmDoctor.CafeLink;

/// <summary>
/// A read-only virtual filesystem for <see cref="RDFile"/> that takes in a zip file.
/// </summary>
internal class ZipVirtualFilesystem : RDFile, IDisposable
{
  private readonly ZipArchive _archive;

  internal ZipVirtualFilesystem(Stream zip)
  {
    _archive = new ZipArchive(zip, ZipArchiveMode.Read);
  }

  public override string InternalReadAllText(string path, Encoding? encoding = null)
  {
    path = path.TrimStart('/', '\\');

#if DEBUG
    Plugin.Logger.LogDebug($"[{nameof(ZipVirtualFilesystem)}] Handling {nameof(InternalReadAllText)}: {path}");
#endif

    encoding ??= DefaultEncoding;

    // encoding.GetString adds a BOM which causes RD to fail deserialization.
    // The original implementation internally uses File.ReadAllText -> File.InternalReadAllText which we mirror here:
    using MemoryStream stream = new(InternalReadAllBytes(path));
    using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true);
    return reader.ReadToEnd();
  }

  public override byte[] InternalReadAllBytes(string path)
  {
    path = path.TrimStart('/', '\\');

#if DEBUG
    Plugin.Logger.LogDebug($"[{nameof(ZipVirtualFilesystem)}] Handling {nameof(InternalReadAllBytes)}: {path}");
#endif

    ZipArchiveEntry? entry = _archive.GetEntry(path);
    if (entry is null)
      throw new FileNotFoundException(path);

    using Stream stream = entry.Open();
    using MemoryStream buf = new();

    stream.CopyTo(buf);
    return buf.ToArray();
  }

  public override bool InternalExists(string path)
  {
    path = path.TrimStart('/', '\\');

#if DEBUG
    Plugin.Logger.LogDebug($"[{nameof(ZipVirtualFilesystem)}] Handling {nameof(InternalExists)}: {path}");
#endif

    return _archive.GetEntry(path) is not null;
  }

  #region Stubbed methods
  public override void InternalWriteAllText(string path, string data, Encoding? encoding = null)
  {
    Plugin.Logger.LogWarning(
      $"[{nameof(ZipVirtualFilesystem)}] Ignoring {nameof(InternalWriteAllText)} request ({path}, {data}, {encoding})"
    );
  }

  public override void InternalWriteAllBytes(string path, byte[] bytes)
  {
    Plugin.Logger.LogWarning($"[{nameof(ZipVirtualFilesystem)}] Ignoring {nameof(WriteAllBytes)} request ({path})");
  }

  public override void InternalCopy(string sourceFileName, string destFileName, bool overwrite = false)
  {
    Plugin.Logger.LogWarning(
      $"[{nameof(ZipVirtualFilesystem)}] Ignoring {nameof(InternalCopy)} request ({sourceFileName}, {destFileName}, {overwrite})"
    );
  }

  public override void InternalDelete(string path)
  {
    Plugin.Logger.LogWarning($"[{nameof(ZipVirtualFilesystem)}] Ignoring {nameof(InternalDelete)} request ({path})");
  }

  public override void InternalMove(string sourceFileName, string destFileName)
  {
    Plugin.Logger.LogWarning(
      $"[{nameof(ZipVirtualFilesystem)}] Ignoring {nameof(InternalMove)} request ({sourceFileName}, {destFileName})"
    );
  }

  public override void InternalCreate(string path)
  {
    Plugin.Logger.LogWarning($"[{nameof(ZipVirtualFilesystem)}] Ignoring {nameof(InternalCreate)} request ({path})");
  }
  #endregion

  public override Encoding DefaultEncoding => RDEditorConstants.DefaultLevelEncoding;

  public void Dispose()
  {
    _archive.Dispose();
  }

#if DEBUG
  public void DumpData()
  {
    foreach (ZipArchiveEntry zipArchiveEntry in _archive.Entries)
    {
      Plugin.Logger.LogDebug(
        $"[{nameof(ZipVirtualFilesystem)}] {zipArchiveEntry.Name} - {zipArchiveEntry.Length} bytes ({zipArchiveEntry.LastWriteTime})"
      );
    }
  }
#endif
}
#endif
