using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerScriptCompilationTests
{
    [Test]
    public void BootScript_Compiles_WithRuntimeModules()
    {
        var source = ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/boot/init.lua");
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();

        Assert.DoesNotThrow(() => script.LoadString(source, null, "@/boot/init.lua"));
    }

    [Test]
    public void PumpTestScript_Compiles_WithRuntimeModules()
    {
        var source = ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/bin/pumptest.lua");
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();

        Assert.DoesNotThrow(() => script.LoadString(source, null, "@/bin/pumptest.lua"));
    }
}