using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.ProgrammableComputer;
using Content.Shared.UserInterface;
using Content.Server.Station.Systems;
using Robust.Server.GameObjects;

namespace Content.Server.ProgrammableComputer;

public sealed class ProgrammableComputerLinkerSystem : EntitySystem
{
    private const int MaxBufferedDevices = 64;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ProgrammableComputerSystem _programmable = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ProgrammableComputerLinkerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ProgrammableComputerLinkerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ProgrammableComputerLinkerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ProgrammableComputerLinkerComponent, ProgrammableComputerLinkerClearAllMessage>(OnClearAll);
        SubscribeLocalEvent<ProgrammableComputerLinkerComponent, ProgrammableComputerLinkerRemoveBufferedMessage>(OnRemoveBuffered);
    }

    private void OnAfterInteract(EntityUid uid, ProgrammableComputerLinkerComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<ProgrammableComputerComponent>(target, out _))
        {
            PairBufferedDevices(uid, target, args.User, comp);
            args.Handled = true;
            return;
        }

        if (!_programmable.IsAtmosLinkableDevice(target))
        {
            _popup.PopupEntity(
                Loc.GetString("programmable-computer-linker-invalid-target"),
                uid,
                args.User,
                PopupType.Medium);
            return;
        }

        var targetNet = GetNetEntity(target);
        if (comp.BufferedDevices.Remove(targetNet))
        {
            _popup.PopupEntity(
                Loc.GetString("programmable-computer-linker-device-removed", ("device", ToPrettyString(target))),
                uid,
                args.User,
                PopupType.Medium);
        }
        else
        {
            if (comp.BufferedDevices.Count >= MaxBufferedDevices)
            {
                _popup.PopupEntity(
                    Loc.GetString("programmable-computer-linker-buffer-full", ("count", MaxBufferedDevices)),
                    uid,
                    args.User,
                    PopupType.Medium);
                return;
            }

            comp.BufferedDevices.Add(targetNet);
            _popup.PopupEntity(
                Loc.GetString("programmable-computer-linker-device-buffered", ("device", ToPrettyString(target))),
                uid,
                args.User,
                PopupType.Medium);
        }

        UpdateUi(uid, comp);
        args.Handled = true;
    }

    private void PairBufferedDevices(EntityUid linkerUid, EntityUid computerUid, EntityUid userUid, ProgrammableComputerLinkerComponent linker)
    {
        if (linker.BufferedDevices.Count == 0)
        {
            _popup.PopupEntity(
                Loc.GetString("programmable-computer-linker-no-buffered-devices"),
                linkerUid,
                userUid,
                PopupType.Medium);
            return;
        }

        var added = 0;
        var skippedInvalid = 0;
        var skippedOutOfRange = 0;
        var skippedAlreadyLinked = 0;
        var skippedLimit = 0;
        var skippedOther = 0;

        foreach (var buffered in linker.BufferedDevices)
        {
            var target = GetEntity(buffered);
            if (!Exists(target) || !_programmable.IsAtmosLinkableDevice(target))
            {
                skippedInvalid++;
                continue;
            }

            if (!IsSameGridAndStation(computerUid, target))
            {
                skippedOutOfRange++;
                continue;
            }

            if (_programmable.TryAddAtmosLinkedDevice(computerUid, target, out _, out var reason))
                added++;
            else
            {
                switch (reason)
                {
                    case "already-linked":
                        skippedAlreadyLinked++;
                        break;
                    case "link-limit":
                        skippedLimit++;
                        break;
                    default:
                        skippedOther++;
                        break;
                }
            }
        }

        var skipped = skippedInvalid + skippedOutOfRange + skippedAlreadyLinked + skippedLimit + skippedOther;

        linker.BufferedDevices.Clear();
        UpdateUi(linkerUid, linker);
        _programmable.RefreshUi(computerUid);

        _popup.PopupEntity(
            Loc.GetString("programmable-computer-linker-paired-summary-detailed",
                ("added", added),
                ("skipped", skipped),
                ("already", skippedAlreadyLinked),
                ("range", skippedOutOfRange),
                ("invalid", skippedInvalid),
                ("limit", skippedLimit)),
            computerUid,
            userUid,
            PopupType.Medium);
    }

    private void OnExamined(EntityUid uid, ProgrammableComputerLinkerComponent comp, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("programmable-computer-linker-examine-buffered", ("count", comp.BufferedDevices.Count)));
    }

    private void OnUiOpened(EntityUid uid, ProgrammableComputerLinkerComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnClearAll(EntityUid uid, ProgrammableComputerLinkerComponent comp, ProgrammableComputerLinkerClearAllMessage args)
    {
        comp.BufferedDevices.Clear();
        UpdateUi(uid, comp);
    }

    private void OnRemoveBuffered(EntityUid uid, ProgrammableComputerLinkerComponent comp, ProgrammableComputerLinkerRemoveBufferedMessage args)
    {
        comp.BufferedDevices.Remove(args.Target);
        UpdateUi(uid, comp);
    }

    private void UpdateUi(EntityUid uid, ProgrammableComputerLinkerComponent comp)
    {
        var buffered = new List<ProgrammableComputerLinkerBufferedEntry>(comp.BufferedDevices.Count);
        foreach (var net in comp.BufferedDevices)
        {
            var ent = GetEntity(net);
            if (!Exists(ent) || !_programmable.IsAtmosLinkableDevice(ent))
                continue;

            buffered.Add(new ProgrammableComputerLinkerBufferedEntry(
                net,
                ToPrettyString(ent),
                _programmable.GetAtmosDeviceTypeForUi(ent),
                _programmable.GetAtmosAddressForUi(ent)));
        }

        _ui.SetUiState(uid, ProgrammableComputerLinkerUiKey.Key, new ProgrammableComputerLinkerUiState(buffered.ToArray()));
    }

    private bool IsSameGridAndStation(EntityUid computerUid, EntityUid deviceUid)
    {
        var computerGrid = Transform(computerUid).GridUid;
        var deviceGrid = Transform(deviceUid).GridUid;
        if (computerGrid == EntityUid.Invalid || deviceGrid == EntityUid.Invalid)
            return false;
        if (computerGrid != deviceGrid)
            return false;

        var computerStation = _station.GetOwningStation(computerUid);
        var deviceStation = _station.GetOwningStation(deviceUid);
        return computerStation == deviceStation;
    }
}
