using System.Collections.Generic;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerPumpScriptTests
{
    [Test]
    public void PumpTest_NoDevices_PrintsGuidanceAndExits()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();

        ProgrammableComputerLuaTestHelper.BindTerm(script, output);
        ProgrammableComputerLuaTestHelper.BindAtmos(script,
            list: _ => DynValue.NewTable(script),
            pumpEnabled: (_, _) => DynValue.Void,
            pumpRead: _ => DynValue.NewTable(script),
            pumpRate: (_, _) => DynValue.Void,
            pumpPressure: (_, _) => DynValue.Void);

        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/bin/pumptest.lua"));

        Assert.That(output, Has.Some.Contains("No linked atmos devices found."));
    }

    [Test]
    public void PumpTest_OneVolumePump_RunsFullSequence()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        var calls = new List<string>();
        var sleepCalls = 0;

        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        var devices = new Table(script)
        {
            [1] = DynValue.NewTable(new Table(script)
            {
                ["label"] = "vp-1",
                ["type"] = "volume_pump",
                ["address"] = "A1"
            })
        };

        var currentRate = 0f;
        var enabled = false;
        const float maxRate = 500f;

        ProgrammableComputerLuaTestHelper.BindAtmos(script,
            list: _ => DynValue.NewTable(devices),
            pumpEnabled: (label, on) =>
            {
                calls.Add($"enabled:{label}:{on}");
                enabled = on;
                return DynValue.Void;
            },
            pumpRead: label =>
            {
                calls.Add($"read:{label}");
                var state = new Table(script)
                {
                    ["enabled"] = enabled,
                    ["type"] = "volume_pump",
                    ["transfer_rate"] = currentRate,
                    ["max_transfer_rate"] = maxRate,
                    ["overclocked"] = false
                };
                return DynValue.NewTable(state);
            },
            pumpRate: (label, rate) =>
            {
                calls.Add($"rate:{label}:{rate:0.###}");
                currentRate = rate;
                return DynValue.Void;
            },
            pumpPressure: (_, _) => DynValue.Void);

        var osTable = new Table(script);
        osTable["sleep"] = DynValue.NewCallback((_, _) =>
        {
            sleepCalls++;
            return DynValue.Void;
        });
        script.Globals["os"] = osTable;

        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/bin/pumptest.lua"));

        Assert.That(calls, Does.Contain("enabled:vp-1:True"));
        Assert.That(calls, Does.Contain("rate:vp-1:500"));
        Assert.That(calls, Does.Contain("rate:vp-1:250"));
        Assert.That(calls, Does.Contain("enabled:vp-1:False"));
        Assert.That(sleepCalls, Is.EqualTo(3));
        Assert.That(output, Has.Some.Contains("=== Test completed ==="));
    }

    [Test]
    public void PumpTest_OnePressurePump_RunsPressureBranch()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        var calls = new List<string>();

        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        var devices = new Table(script)
        {
            [1] = DynValue.NewTable(new Table(script)
            {
                ["label"] = "pp-1",
                ["type"] = "pressure_pump",
                ["address"] = "B2"
            })
        };

        var currentPressure = 0f;
        var enabled = false;
        const float maxPressure = 4500f;

        ProgrammableComputerLuaTestHelper.BindAtmos(script,
            list: _ => DynValue.NewTable(devices),
            pumpEnabled: (label, on) =>
            {
                calls.Add($"enabled:{label}:{on}");
                enabled = on;
                return DynValue.Void;
            },
            pumpRead: label =>
            {
                calls.Add($"read:{label}");
                var state = new Table(script)
                {
                    ["enabled"] = enabled,
                    ["type"] = "pressure_pump",
                    ["target_pressure"] = currentPressure,
                    ["max_target_pressure"] = maxPressure
                };
                return DynValue.NewTable(state);
            },
            pumpRate: (_, _) => DynValue.Void,
            pumpPressure: (label, pressure) =>
            {
                calls.Add($"pressure:{label}:{pressure:0.###}");
                currentPressure = pressure;
                return DynValue.Void;
            });

        var osTable = new Table(script);
        osTable["sleep"] = DynValue.NewCallback((_, _) => DynValue.Void);
        script.Globals["os"] = osTable;

        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/bin/pumptest.lua"));

        Assert.That(calls, Does.Contain("enabled:pp-1:True"));
        Assert.That(calls, Does.Contain("pressure:pp-1:4500"));
        Assert.That(calls, Does.Contain("pressure:pp-1:2250"));
        Assert.That(calls, Does.Contain("enabled:pp-1:False"));
        Assert.That(output, Has.Some.Contains("=== Test completed ==="));
    }
}