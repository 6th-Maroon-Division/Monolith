using Content.Shared.SmartFridge;

namespace Content.Server.SmartFridge;

public sealed class SmartFridgeSystem : SharedSmartFridgeSystem
{
    public void ResyncGrid(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<SmartFridgeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var transform))
        {
            if (transform.GridUid != gridUid)
                continue;

            RebuildContainedEntries((uid, component));
        }
    }
}
