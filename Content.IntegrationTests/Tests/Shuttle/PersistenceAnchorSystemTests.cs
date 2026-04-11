using System.Collections.Generic;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Shuttle;

[TestFixture]
[TestOf(typeof(PersistenceAnchorSystem))]
public sealed class PersistenceAnchorSystemTests
{
    [Test]
    public async Task ForceSaveAnchorWritesSnapshot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var persistence = entMan.System<PersistenceAnchorSystem>();
            var userData = server.ResolveDependency<IResourceManager>().UserData;

            mapSystem.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            entMan.EnsureComponent<ShuttleComponent>(grid.Owner);

            var anchor = entMan.SpawnEntity("PersistenceAnchor", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            Assert.That(persistence.ForceSaveAnchor(anchor, out var reason), Is.True, reason);

            var anchorComp = entMan.GetComponent<PersistenceAnchorComponent>(anchor);
            Assert.That(string.IsNullOrWhiteSpace(anchorComp.AnchorId), Is.False);

            var snapshotPath = new ResPath($"/PersistenceAnchors/Active/{anchorComp.AnchorId}.yml");
            Assert.That(userData.Exists(snapshotPath), Is.True, $"Expected snapshot file at {snapshotPath}");

            var report = persistence.GetStatusReport();
            Assert.That(report.Contains(anchorComp.AnchorId!, StringComparison.Ordinal), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoveTrackingArchivesSavedSnapshot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var persistence = entMan.System<PersistenceAnchorSystem>();
            var userData = server.ResolveDependency<IResourceManager>().UserData;

            mapSystem.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            entMan.EnsureComponent<ShuttleComponent>(grid.Owner);

            var anchor = entMan.SpawnEntity("PersistenceAnchor", new EntityCoordinates(grid.Owner, 1.5f, 0.5f));
            Assert.That(persistence.ForceSaveAnchor(anchor, out var saveReason), Is.True, saveReason);

            var anchorComp = entMan.GetComponent<PersistenceAnchorComponent>(anchor);
            Assert.That(anchorComp.AnchorId, Is.Not.Null.And.Not.Empty);

            var activePath = new ResPath($"/PersistenceAnchors/Active/{anchorComp.AnchorId}.yml");
            var archivePath = new ResPath($"/PersistenceAnchors/Archive/{anchorComp.AnchorId}.yml");
            Assert.That(userData.Exists(activePath), Is.True, $"Expected active snapshot at {activePath}");

            Assert.That(persistence.RemoveTracking(anchor, out var removeReason), Is.True, removeReason);
            Assert.That(userData.Exists(activePath), Is.False, "Active snapshot should be moved to archive after removal");
            Assert.That(userData.Exists(archivePath), Is.True, $"Expected archived snapshot at {archivePath}");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForceSaveAllAnchorsWritesSnapshotForEach()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var persistence = entMan.System<PersistenceAnchorSystem>();
            var userData = server.ResolveDependency<IResourceManager>().UserData;

            // Spawn three anchors on separate grids.
            mapSystem.CreateMap(out var mapId);
            var anchors = new List<EntityUid>();
            for (var i = 0; i < 3; i++)
            {
                var grid = mapManager.CreateGridEntity(mapId);
                entMan.EnsureComponent<ShuttleComponent>(grid.Owner);
                var anchor = entMan.SpawnEntity("PersistenceAnchor", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
                anchors.Add(anchor);
            }

            var savedCount = persistence.ForceSaveAllAnchors();

            Assert.That(savedCount, Is.GreaterThanOrEqualTo(anchors.Count),
                $"Expected at least {anchors.Count} anchors saved, got {savedCount}");

            foreach (var uid in anchors)
            {
                var anchorComp = entMan.GetComponent<PersistenceAnchorComponent>(uid);
                Assert.That(string.IsNullOrWhiteSpace(anchorComp.AnchorId), Is.False,
                    "Anchor should have been assigned an ID by ForceSaveAllAnchors");

                var snapshotPath = new ResPath($"/PersistenceAnchors/Active/{anchorComp.AnchorId}.yml");
                Assert.That(userData.Exists(snapshotPath), Is.True,
                    $"Expected snapshot at {snapshotPath} for anchor {uid}");
            }
        });

        await pair.CleanReturnAsync();
    }
}
