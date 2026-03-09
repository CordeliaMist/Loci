namespace Loci.Gui;

public interface IIpcTesterGroup : IDisposable
{
    /// <summary>
    ///     Gets if we are currently subscribed to the IPC events.
    /// </summary>
    public bool IsSubscribed { get; }

    /// <summary>
    ///     A method to subscribe to all relevent IPC events.
    /// </summary>
    public void Subscribe();

    /// <summary>
    ///     A method to unsubscribe from all relevent IPC events. <para />
    /// </summary>
    public void Unsubscribe();
}
