using System.Collections.Generic;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerAtmosInjectorApiTests
{
    [Test]
    public void AtmosApi_InjectorControl_UpdateAndReadBackState()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        var calls = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        var injectorEnabled = false;
        var injectorRate = 50f;

        ProgrammableComputerLuaTestHelper.BindAtmos(
            script,
            list: _ => DynValue.NewTable(script),
            pumpEnabled: (_, _) => DynValue.Void,
            pumpRead: _ => DynValue.NewTable(script),
            pumpRate: (_, _) => DynValue.Void,
            pumpPressure: (_, _) => DynValue.Void,
            injector: (label, parameters) =>
            {
                calls.Add($"injector:{label}");
                if (parameters.Get("enabled").Type != DataType.Nil)
                    injectorEnabled = parameters.Get("enabled").CastToBool();
                if (parameters.Get("rate").Type == DataType.Number)
                    injectorRate = (float) parameters.Get("rate").Number;
                return DynValue.Void;
            },
            injectorRead: label =>
            {
                calls.Add($"injector_read:{label}");
                return DynValue.NewTable(new Table(script)
                {
                    ["enabled"] = injectorEnabled,
                    ["type"] = "outlet_injector",
                    ["rate"] = injectorRate,
                    ["max_rate"] = 500f,
                    ["max_pressure"] = 4500f,
                });
            });

        script.DoString(@"
atmos.injector('injector-1', { enabled = true, rate = 250 })
local injector = atmos.injector_read('injector-1')
assert(injector.enabled == true)
assert(math.abs(injector.rate - 250) < 0.001)
");

        Assert.That(output, Is.Empty);
        Assert.That(calls, Does.Contain("injector:injector-1"));
        Assert.That(calls, Does.Contain("injector_read:injector-1"));
    }
}