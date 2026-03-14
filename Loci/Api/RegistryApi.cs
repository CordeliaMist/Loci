using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Loci.Data;
using Loci.Services;
using LociApi.Api;
using LociApi.Enums;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxy24.Delegates;

namespace Loci.Api;

public class RegistryApi(ApiHelpers helpers, LociManager manager) : ILociApiRegistry
{
    public LociApiEc RegisterByPtr(nint address, string hostLabel)
    {
        if (!CharaWatcher.Rendered.Contains(address))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
            return LociApiEc.TargetNotFound;

        var res = helpers.AddEphemeralHost(actorSM, hostLabel);
        // Fire here to prevent circular call loop where a listener re-registers from its own call.
        if (res is LociApiEc.Success && actorSM.OwnerValid)
            ActorHostsChanged?.Invoke(actorSM.OwnerAddress, hostLabel);

        return res;
    }

    public LociApiEc RegisterByName(string charaName, string buddyName, string hostLabel)
    {
        var name = helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;

        var res = helpers.AddEphemeralHost(actorSM, hostLabel);
        // Fire here to prevent circular call loop where a listener re-registers from its own call.
        if (res is LociApiEc.Success && actorSM.OwnerValid)
            ActorHostsChanged?.Invoke(actorSM.OwnerAddress, hostLabel);

        return res;
    }

    public LociApiEc UnregisterByPtr(nint address, string hostLabel, bool clearData)
    {
        if (!CharaWatcher.Rendered.Contains(address))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
            return LociApiEc.TargetNotFound;

        var res = helpers.RemoveEphemeralHost(actorSM, hostLabel);
        // Fire here to prevent circular call loop where a listener re-registers from its own call.
        if (res is LociApiEc.Success && actorSM.OwnerValid)
            ActorHostsChanged?.Invoke(actorSM.OwnerAddress, hostLabel);

        // Clear the data if we desired.
        if (clearData)
        {
            // Clear out the data for this SM.
            helpers.ClearActorSM(actorSM);
            // If they are not rendered, remove them, as they were ephemeral.
            manager.RemoveIfStale(actorSM);
        }

        return res;
    }

    public LociApiEc UnregisterByName(string charaName, string buddyName, string hostLabel, bool clearData)
    {
        var name = helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;

        var res = helpers.RemoveEphemeralHost(actorSM, hostLabel);
        // Fire here to prevent circular call loop where a listener re-registers from its own call.
        if (res is LociApiEc.Success && actorSM.OwnerValid)
            ActorHostsChanged?.Invoke(actorSM.OwnerAddress, hostLabel);

        // Clear the data if we desired.
        if (clearData)
        {
            // Clear out the data for this SM.
            helpers.ClearActorSM(actorSM);
            // If they are not rendered, remove them, as they were ephemeral.
            manager.RemoveIfStale(actorSM);
        }

        return res;
    }

    // Quick one-line solution to iterated removal of a defined host label
    public int UnregisterAll(string hostLabel, bool clearData)
    {
        // Iterate through all LociManagers, and remove the ephemeralHost.
        // If cleardata is true, clear their statuses as well (ClientLockedStatuses ignored)
        int unregistered = 0;
        foreach (var (lociName, data) in LociManager.Managers.ToList())
        {
            if (!data.EphemeralHosts.Remove(hostLabel))
                continue;

            // Fire here to prevent circular call loop where a listener re-registers from its own call.
            if (data.OwnerValid)
                ActorHostsChanged?.Invoke(data.OwnerAddress, hostLabel);

            unregistered++;
            // If we dont wanna clear data, continue.
            if (!clearData)
                 continue;

            // Clear out the data for this SM.
            helpers.ClearActorSM(data);
            // If they are not rendered, remove them, as they were ephemeral.
            manager.RemoveIfStale(data);
        }
        return unregistered;
    }

    public List<string> GetHostsByPtr(nint address)
        => LociManager.Rendered.TryGetValue(address, out var actorSM) ? [.. actorSM.EphemeralHosts] : [];

    public List<string> GetHostsByName(string charaName, string buddyName)
    {
        var name = helpers.ToLociName(charaName, buddyName);
        return LociManager.Managers.TryGetValue(name, out var actorSM) ? [.. actorSM.EphemeralHosts] : [];
    }

    public int GetHostActorCount(string hostLabel)
        => LociManager.Managers.Values.Count(sm => sm.EphemeralHosts.Contains(hostLabel));


    public event Action<nint, string>? ActorHostsChanged;
}