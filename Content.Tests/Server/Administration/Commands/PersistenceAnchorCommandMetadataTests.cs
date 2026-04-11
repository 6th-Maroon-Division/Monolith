using Content.Server.Administration.Commands;
using NUnit.Framework;
using Robust.UnitTesting;

namespace Content.Tests.Server.Administration.Commands;

[TestFixture]
public sealed class PersistenceAnchorCommandMetadataTests : RobustUnitTest
{
    [Test]
    public void PersistenceAnchorSaveCommandMetadataIsStable()
    {
        var command = new PersistenceAnchorSaveCommand();

        Assert.That(command.Command, Is.EqualTo("persistenceanchorsave"));
        Assert.That(command.Help, Does.Contain("<netEntityUid|all>"));
        Assert.That(command.Description, Does.Contain("Force-saves persistence anchor state"));
    }
}
