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
public sealed class ProgrammableComputerAtmosLinkTests
{
    [Test]
    public async Task AtmosLinks_AddRenameUnlink_Works()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        EntityUid computer = default;
        EntityUid filter = default;
        ProgrammableComputerSystem system = default!;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            filter = entMan.SpawnEntity("GasFilter", MapCoordinates.Nullspace);
            system = entMan.System<ProgrammableComputerSystem>();
        });

        string label = string.Empty;
        await server.WaitAssertion(() =>
        {
            Assert.That(system.TryAddAtmosLinkedDevice(computer, filter, out label), Is.True);
            Assert.That(label, Is.Not.Empty);

            system.RefreshUi(computer);

            var links = entMan.EnsureComponent<ProgrammableComputerAtmosLinksComponent>(computer);
            Assert.That(links.Links.Count, Is.EqualTo(1));
            Assert.That(links.Links.ContainsKey(label), Is.True);

            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.AtmosLinks.Length, Is.EqualTo(1));
            Assert.That(uiState.AtmosLinks[0].Label, Is.EqualTo(label));
            Assert.That(uiState.AtmosLinks[0].DeviceType, Is.EqualTo("filter"));
        });

        const string renamed = "filter-main";
        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerAtmosRenameMessage(label, renamed));
            var links = entMan.EnsureComponent<ProgrammableComputerAtmosLinksComponent>(computer);
            Assert.That(links.Links.ContainsKey(renamed), Is.True);
            Assert.That(links.Links.ContainsKey(label), Is.False);

            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.AtmosLinks.Length, Is.EqualTo(1));
            Assert.That(uiState.AtmosLinks[0].Label, Is.EqualTo(renamed));
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerAtmosUnlinkMessage(renamed));
            var links = entMan.EnsureComponent<ProgrammableComputerAtmosLinksComponent>(computer);
            Assert.That(links.Links, Is.Empty);

            var uiState = ProgrammableComputerIntegrationTestHelper.GetUiState(entMan, computer);
            Assert.That(uiState.AtmosLinks, Is.Empty);
        });

        await pair.CleanReturnAsync();
    }
}