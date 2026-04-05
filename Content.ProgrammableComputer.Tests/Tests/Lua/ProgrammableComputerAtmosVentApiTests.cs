using System.Collections.Generic;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerAtmosVentApiTests
{
    [Test]
    public void AtmosApi_VentControl_UpdateAndReadBackState()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        var calls = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        var ventEnabled = false;
        var ventMode = "release";
        var ventPressureChecks = "external";
        var ventExternalPressure = 101.325f;
        var ventInternalPressure = 0f;
        var ventLockoutOverride = false;

        ProgrammableComputerLuaTestHelper.BindAtmos(
            script,
            list: _ => DynValue.NewTable(script),
            pumpEnabled: (_, _) => DynValue.Void,
            pumpRead: _ => DynValue.NewTable(script),
            pumpRate: (_, _) => DynValue.Void,
            pumpPressure: (_, _) => DynValue.Void,
            vent: (label, parameters) =>
            {
                calls.Add($"vent:{label}");
                if (parameters.Get("enabled").Type != DataType.Nil)
                    ventEnabled = parameters.Get("enabled").CastToBool();
                if (parameters.Get("mode").Type == DataType.String)
                    ventMode = parameters.Get("mode").String;
                if (parameters.Get("pressure_checks").Type == DataType.String)
                    ventPressureChecks = parameters.Get("pressure_checks").String;
                if (parameters.Get("external_pressure").Type == DataType.Number)
                    ventExternalPressure = (float) parameters.Get("external_pressure").Number;
                if (parameters.Get("internal_pressure").Type == DataType.Number)
                    ventInternalPressure = (float) parameters.Get("internal_pressure").Number;
                if (parameters.Get("lockout_override").Type != DataType.Nil)
                    ventLockoutOverride = parameters.Get("lockout_override").CastToBool();
                return DynValue.Void;
            },
            ventRead: label =>
            {
                calls.Add($"vent_read:{label}");
                return DynValue.NewTable(new Table(script)
                {
                    ["enabled"] = ventEnabled,
                    ["type"] = "vent_pump",
                    ["mode"] = ventMode,
                    ["pressure_checks"] = ventPressureChecks,
                    ["external_pressure"] = ventExternalPressure,
                    ["internal_pressure"] = ventInternalPressure,
                    ["max_pressure"] = 4500f,
                    ["lockout_override"] = ventLockoutOverride,
                });
            });

        script.DoString(@"
atmos.vent('vent-1', {
  enabled = true,
  mode = 'siphon',
  pressure_checks = 'both',
  external_pressure = 90,
  internal_pressure = 120,
  lockout_override = true,
})

local vent = atmos.vent_read('vent-1')
assert(vent.enabled == true)
assert(vent.mode == 'siphon')
assert(vent.pressure_checks == 'both')
assert(math.abs(vent.external_pressure - 90) < 0.001)
assert(math.abs(vent.internal_pressure - 120) < 0.001)
assert(vent.lockout_override == true)
");

        Assert.That(output, Is.Empty);
        Assert.That(calls, Does.Contain("vent:vent-1"));
        Assert.That(calls, Does.Contain("vent_read:vent-1"));
    }
}