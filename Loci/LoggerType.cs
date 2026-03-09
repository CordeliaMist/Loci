namespace Loci;

// Categories of logging.
[Flags]
public enum LoggerType : long
{
    None            = 0L,
    // Essential
    Mediator        = 1L << 0,
    Objects         = 1L << 1,
    Framework       = 1L << 2,
    // Processing
    Memory          = 1L << 3,
    Processors      = 1L << 4,
    Updates         = 1L << 5,
    SheVfx          = 1L << 6,
    // Data
    Data            = 1L << 7,
    DataManagement  = 1L << 8,
    // IPC   
    IpcProvider     = 1L << 9,
    Ipc             = 1L << 10,

    // All Recommended types.
    Recommended = Objects | Data | DataManagement | IpcProvider,
}
