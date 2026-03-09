using CkCommons;
using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Loci.Services;
using System.Globalization;

namespace Loci.Gui;

public class IpcTesterRegistration : IIpcTesterGroup
{
    private ICallGateSubscriber<nint, string, bool> _registerActorByPtr;    // Mark an actor for use by pointer using an identification code.
    private ICallGateSubscriber<string, string, bool> _registerActorByName;   // Mark an actor for use by name using an identification code.
    private ICallGateSubscriber<nint, string, bool> _unregisterActorByPtr;  // Unmark an actor by pointer.
    private ICallGateSubscriber<string, string, bool> _unregisterActorByName; // Unmark an actor by name.

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _nameToProcess = string.Empty;
    private string _tagToBind = string.Empty;

    private string _lastRegisteredActor = string.Empty;
    private string _lastUnregisteredActor = string.Empty;
    private string _lastRegisteredCode = string.Empty;
    private string _lastUnregisteredCode = string.Empty;
    private bool _lastReturnCode = false;

    public IpcTesterRegistration()
    {
        _registerActorByPtr = Svc.PluginInterface.GetIpcSubscriber<nint, string, bool>("Loci.RegisterActorByPtr");
        _registerActorByName = Svc.PluginInterface.GetIpcSubscriber<string, string, bool>("Loci.RegisterActorByName");
        _unregisterActorByPtr = Svc.PluginInterface.GetIpcSubscriber<nint, string, bool>("Loci.UnregisterActorByPtr");
        _unregisterActorByName = Svc.PluginInterface.GetIpcSubscriber<string, string, bool>("Loci.UnregisterActorByName");
    }

    public bool IsSubscribed { get; private set; } = false;

    public void Subscribe() => IsSubscribed = true;
    public void Unsubscribe() => IsSubscribed = false;
    public void Dispose() => Unsubscribe();

    public unsafe void Draw()
    {
        using var _ = ImRaii.TreeNode("Registration");
        if (!_) return;

        if (ImGui.InputTextWithHint("##drawObject", "Player Address..", ref _actorAddrString, 16, ImGuiInputTextFlags.CharsHexadecimal))
            _actorAddr = nint.TryParse(_actorAddrString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp) ? tmp : nint.Zero;
        ImGui.InputTextWithHint("##actorName", "Player Name@World...", ref _nameToProcess, 100);
        ImGui.InputTextWithHint("##binding-tag", "HostTagToAssign", ref _tagToBind, 60);

        using var table = ImRaii.Table(string.Empty, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Last Return Code");
        ImGui.TableNextColumn();
        CkGui.ColorTextBool(_lastReturnCode ? "Success" : "Failure", _lastReturnCode);

        IpcTesterTab.DrawIpcRowStart("Loci.RegisterActorByPtr", "Register w/ Address");
        if (CkGui.SmallIconTextButton(FAI.Share, "Register", disabled: !IsSubscribed || _actorAddr == nint.Zero))
        {
            _lastReturnCode = _registerActorByPtr.InvokeFunc(_actorAddr, _tagToBind);
            if (_lastReturnCode)
            {
                // Attempt to fetch the chara.
                if (CharaWatcher.TryGetValue(_actorAddr, out Character* chara))
                {
                    _lastRegisteredActor = $"{chara->GetNameWithWorld()} ({(nint)chara})";
                    _lastRegisteredCode = _tagToBind;
                }
            }
        }

        IpcTesterTab.DrawIpcRowStart("Loci.RegisterActorByName", "Register w/ PlayerName@World");
        if (CkGui.SmallIconTextButton(FAI.Share, "Register", disabled: !IsSubscribed || _nameToProcess.Length > 0))
        {
            _lastReturnCode = _registerActorByName.InvokeFunc(_nameToProcess, _tagToBind);
            if (_lastReturnCode)
            {
                _lastRegisteredActor = _nameToProcess;
                _lastRegisteredCode = _tagToBind;
            }
        }

        IpcTesterTab.DrawIpcRowStart("Loci.UnregisterActorByPtr", "Unregister w/ Address");
        if (CkGui.SmallIconTextButton(FAI.Share, "Unregister", disabled: !IsSubscribed || _actorAddr == nint.Zero))
        {
            _lastReturnCode = _unregisterActorByPtr.InvokeFunc(_actorAddr, _tagToBind);
            if (_lastReturnCode)
            {
                // Attempt to fetch the chara.
                if (CharaWatcher.TryGetValue(_actorAddr, out Character* chara))
                {
                    _lastUnregisteredActor = $"{chara->GetNameWithWorld()} ({(nint)chara})";
                    _lastUnregisteredCode = _tagToBind;
                }
            }
        }

        IpcTesterTab.DrawIpcRowStart("Loci.UnregisterActorByName", "Unregister w/ PlayerName@World");
        if (CkGui.SmallIconTextButton(FAI.Share, "Unregister", disabled: !IsSubscribed || _nameToProcess.Length > 0))
        {
            _lastReturnCode = _unregisterActorByName.InvokeFunc(_nameToProcess, _tagToBind);
            if (_lastReturnCode)
            {
                _lastUnregisteredActor = _nameToProcess;
                _lastUnregisteredCode = _tagToBind;
            }
        }
    }
}
