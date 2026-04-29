namespace RhythmDoctor.CafeLink;

[Flags]
public enum BusyStatus
{
  None = 0,
  CleaningUpTransientPlay = 1 << 0,
  Importing = 1 << 1,
  ProcessingTransient = 1 << 2,
}
