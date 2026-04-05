using System.Collections.Generic;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerAtmosRegulatorApiTests
{
    [Test]
    public void AtmosApi_RegulatorControl_UpdateAndReadBackState()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        var calls = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        var regulatorThreshold = 200f;

        ProgrammableComputerLuaTestHelper.BindAtmos(
            script,
            list: _ => DynValue.NewTable(script),
            pumpEnabled: (_, _) => DynValue.Void,
            pumpRead: _ => DynValue.NewTable(script),
            pumpRate: (_, _) => DynValue.Void,
            pumpPressure: (_, _) => DynValue.Void,
            regulator: (label, parameters) =>
            {
                calls.Add($"regulator:{label}");
                if (parameters.Get("threshold").Type == DataType.Number)
                    regulatorThreshold = (float) parameters.Get("threshold").Number;
                return DynValue.Void;
            },
            regulatorRead: label =>
            {
                calls.Add($"regulator_read:{label}");
                return DynValue.NewTable(new Table(script)
                {
                    ["type"] = "pressure_regulator",
                    ["threshold"] = regulatorThreshold,
                    ["max_transfer_rate"] = 200f,
                    ["enabled"] = true,
                    ["flow_rate"] = 5f,
                    ["inlet_pressure"] = 500f,
                    ["outlet_pressure"] = 100f,
                });
            });

        script.DoString(@"
atmos.regulator('reg-1', { threshold = 444 })
local regulator = atmos.regulator_read('reg-1')
assert(math.abs(regulator.threshold - 444) < 0.001)
");

        Assert.That(output, Is.Empty);
        Assert.That(calls, Does.Contain("regulator:reg-1"));
        Assert.That(calls, Does.Contain("regulator_read:reg-1"));
    }
}