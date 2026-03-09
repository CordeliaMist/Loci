using Loci.Data;

namespace Loci.Services.Mediator;

// DDS
public record FolderUpdateManagers : MessageBase;

// CKFS
public enum FSChangeType { Created, Deleted, Renamed, Modified }

public record LociStatusChanged(FSChangeType Type, LociStatus Item, string? OldString = null) : MessageBase;
public record LociPresetChanged(FSChangeType Type, LociPreset Item, string? OldString = null) : MessageBase;
// Can add events here later.
public record ReloadCKFS(bool IsPresetFS) : MessageBase;