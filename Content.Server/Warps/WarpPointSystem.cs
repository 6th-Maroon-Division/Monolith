using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Server.Station.Systems; // Frontier

namespace Content.Server.Warps;

public sealed class WarpPointSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!; // Frontier
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarpPointComponent, ExaminedEvent>(OnWarpPointExamine);
        SubscribeLocalEvent<WarpPointComponent, ComponentStartup>(OnStartup); // Frontier
    }

    private void OnWarpPointExamine(EntityUid uid, WarpPointComponent component, ExaminedEvent args)
    {
        if (!HasComp<GhostComponent>(args.Examiner))
            return;

        var loc = component.Location == null ? "<null>" : $"'{component.Location}'";
        args.PushText(Loc.GetString("warp-point-component-on-examine-success", ("location", loc)));
    }

    // Frontier
    private void OnStartup(EntityUid uid, WarpPointComponent component, ComponentStartup args)
    {
        if (!string.IsNullOrWhiteSpace(component.Location))
            return;

        if ((component.QueryStationName || component.UseStationName)
            && _station.GetOwningStation(uid) is { Valid: true } station
            && TryComp(station, out MetaDataComponent? stationMetadata))
        {
            component.Location = stationMetadata.EntityName;
        }
        else if ((component.QueryGridName || component.UseStationName)
            && TryComp(uid, out TransformComponent? xform)
            && xform.GridUid is { Valid: true } grid
            && TryComp(grid, out MetaDataComponent? gridMetadata))
        {
            component.Location = gridMetadata.EntityName;
        }
    }
    // End Frontier
}
