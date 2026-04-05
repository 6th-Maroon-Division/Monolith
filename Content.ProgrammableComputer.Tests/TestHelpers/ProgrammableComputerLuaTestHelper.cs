using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

internal static class ProgrammableComputerLuaTestHelper
{
    public static Script CreateRuntimeScript()
    {
        return new Script(CoreModules.Preset_HardSandbox | CoreModules.Coroutine | CoreModules.ErrorHandling | CoreModules.LoadMethods);
    }

    public static void BindTerm(Script script, List<string> lines)
    {
        var termTable = new Table(script);
        termTable["writeLine"] = DynValue.NewCallback((_, args) =>
        {
            var txt = args.Count > 0 ? args[0].CastToString() : string.Empty;
            lines.Add(txt ?? string.Empty);
            return DynValue.Void;
        });
        termTable["write"] = DynValue.NewCallback((_, args) =>
        {
            var txt = args.Count > 0 ? args[0].CastToString() : string.Empty;
            lines.Add(txt ?? string.Empty);
            return DynValue.Void;
        });
        script.Globals["term"] = termTable;
    }

    public static void BindAtmos(
        Script script,
        Func<string, DynValue> list,
        Func<string, bool, DynValue> pumpEnabled,
        Func<string, DynValue> pumpRead,
        Func<string, float, DynValue> pumpRate,
        Func<string, float, DynValue> pumpPressure,
        Func<string, Table, DynValue> vent = null,
        Func<string, DynValue> ventRead = null,
        Func<string, Table, DynValue> injector = null,
        Func<string, DynValue> injectorRead = null,
        Func<string, Table, DynValue> mixer = null,
        Func<string, DynValue> mixerRead = null,
        Func<string, Table, DynValue> regulator = null,
        Func<string, DynValue> regulatorRead = null)
    {
        var atmos = new Table(script);

        vent ??= static (_, _) => DynValue.Void;
        ventRead ??= _ => DynValue.NewTable(script);
        injector ??= static (_, _) => DynValue.Void;
        injectorRead ??= _ => DynValue.NewTable(script);
        mixer ??= static (_, _) => DynValue.Void;
        mixerRead ??= _ => DynValue.NewTable(script);
        regulator ??= static (_, _) => DynValue.Void;
        regulatorRead ??= _ => DynValue.NewTable(script);

        atmos["list"] = DynValue.NewCallback((_, _) => list(string.Empty));
        atmos["pump_enabled"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            var enabled = args[1].CastToBool();
            return pumpEnabled(label, enabled);
        });
        atmos["pump_read"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return pumpRead(label);
        });
        atmos["pump_rate"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            var rate = (float) args[1].Number;
            return pumpRate(label, rate);
        });
        atmos["pump_pressure"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            var pressure = (float) args[1].Number;
            return pumpPressure(label, pressure);
        });
        atmos["vent"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return vent(label, args[1].Table);
        });
        atmos["vent_read"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return ventRead(label);
        });
        atmos["injector"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return injector(label, args[1].Table);
        });
        atmos["injector_read"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return injectorRead(label);
        });
        atmos["mixer"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return mixer(label, args[1].Table);
        });
        atmos["mixer_read"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return mixerRead(label);
        });
        atmos["regulator"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return regulator(label, args[1].Table);
        });
        atmos["regulator_read"] = DynValue.NewCallback((_, args) =>
        {
            var label = args[0].CastToString() ?? string.Empty;
            return regulatorRead(label);
        });

        script.Globals["atmos"] = atmos;
    }

    public static string ReadRepoFile(string relativePath)
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Required test file not found: {fullPath}");

        return File.ReadAllText(fullPath);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (dir != null)
        {
            var slnx = Path.Combine(dir.FullName, "SpaceStation14.slnx");
            if (File.Exists(slnx))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing SpaceStation14.slnx.");
    }
}