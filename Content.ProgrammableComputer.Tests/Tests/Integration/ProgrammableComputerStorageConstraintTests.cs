using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.Server.ProgrammableComputer;
using Content.Shared.ProgrammableComputer;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerStorageConstraintTests
{
    [Test]
    public async Task T1Configuration_HasBasicStorage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid computer = default;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            var cpuT1 = entMan.SpawnEntity("ProgrammableComputerCpuTier1", MapCoordinates.Nullspace);
            var ramT1 = entMan.SpawnEntity("ProgrammableComputerRamTier1", MapCoordinates.Nullspace);
            var diskT1 = entMan.SpawnEntity("ProgrammableComputerDiskTier1", MapCoordinates.Nullspace);

            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, cpuT1, ProgrammableComputerComponent.CpuSlotName);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, ramT1, ProgrammableComputerComponent.RamSlotOneName);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, diskT1, ProgrammableComputerComponent.DiskSlotOneName);

            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        server.RunTicks(360);

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
            var state = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);

            Assert.That(state.RamAvailableKiB, Is.GreaterThan(0), "T1 RAM should be available");
            Assert.That(state.DiskAvailableKiB, Is.GreaterThan(0), "T1 Disk should be available");
            Assert.That(state.RamAvailableKiB, Is.LessThanOrEqualTo(256), "T1 RAM should be <= 256KB");
            Assert.That(state.DiskAvailableKiB, Is.LessThanOrEqualTo(512), "T1 Disk should be <= 512KB");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task T2Configuration_HasEnhancedStorage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid computer = default;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            var cpuT2 = entMan.SpawnEntity("ProgrammableComputerCpuTier2", MapCoordinates.Nullspace);
            var ramT2 = entMan.SpawnEntity("ProgrammableComputerRamTier2", MapCoordinates.Nullspace);
            var diskT2 = entMan.SpawnEntity("ProgrammableComputerDiskTier2", MapCoordinates.Nullspace);

            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, cpuT2, ProgrammableComputerComponent.CpuSlotName);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, ramT2, ProgrammableComputerComponent.RamSlotOneName);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, diskT2, ProgrammableComputerComponent.DiskSlotOneName);

            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        server.RunTicks(360);

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
            var state = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);

            Assert.That(state.RamAvailableKiB, Is.GreaterThan(256), "T2 RAM should exceed T1");
            Assert.That(state.DiskAvailableKiB, Is.GreaterThan(512), "T2 Disk should exceed T1");
            Assert.That(state.RamAvailableKiB, Is.LessThanOrEqualTo(1024), "T2 RAM should be <= 1024KB");
            Assert.That(state.DiskAvailableKiB, Is.LessThanOrEqualTo(2048), "T2 Disk should be <= 2048KB");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task T3Configuration_HasMaxStorage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid computer = default;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            var cpuT3 = entMan.SpawnEntity("ProgrammableComputerCpuTier3", MapCoordinates.Nullspace);
            var ramT3 = entMan.SpawnEntity("ProgrammableComputerRamTier3", MapCoordinates.Nullspace);
            var diskT3 = entMan.SpawnEntity("ProgrammableComputerDiskTier3", MapCoordinates.Nullspace);

            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, cpuT3, ProgrammableComputerComponent.CpuSlotName);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, ramT3, ProgrammableComputerComponent.RamSlotOneName);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, diskT3, ProgrammableComputerComponent.DiskSlotOneName);

            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        server.RunTicks(360);

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
            var state = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);

            Assert.That(state.RamAvailableKiB, Is.GreaterThan(1024), "T3 RAM should exceed T2");
            Assert.That(state.DiskAvailableKiB, Is.GreaterThan(2048), "T3 Disk should exceed T2");
            Assert.That(state.RamAvailableKiB, Is.LessThanOrEqualTo(4096), "T3 RAM should be <= 4096KB");
            Assert.That(state.DiskAvailableKiB, Is.LessThanOrEqualTo(8192), "T3 Disk should be <= 8192KB");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultipleRamSlots_IncreaseTotalStorage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid computer = default;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            ProgrammableComputerIntegrationTestHelper.InstallRequiredHardware(entMan, computer);
            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        server.RunTicks(360);

        var ramWithOneSlot = 0;
        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
            var state = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            ramWithOneSlot = state.RamAvailableKiB;
        });

        await server.WaitAssertion(() =>
        {
            // Add second RAM module to second slot
            var ram2 = entMan.SpawnEntity("ProgrammableComputerRamTier1", MapCoordinates.Nullspace);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, ram2, ProgrammableComputerComponent.RamSlotTwoName);
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
        });

        await server.WaitAssertion(() =>
        {
            var state = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(state.RamAvailableKiB, Is.GreaterThan(ramWithOneSlot), 
                "Second RAM slot should increase total RAM capacity");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultipleDiskSlots_IncreaseTotalStorage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid computer = default;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            ProgrammableComputerIntegrationTestHelper.InstallRequiredHardware(entMan, computer);
            var disk1 = entMan.SpawnEntity("ProgrammableComputerDiskTier1", MapCoordinates.Nullspace);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, disk1, ProgrammableComputerComponent.DiskSlotOneName);
            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        server.RunTicks(360);

        var diskWithOneSlot = 0;
        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
            var state = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            diskWithOneSlot = state.DiskAvailableKiB;
        });

        await server.WaitAssertion(() =>
        {
            // Add second Disk module to second slot
            var disk2 = entMan.SpawnEntity("ProgrammableComputerDiskTier1", MapCoordinates.Nullspace);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, disk2, ProgrammableComputerComponent.DiskSlotTwoName);
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
        });

        await server.WaitAssertion(() =>
        {
            var state = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(state.DiskAvailableKiB, Is.GreaterThan(diskWithOneSlot), 
                "Second Disk slot should increase total storage capacity");
        });

        await pair.CleanReturnAsync();
    }
}
