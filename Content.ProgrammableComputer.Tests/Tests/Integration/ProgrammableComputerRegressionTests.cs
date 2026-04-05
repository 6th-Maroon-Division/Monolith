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
public sealed class ProgrammableComputerRegressionTests
{
    [Test]
    public async Task Regressions_RefreshAndKeyboardInput_DoNotBreakRuntime()
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

            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerRefreshStateMessage());
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer,
                new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        server.RunTicks(260);

        await server.WaitAssertion(() =>
        {
            var packedKeyDown = 10 | (1 << 7);
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerKeyMessage(packedKeyDown));
        });

        server.RunTicks(15);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsRunning"), Is.True);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(runtime), Does.Not.Contain("keyboard listener error"));
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(runtime), Does.Not.Contain("touch listener error"));

            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.PoweredOn, Is.True);
            Assert.That(uiState.Booting, Is.False);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(uiState), Does.Not.Contain("keyboard listener error"));
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetTerminalText(uiState), Does.Not.Contain("touch listener error"));
        });

        await pair.CleanReturnAsync();
    }
}