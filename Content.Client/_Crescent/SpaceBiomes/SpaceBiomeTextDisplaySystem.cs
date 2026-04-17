using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.Prototypes;
using Content.Client.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Content.Shared._Crescent.Vessel;
using System;

namespace Content.Client._Crescent.SpaceBiomes;

public sealed partial class SpaceTextDisplaySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protMan = default!;
    [Dependency] private IOverlayManager _overMan = default!;
    [Dependency] private ContentAudioSystem _audioSys = default!;

    private SpaceBiomeTextOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceBiomeSwapMessage>(OnSwap);
        SubscribeLocalEvent<PlayerParentChangedMessage>(OnNewVesselEntered);
        _overlay = new();
        _overMan.AddOverlay(_overlay);
    }

    private void OnSwap(ref SpaceBiomeSwapMessage ev)
    {
        _audioSys.DisableAmbientMusic();
        SpaceBiomePrototype biome = _protMan.Index<SpaceBiomePrototype>(ev.Id);
        _overlay.Reset();
        _overlay.ResetDescription();
        _overlay.Text = biome.Name;
        _overlay.TextDescription = biome.Description;
        _overlay.CharInterval = CalculateCharInterval(_overlay.Text);
        _overlay.CharIntervalDescription = CalculateCharInterval(_overlay.TextDescription);
    }

    private void OnNewVesselEntered(ref PlayerParentChangedMessage ev)
    {
        if (ev.Grid == null) //player walked into space so we dont care
            return;

        var name = MetaData((EntityUid)ev.Grid).EntityName; //this should never be null. i hope
        var description = ""; //fallback for description is nothin'
        if (TryComp<VesselInfoComponent>((EntityUid)ev.Grid, out var vesselinfo))
            description = vesselinfo.Description;


        _overlay.Reset();             //these should be reset as well to match OnSwap
        _overlay.ResetDescription();

        if (_overlay.Text != null)
            return;

        if (name.Length == 0)
            return;

        _overlay.Text = name;
        _overlay.TextDescription = description; // fallback is "" if no description is found.
        _overlay.CharInterval = CalculateCharInterval(_overlay.Text);
        _overlay.CharIntervalDescription = CalculateCharInterval(_overlay.TextDescription);
    }

    private static TimeSpan CalculateCharInterval(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return TimeSpan.Zero;

        var seconds = 2f / text.Length;
        if (!float.IsFinite(seconds) || seconds <= 0f)
            return TimeSpan.Zero;

        return TimeSpan.FromSeconds(seconds);
    }
}
