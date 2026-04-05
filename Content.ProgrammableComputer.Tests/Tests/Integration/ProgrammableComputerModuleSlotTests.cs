using System.Threading.Tasks;
using Content.IntegrationTests;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerModuleSlotTests
{
    [Test]
    public async Task ComputerVariants_AllSpawnable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var computerPrototypeIds = new[] { "ComputerProgrammable", "ComputerTabletopProgrammable", "ComputerWallmountProgrammable" };

        foreach (var protoId in computerPrototypeIds)
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(protoId), $"Prototype {protoId} not found");
                var computer = entMan.SpawnEntity(protoId, MapCoordinates.Nullspace);
                Assert.That(computer, Is.Not.EqualTo(EntityUid.Invalid), $"Failed to spawn {protoId}");
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllModuleVariants_Spawnable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var validModulePrototypes = new[]
        {
            "ProgrammableComputerCpuTier1", "ProgrammableComputerCpuTier2", "ProgrammableComputerCpuTier3",
            "ProgrammableComputerRamTier1", "ProgrammableComputerRamTier2", "ProgrammableComputerRamTier3",
            "ProgrammableComputerDiskTier1", "ProgrammableComputerDiskTier2", "ProgrammableComputerDiskTier3",
            "ProgrammableComputerNetworkTier1", "ProgrammableComputerNetworkTier2", "ProgrammableComputerNetworkTier3",
            "ProgrammableComputerGpuTier1", "ProgrammableComputerGpuTier2", "ProgrammableComputerGpuTier3",
            "ProgrammableComputerTimerModule"
        };

        foreach (var protoId in validModulePrototypes)
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(protoId), $"Prototype {protoId} not found");
                var moduleEnt = entMan.SpawnEntity(protoId, MapCoordinates.Nullspace);
                Assert.That(moduleEnt, Is.Not.EqualTo(EntityUid.Invalid), $"Failed to spawn module {protoId}");
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TimerModule_OnlyExistsInT1()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            // Timer module should exist (single tier only)
            Assert.That(protoMan.HasIndex<EntityPrototype>("ProgrammableComputerTimerModule"),
                "Timer module not found");

            // Tier variants should not exist
            Assert.That(!protoMan.HasIndex<EntityPrototype>("ProgrammableComputerTimerModuleTier2"),
                "Timer Tier2 should not exist");
            Assert.That(!protoMan.HasIndex<EntityPrototype>("ProgrammableComputerTimerModuleTier3"),
                "Timer Tier3 should not exist");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllModuleTiers_ExistAsExpected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var moduleTypes = new[] { "Cpu", "Ram", "Disk", "Network", "Gpu" };

        await server.WaitAssertion(() =>
        {
            foreach (var moduleType in moduleTypes)
            {
                // All module types should have Tier1
                Assert.That(protoMan.HasIndex<EntityPrototype>($"ProgrammableComputer{moduleType}Tier1"),
                    $"Missing Tier1 variant for {moduleType}");

                // All should have Tier2 and Tier3
                Assert.That(protoMan.HasIndex<EntityPrototype>($"ProgrammableComputer{moduleType}Tier2"),
                    $"Missing Tier2 variant for {moduleType}");
                Assert.That(protoMan.HasIndex<EntityPrototype>($"ProgrammableComputer{moduleType}Tier3"),
                    $"Missing Tier3 variant for {moduleType}");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CircuitboardAndLinker_SpawnableItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.HasIndex<EntityPrototype>("ProgrammableComputerCircuitboard"),
                "Circuitboard prototype not found");
            Assert.That(protoMan.HasIndex<EntityPrototype>("ProgrammableComputerLinker"),
                "Linker tool prototype not found");

            var circuitboard = entMan.SpawnEntity("ProgrammableComputerCircuitboard", MapCoordinates.Nullspace);
            var linker = entMan.SpawnEntity("ProgrammableComputerLinker", MapCoordinates.Nullspace);

            Assert.That(circuitboard, Is.Not.EqualTo(EntityUid.Invalid), "Failed to spawn circuitboard");
            Assert.That(linker, Is.Not.EqualTo(EntityUid.Invalid), "Failed to spawn linker");
        });

        await pair.CleanReturnAsync();
    }
}
