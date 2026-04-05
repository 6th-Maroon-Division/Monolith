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
public sealed class ProgrammableComputerPowerLifecycleTests
{
    [Test]
    public async Task PowerLifecycle_BootAndShutdown_Works()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid computer = default;
        ProgrammableComputerSystem system = default!;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            system = entMan.System<ProgrammableComputerSystem>();
            ProgrammableComputerIntegrationTestHelper.InstallRequiredHardware(entMan, computer);
        });

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsPoweredOn"), Is.False);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsRunning"), Is.False);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(runtime), Does.Contain("Press Start to power on."));

            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.PoweredOn, Is.False);
            Assert.That(uiState.Booting, Is.False);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(uiState), Does.Contain("Press Start to power on."));
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        await server.WaitAssertion(() =>
        {
            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.PoweredOn, Is.True);
            Assert.That(uiState.Booting, Is.True);
        });

        server.RunTicks(320);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsPoweredOn"), Is.True);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsRunning"), Is.True);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(runtime), Does.Contain("Lua Computer v1.0"));

            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.PoweredOn, Is.True);
            Assert.That(uiState.Booting, Is.False);
            Assert.That(uiState.Width, Is.EqualTo(ProgrammableComputerComponent.TerminalWidth));
            Assert.That(uiState.Height, Is.EqualTo(ProgrammableComputerComponent.TerminalHeight));
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(uiState), Does.Contain("Lua Computer v1.0"));
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Shutdown));
        });

        server.RunTicks(10);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsPoweredOn"), Is.False);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsRunning"), Is.False);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(runtime), Does.Contain("System halted."));

            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.PoweredOn, Is.False);
            Assert.That(uiState.Booting, Is.False);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(uiState), Does.Contain("System halted."));
        });

        await pair.CleanReturnAsync();
    }
}