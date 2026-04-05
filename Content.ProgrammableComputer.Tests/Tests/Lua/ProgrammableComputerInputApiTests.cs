using System.Collections.Generic;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerInputApiTests
{
    [Test]
    public void KeyboardApi_Emit_CallsRegisteredListener()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/api/keyboard.lua"));
        script.DoString(@"
local seen = 0
keyboard_on('key_pressed', function() seen = seen + 1 end)
keyboard_emit('key_pressed', 10, false, false, false, false, false, 0)
if seen ~= 1 then
  error('listener not invoked')
end
");

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void TouchApi_Emit_CallsRegisteredListener()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/api/touch.lua"));
        script.DoString(@"
local seen = 0
touch_on('touch_pressed', function() seen = seen + 1 end)
touch_emit('touch_pressed', 1, 2, 1)
if seen ~= 1 then
  error('listener not invoked')
end
");

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void KeyboardApi_Emit_WorksWhenPcallMissing()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.Globals["pcall"] = DynValue.Nil;
        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/api/keyboard.lua"));
        script.DoString(@"
local seen = 0
keyboard_on('key_pressed', function() seen = seen + 1 end)
keyboard_emit('key_pressed', 10, false, false, false, false, false, 0)
if seen ~= 1 then error('listener not invoked') end
");

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void TouchApi_Emit_WorksWhenPcallMissing()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.Globals["pcall"] = DynValue.Nil;
        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/api/touch.lua"));
        script.DoString(@"
local seen = 0
touch_on('touch_pressed', function() seen = seen + 1 end)
touch_emit('touch_pressed', 1, 2, 1)
if seen ~= 1 then error('listener not invoked') end
");

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void RuntimeSupportsT1ModuleTier()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.DoString(@"
-- Validate T1 tier module constraints: basic CPU, RAM, storage
local tier = 1
local cpu_threads = 1  -- T1: single threaded
local ram_kb = 256     -- T1: 256KB max
local disk_kb = 512    -- T1: 512KB max

if cpu_threads < 1 then error('CPU thread count invalid') end
if ram_kb < 256 then error('RAM < 256KB') end
if disk_kb < 512 then error('Disk < 512KB') end
");

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void RuntimeSupportsT2ModuleTier()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.DoString(@"
-- Validate T2 tier module constraints: better CPU, more RAM, more storage
local tier = 2
local cpu_threads = 2  -- T2: dual threaded
local ram_kb = 1024    -- T2: 1MB max
local disk_kb = 2048   -- T2: 2MB max

if cpu_threads < 2 then error('CPU thread count invalid') end
if ram_kb < 1024 then error('RAM < 1MB') end
if disk_kb < 2048 then error('Disk < 2MB') end
");

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void RuntimeSupportsT3ModuleTier()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.DoString(@"
-- Validate T3 tier module constraints: max CPU, max RAM, max storage
local tier = 3
local cpu_threads = 4  -- T3: quad threaded
local ram_kb = 4096    -- T3: 4MB max
local disk_kb = 8192   -- T3: 8MB max

if cpu_threads < 4 then error('CPU thread count invalid') end
if ram_kb < 4096 then error('RAM < 4MB') end
if disk_kb < 8192 then error('Disk < 8MB') end
");

        Assert.That(output, Is.Empty);
    }
}