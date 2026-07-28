using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Gateway;
using Content.Shared.Teleportation.Components;

namespace Content.IntegrationTests.Tests.Gateway;

public sealed class GatewayInteractionTest : InteractionTest
{
    [Test]
    public async Task OpeningGatewayLinksBothDirections()
    {
        await SpawnTarget("GatewayNode");
        var source = SEntMan.GetEntity(Target!.Value);
        var destination = await SpawnEntity("GatewayCentral", SEntMan.GetCoordinates(TargetCoords));

        await Activate();
        Assert.That(IsUiOpen(GatewayUiKey.Key));

        await SendBui(GatewayUiKey.Key,
            new GatewayOpenPortalMessage(SEntMan.GetNetEntity(destination)));

        var sourceLinks = SEntMan.GetComponent<LinkedEntityComponent>(source);
        var destinationLinks = SEntMan.GetComponent<LinkedEntityComponent>(destination);

        Assert.That(sourceLinks.LinkedEntities, Does.Contain(destination));
        Assert.That(destinationLinks.LinkedEntities, Does.Contain(source));
        Assert.That(SEntMan.HasComponent<PortalComponent>(source), Is.True);
        Assert.That(SEntMan.HasComponent<PortalComponent>(destination), Is.True);
    }
}
