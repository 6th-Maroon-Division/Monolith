using System.Collections.Generic;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerAtmosMixerApiTests
{
    [Test]
    public void AtmosApi_MixerControl_UpdateAndReadBackState()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        var calls = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        var mixerEnabled = true;
        var mixerTargetPressure = 101.325f;
        var mixerInletOne = 0.5f;
        var mixerInletTwo = 0.5f;

        ProgrammableComputerLuaTestHelper.BindAtmos(
            script,
            list: _ => DynValue.NewTable(script),
            pumpEnabled: (_, _) => DynValue.Void,
            pumpRead: _ => DynValue.NewTable(script),
            pumpRate: (_, _) => DynValue.Void,
            pumpPressure: (_, _) => DynValue.Void,
            mixer: (label, parameters) =>
            {
                calls.Add($"mixer:{label}");
                if (parameters.Get("enabled").Type != DataType.Nil)
                    mixerEnabled = parameters.Get("enabled").CastToBool();
                if (parameters.Get("target_pressure").Type == DataType.Number)
                    mixerTargetPressure = (float) parameters.Get("target_pressure").Number;
                if (parameters.Get("inlet_one").Type == DataType.Number)
                    mixerInletOne = (float) parameters.Get("inlet_one").Number;
                if (parameters.Get("inlet_two").Type == DataType.Number)
                    mixerInletTwo = (float) parameters.Get("inlet_two").Number;
                return DynValue.Void;
            },
            mixerRead: label =>
            {
                calls.Add($"mixer_read:{label}");
                return DynValue.NewTable(new Table(script)
                {
                    ["enabled"] = mixerEnabled,
                    ["type"] = "mixer",
                    ["target_pressure"] = mixerTargetPressure,
                    ["max_target_pressure"] = 4500f,
                    ["inlet_one"] = mixerInletOne,
                    ["inlet_two"] = mixerInletTwo,
                });
            });

        script.DoString(@"
atmos.mixer('mixer-1', { enabled = false, target_pressure = 333, inlet_one = 0.25, inlet_two = 0.75 })
local mixer = atmos.mixer_read('mixer-1')
assert(mixer.enabled == false)
assert(math.abs(mixer.target_pressure - 333) < 0.001)
assert(math.abs(mixer.inlet_one - 0.25) < 0.001)
assert(math.abs(mixer.inlet_two - 0.75) < 0.001)
");

        Assert.That(output, Is.Empty);
        Assert.That(calls, Does.Contain("mixer:mixer-1"));
        Assert.That(calls, Does.Contain("mixer_read:mixer-1"));
    }
}