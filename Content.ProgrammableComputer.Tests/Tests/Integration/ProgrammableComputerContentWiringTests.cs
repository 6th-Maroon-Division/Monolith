using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.Server._NF.Construction.Components;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.ProgrammableComputer;
using Content.Shared.Research.Prototypes;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerContentWiringTests
{
    [Test]
    public async Task ProgrammableBoard_HasAllComputerTargets_Wired()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid board = default;
        await server.WaitAssertion(() =>
        {
            board = entMan.SpawnEntity("ProgrammableComputerCircuitboard", MapCoordinates.Nullspace);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.TryGetComponent<ComputerBoardComponent>(board, out var computerBoard), Is.True);
                Assert.That(computerBoard!.Prototype, Is.EqualTo("ComputerProgrammable"));

                Assert.That(entMan.TryGetComponent<ComputerTabletopBoardComponent>(board, out var tabletopBoard), Is.True);
                Assert.That(tabletopBoard!.Prototype, Is.EqualTo("ComputerTabletopProgrammable"));

                Assert.That(entMan.TryGetComponent<ComputerWallmountBoardComponent>(board, out var wallmountBoard), Is.True);
                Assert.That(wallmountBoard!.Prototype, Is.EqualTo("ComputerWallmountProgrammable"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProgrammableVariants_HaveRuntimeAndBoardConfiguration()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        await server.WaitAssertion(() =>
        {
            AssertVariant(entMan, "ComputerProgrammable");
            AssertVariant(entMan, "ComputerTabletopProgrammable");
            AssertVariant(entMan, "ComputerWallmountProgrammable");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProgrammableRecipes_AreDefinedAndReachableFromPacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var recipeIds = new[]
            {
                "ProgrammableComputerCpuTier1",
                "ProgrammableComputerCpuTier2",
                "ProgrammableComputerCpuTier3",
                "ProgrammableComputerRamTier1",
                "ProgrammableComputerRamTier2",
                "ProgrammableComputerRamTier3",
                "ProgrammableComputerDiskTier1",
                "ProgrammableComputerDiskTier2",
                "ProgrammableComputerDiskTier3",
                "ProgrammableComputerNetworkTier1",
                "ProgrammableComputerNetworkTier2",
                "ProgrammableComputerNetworkTier3",
                "ProgrammableComputerGpuTier1",
                "ProgrammableComputerGpuTier2",
                "ProgrammableComputerGpuTier3",
                "ProgrammableComputerTimerModule",
                "ProgrammableComputerCircuitboard",
                "ProgrammableComputerLinker",
            };

            foreach (var recipeId in recipeIds)
            {
                Assert.That(protoMan.TryIndex<LatheRecipePrototype>(recipeId, out _), Is.True,
                    $"Missing lathe recipe prototype: {recipeId}");
            }

            AssertPackContains(protoMan, "ProgrammableComputerPartsStatic",
                "ProgrammableComputerCpuTier1",
                "ProgrammableComputerRamTier1",
                "ProgrammableComputerDiskTier1",
                "ProgrammableComputerNetworkTier1",
                "ProgrammableComputerGpuTier1",
                "ProgrammableComputerTimerModule");

            AssertPackContains(protoMan, "ProgrammableComputerParts",
                "ProgrammableComputerCpuTier2",
                "ProgrammableComputerRamTier2",
                "ProgrammableComputerDiskTier2",
                "ProgrammableComputerNetworkTier2",
                "ProgrammableComputerGpuTier2",
                "ProgrammableComputerCpuTier3",
                "ProgrammableComputerRamTier3",
                "ProgrammableComputerDiskTier3",
                "ProgrammableComputerNetworkTier3",
                "ProgrammableComputerGpuTier3");

            AssertPackContains(protoMan, "ToolsStatic", "ProgrammableComputerLinker");
            AssertPackContains(protoMan, "EngineeringBoards", "ProgrammableComputerCircuitboard");

            EntProtoId engineeringTechfabId = "EngineeringTechFab";
            var engineeringTechfab = protoMan.Index<EntityPrototype>(engineeringTechfabId);
            Assert.That(engineeringTechfab.TryGetComponent<LatheComponent>(out var lathe, compFactory), Is.True);
            ProtoId<LatheRecipePackPrototype> staticPack = "ProgrammableComputerPartsStatic";
            ProtoId<LatheRecipePackPrototype> dynamicPack = "ProgrammableComputerParts";
            Assert.That(lathe!.StaticPacks, Does.Contain(staticPack));
            Assert.That(lathe.DynamicPacks, Does.Contain(dynamicPack));
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertVariant(IServerEntityManager entMan, string prototype)
    {
        var computer = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);

        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<ProgrammableComputerComponent>(computer), Is.True,
                $"{prototype} should have ProgrammableComputerComponent.");

            Assert.That(entMan.TryGetComponent<ComputerComponent>(computer, out var computerComp), Is.True,
                $"{prototype} should have ComputerComponent.");

            Assert.That(computerComp!.BoardPrototype, Is.EqualTo("ProgrammableComputerCircuitboard"),
                $"{prototype} should deconstruct to ProgrammableComputerCircuitboard.");
        });
    }

    private static void AssertPackContains(IPrototypeManager protoMan, string packId, params string[] recipeIds)
    {
        var pack = protoMan.Index<LatheRecipePackPrototype>(packId);
        foreach (var recipeId in recipeIds)
        {
            ProtoId<LatheRecipePrototype> recipeProtoId = recipeId;
            Assert.That(pack.Recipes, Does.Contain(recipeProtoId),
                $"Expected recipe pack {packId} to contain recipe {recipeId}.");
        }
    }
}
