using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerRuntimeBootstrapTests
{
    [Test]
    public void RuntimeBootstrap_ContainsCriticalWrappers()
    {
        var source = ProgrammableComputerLuaTestHelper.ReadRepoFile("Content.Server/ProgrammableComputer/ProgrammableComputerSystem.cs");

        Assert.That(source, Does.Contain("function os.sleep(seconds)"));
        Assert.That(source, Does.Contain("function atmos.pump_read(label)"));
        Assert.That(source, Does.Contain("function atmos.filter(label, params)"));
        Assert.That(source, Does.Contain("function atmos.filter_read(label)"));
        Assert.That(source, Does.Contain("function atmos.vent(label, params)"));
        Assert.That(source, Does.Contain("function atmos.vent_read(label)"));
        Assert.That(source, Does.Contain("function atmos.injector(label, params)"));
        Assert.That(source, Does.Contain("function atmos.injector_read(label)"));
        Assert.That(source, Does.Contain("function atmos.mixer(label, params)"));
        Assert.That(source, Does.Contain("function atmos.mixer_read(label)"));
        Assert.That(source, Does.Contain("function atmos.regulator(label, params)"));
        Assert.That(source, Does.Contain("function atmos.regulator_read(label)"));
    }

    [Test]
    public void RuntimeUsesExpectedCoreModules()
    {
        var source = ProgrammableComputerLuaTestHelper.ReadRepoFile("Content.Server/ProgrammableComputer/ProgrammableComputerSystem.cs");

        Assert.That(source, Does.Contain("CoreModules.Coroutine"));
        Assert.That(source, Does.Contain("CoreModules.ErrorHandling"));
        Assert.That(source, Does.Contain("CoreModules.LoadMethods"));
    }

    [Test]
    public void CraftRecipes_AllModuleTiers_Defined()
    {
        var recipeSource = ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/Prototypes/_Mono/Recipes/Lathes/programmable_computer_parts.yml");

        // Validate all module tiers and types are defined
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerCpuTier1"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerCpuTier2"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerCpuTier3"));

        Assert.That(recipeSource, Does.Contain("ProgrammableComputerRamTier1"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerRamTier2"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerRamTier3"));

        Assert.That(recipeSource, Does.Contain("ProgrammableComputerDiskTier1"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerDiskTier2"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerDiskTier3"));

        Assert.That(recipeSource, Does.Contain("ProgrammableComputerNetworkTier1"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerNetworkTier2"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerNetworkTier3"));

        Assert.That(recipeSource, Does.Contain("ProgrammableComputerGpuTier1"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerGpuTier2"));
        Assert.That(recipeSource, Does.Contain("ProgrammableComputerGpuTier3"));

        Assert.That(recipeSource, Does.Contain("ProgrammableComputerTimerModule"));
    }

    [Test]
    public void CraftRecipes_AreProperlyCategorizedInPacks()
    {
        var engiPackSource = ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/Prototypes/_Mono/Recipes/Lathes/Packs/engi.yml");

        // Validate T1 pack (static) contains basic modules
        Assert.That(engiPackSource, Does.Contain("id: ProgrammableComputerPartsStatic"));
        Assert.That(engiPackSource, Does.Contain("ProgrammableComputerCpuTier1"));
        Assert.That(engiPackSource, Does.Contain("ProgrammableComputerTimerModule"));

        // Validate T2/T3 pack (dynamic) contains advanced modules
        Assert.That(engiPackSource, Does.Contain("id: ProgrammableComputerParts"));
        Assert.That(engiPackSource, Does.Contain("ProgrammableComputerCpuTier2"));
        Assert.That(engiPackSource, Does.Contain("ProgrammableComputerCpuTier3"));
    }

    [Test]
    public void CircuitboardAndLinkerRecipes_Defined()
    {
        var electronicsSource = ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/Prototypes/Recipes/Lathes/electronics.yml");
        var toolsSource = ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/Prototypes/Recipes/Lathes/tools.yml");

        Assert.That(electronicsSource, Does.Contain("ProgrammableComputerCircuitboard"));
        Assert.That(toolsSource, Does.Contain("ProgrammableComputerLinker"));
    }
}