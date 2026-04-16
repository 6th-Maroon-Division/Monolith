using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Shuttle;

[TestFixture]
public sealed class PersistenceRestoredWallVisualsTest
{
    [Test]
    public async Task RestoredWallsHaveNonBlankClientSprite()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        var mapManager = server.ResolveDependency<IMapManager>();
        var serverEntManager = server.ResolveDependency<IEntityManager>();
        var clientEntManager = client.ResolveDependency<IEntityManager>();
        var mapLoader = serverEntManager.System<MapLoaderSystem>();
        var mapSystem = serverEntManager.System<SharedMapSystem>();

        var savePath = new ResPath("/restored-wall-visuals-test.yml");
        EntityUid restoredWall = default;

        await server.WaitAssertion(() =>
        {
            mapSystem.CreateMap(out var sourceMap);
            var sourceGrid = mapManager.CreateGridEntity(sourceMap);

            mapSystem.SetTile(sourceGrid, Vector2i.Zero, new Tile(typeId: 1, flags: 1, variant: 255));
            mapSystem.SetTile(sourceGrid, new Vector2i(1, 0), new Tile(typeId: 1, flags: 1, variant: 255));

            serverEntManager.SpawnEntity("WallReinforced", new EntityCoordinates(sourceGrid.Owner, 0.5f, 0.5f));
            serverEntManager.SpawnEntity("WallReinforced", new EntityCoordinates(sourceGrid.Owner, 1.5f, 0.5f));

            Assert.That(mapLoader.TrySaveGrid(sourceGrid.Owner, savePath), Is.True);
            mapSystem.DeleteMap(sourceMap);

            mapSystem.CreateMap(out var restoredMap);
            Assert.That(mapLoader.TryLoadGrid(restoredMap, savePath, out var restoredGrid), Is.True);
            Assert.That(restoredGrid, Is.Not.Null);

            var wallQuery = serverEntManager.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (wallQuery.MoveNext(out var uid, out var meta, out var xform))
            {
                if (xform.GridUid != restoredGrid!.Value.Owner)
                    continue;

                if (meta.EntityPrototype?.ID != "WallReinforced")
                    continue;

                restoredWall = uid;
                break;
            }

            Assert.That(restoredWall, Is.Not.EqualTo(EntityUid.Invalid));
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var net = serverEntManager.GetNetEntity(restoredWall);
            var clientWall = clientEntManager.GetEntity(net);

            Assert.That(clientEntManager.TryGetComponent(clientWall, out SpriteComponent sprite), Is.True);
            Assert.That(sprite!.TryGetLayer(0, out var layer), Is.True);
            Assert.That(layer!.Blank, Is.False,
                "Restored wall sprite is blank on client after save/load.");
        });

        await pair.CleanReturnAsync();
    }
}