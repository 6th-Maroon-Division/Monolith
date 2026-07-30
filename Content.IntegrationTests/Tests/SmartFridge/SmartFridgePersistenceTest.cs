using System.Linq;
using Content.Server.SmartFridge;
using Content.Shared.SmartFridge;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.SmartFridge;

[TestFixture]
public sealed class SmartFridgePersistenceTest
{
    [TestCase("SmartFridge", "FoodAmbrosiaVulgaris")]
    [TestCase("SmartArmoryStorage", "WeaponPistolMk58")]
    public async Task ContentsSurviveGridSaveLoad(string fridgePrototype, string itemPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var maps = server.ResolveDependency<IMapManager>();
        var mapSystem = entities.System<SharedMapSystem>();
        var mapLoader = entities.System<MapLoaderSystem>();
        var containers = entities.System<SharedContainerSystem>();
        var savePath = new ResPath($"/{nameof(SmartFridgePersistenceTest)}-{fridgePrototype}.yml");
        EntityUid restoredFridge = default;

        await server.WaitAssertion(() =>
        {
            mapSystem.CreateMap(out var sourceMap);
            var sourceGrid = maps.CreateGridEntity(sourceMap);
            mapSystem.SetTile(sourceGrid, Vector2i.Zero, new Tile(typeId: 1, flags: 1, variant: 255));

            var fridge = entities.SpawnEntity(fridgePrototype, new EntityCoordinates(sourceGrid, 0.5f, 0.5f));
            var item = entities.SpawnEntity(itemPrototype, new EntityCoordinates(sourceGrid, 0.5f, 0.5f));
            var fridgeComponent = entities.GetComponent<SmartFridgeComponent>(fridge);
            Assert.That(containers.TryGetContainer(fridge, fridgeComponent.Container, out var sourceContainer), Is.True);
            Assert.That(containers.Insert(item, sourceContainer!), Is.True);

            var saveOptions = SerializationOptions.Default with
            {
                Category = FileCategory.Grid,
                MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
                EntityExceptionBehaviour = EntityExceptionBehaviour.IgnoreEntity,
                ErrorOnOrphan = false,
                LogAutoInclude = null,
            };

            Assert.That(mapLoader.TrySaveGrid(sourceGrid, savePath, saveOptions), Is.True);
            mapSystem.DeleteMap(sourceMap);

            mapSystem.CreateMap(out var restoredMap);
            Assert.That(mapLoader.TryLoadGrid(restoredMap, savePath, out var restoredGrid), Is.True);
            Assert.That(restoredGrid, Is.Not.Null);

            var query = entities.EntityQueryEnumerator<SmartFridgeComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var transform))
            {
                if (transform.GridUid == restoredGrid.Value)
                {
                    restoredFridge = uid;
                    break;
                }
            }
            Assert.That(restoredFridge, Is.Not.EqualTo(EntityUid.Invalid));
        });

        // The container manager finishes restoring relationships after MapInit. Smart fridges
        // rebuild their network-entity index on the following task queue pass.
        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            entities.System<SmartFridgeSystem>().ResyncGrid(entities.GetComponent<TransformComponent>(restoredFridge).GridUid!.Value);
            var restoredComponent = entities.GetComponent<SmartFridgeComponent>(restoredFridge);
            Assert.That(containers.TryGetContainer(restoredFridge, restoredComponent.Container, out var restoredContainer), Is.True);
            Assert.That(restoredContainer!.ContainedEntities, Has.Count.EqualTo(1));
            Assert.That(restoredComponent.ContainedEntries.Values.Sum(entry => entry.Count), Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }
}
