using CkCommons;
using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Loci.Services;
using Loci.Services.Mediator;

using MoodleTuple = (
    int Version,
    System.Guid GUID,
    int IconID,
    string Title,
    string Description,
    string CustomVFXPath,
    long ExpireTicks,
    int Type,
    int Stacks,
    int StackSteps,
    uint Modifiers,
    System.Guid ChainedStatus,
    int ChainTrigger,
    string Applier,
    string Dispeller,
    bool Permanent
);

namespace Loci.Data;

public record MoodleData(int PosCnt, int NegCnt, int SpecCnt, int TotalCnt);

// Watch moodles to correctly calculate status offsets on targets, allowing for co-existance.
public sealed class MoodlesWatcher : DisposableMediatorSubscriberBase
{
    private readonly ICallGateSubscriber<int> ApiVersion;
    private readonly ICallGateSubscriber<nint, object> ManagedModified;
    private readonly ICallGateSubscriber<nint, List<MoodleTuple>> GetManagerInfo;

    internal static Dictionary<nint, MoodleData> Offsets = [];

    public MoodlesWatcher(ILogger<MoodlesWatcher> logger, LociMediator mediator)
        : base(logger, mediator)
    {
        ApiVersion = Svc.PluginInterface.GetIpcSubscriber<int>("Moodles.Version");
        ManagedModified = Svc.PluginInterface.GetIpcSubscriber<nint, object>("Moodles.StatusManagerModified");
        GetManagerInfo = Svc.PluginInterface.GetIpcSubscriber<nint, List<MoodleTuple>>("Moodles.GetStatusManagerInfoByPtrV2");
        ManagedModified.Subscribe(OnManagerModified);

        Svc.ClientState.Login += OnLogin;
        if (Svc.ClientState.IsLoggedIn)
            OnLogin();

        // Process object creation here
        Mediator.Subscribe<WatchedObjectCreated>(this, _ => OnObjectCreated(_.Address));
        Mediator.Subscribe<WatchedObjectDestroyed>(this, _ => OnObjectDeleted(_.Address));
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, _ => CheckAPI());
    }

    public static bool APIAvailable { get; private set; } = false;

    private void CheckAPI()
    {
        try
        {
            var prevRes = APIAvailable;
            APIAvailable = ApiVersion.InvokeFunc() >= 4;
            // Check mediator calls
            if (APIAvailable && !prevRes)
                CheckManagers();
            else if (!APIAvailable && prevRes)
                ClearManagers();
        }
        catch
        {
            APIAvailable = false;
        }
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Svc.ClientState.Login -= OnLogin;
        ManagedModified.Unsubscribe(OnManagerModified);
    }

    private async void OnLogin()
    {
        // Wait for the player to be fully loaded in first.
        await Utils.WaitForPlayerLoading().ConfigureAwait(false);
        // Init data
        CheckAPI();
        CheckManagers();
    }

    private async void OnObjectCreated(nint charaAddr)
    {
        if (!APIAvailable)
            return;

        // Add or update the dictionary.
        var newData = GetManagerInfo.InvokeFunc(charaAddr);
        if (newData is null)
            return;

        int[] counts = [0, 0, 0];
        foreach (var item in newData)
        {
            if (item.Type is 0) counts[0]++;
            else if (item.Type is 1) counts[1]++;
            else if (item.Type is 2) counts[2]++;
        }

        // Update the offset counts.
        Offsets[charaAddr] = new(counts[0], counts[1], counts[2], newData.Count);
    }

    private void OnObjectDeleted(nint charaAddr)
    {
        Offsets.Remove(charaAddr);
    }

    private async void OnManagerModified(nint charaAddr)
    {
        // Add or update the dictionary.
        var newData = GetManagerInfo.InvokeFunc(charaAddr);
        if (newData is null)
            return;
        
        int[] counts = [0, 0, 0];
        foreach (var item in newData)
        {
            if (item.Type is 0) counts[0]++;
            else if (item.Type is 1) counts[1]++;
            else if (item.Type is 2) counts[2]++;
        }
        // Update the offset counts.
        Offsets[charaAddr] = new(counts[0], counts[1], counts[2], newData.Count);
    }

    private async void CheckManagers()
    {
        if (!APIAvailable)
            return;

        try
        {
            foreach (var chara in CharaWatcher.Rendered.ToList())
            {
                var newData = GetManagerInfo.InvokeFunc(chara);
                if (newData is null)
                    continue;

                int[] counts = [0, 0, 0];
                foreach (var item in newData)
                {
                    if (item.Type is 0) counts[0]++;
                    else if (item.Type is 1) counts[1]++;
                    else if (item.Type is 2) counts[2]++;
                }

                Offsets[chara] = new(counts[0], counts[1], counts[2], newData.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking managers");
        }
    }

    private void ClearManagers()
        => Offsets.Clear();
    public static unsafe void DebugManagers()
    {
        CkGui.ColorText("Statuses applied by moodles:", ImGuiColors.DalamudYellow);
        foreach (var (charaAddr, dat) in Offsets)
        {
            if (dat.TotalCnt is 0)
                continue;
            ImGui.Text($"({((Character*)charaAddr)->GetNameWithWorld()}) {charaAddr:X}: " +
                $"({dat.PosCnt} Pos, {dat.NegCnt} Negative, {dat.SpecCnt} Special, {dat.TotalCnt} Total");
        }
    }
}