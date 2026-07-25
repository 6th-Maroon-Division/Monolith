using Content.Server.Gatherable.Components;

namespace Content.Server._Mono.Drill;

public sealed partial class ShipDrillSystem
{
    private EntityQuery<GatherableComponent> _gatherQuery;

    public void DrillGatherable(EntityUid drilled, EntityUid drill)
    {
        if (_gatherQuery.TryComp(drilled, out var gather))
        {
            _gather.Gather(drilled, drill, gather, true);
        }
    }
}
