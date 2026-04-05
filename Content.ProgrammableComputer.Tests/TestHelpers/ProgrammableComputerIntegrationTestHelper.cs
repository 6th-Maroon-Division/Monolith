using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Content.Server.ProgrammableComputer;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.ProgrammableComputer;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.ProgrammableComputer.Tests;

internal static class ProgrammableComputerIntegrationTestHelper
{
    public static object GetRuntime(ProgrammableComputerSystem system, EntityUid computer)
    {
        var field = typeof(ProgrammableComputerSystem).GetField("_runtimes", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        var dict = field!.GetValue(system) as IDictionary;
        Assert.That(dict, Is.Not.Null);

        var runtime = dict![computer];
        Assert.That(runtime, Is.Not.Null, "Runtime was not created for computer entity.");
        return runtime!;
    }

    public static bool GetRuntimeBool(object runtime, string fieldName)
    {
        var field = runtime.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (bool) field!.GetValue(runtime)!;
    }

    public static string GetTerminalText(object runtime)
    {
        var terminalProp = runtime.GetType().GetProperty("Terminal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(terminalProp, Is.Not.Null);
        var terminal = terminalProp!.GetValue(runtime);
        Assert.That(terminal, Is.Not.Null);

        var getCells = terminal!.GetType().GetMethod("GetCells", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(getCells, Is.Not.Null);

        var cells = getCells!.Invoke(terminal, null) as Array;
        Assert.That(cells, Is.Not.Null);

        var chars = new char[cells!.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells.GetValue(i);
            if (cell == null)
            {
                chars[i] = ' ';
                continue;
            }

            var glyphField = cell.GetType().GetField("Glyph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            chars[i] = glyphField != null ? (char) glyphField.GetValue(cell)! : ' ';
        }

        return new string(chars);
    }

    public static ProgrammableComputerBoundUserInterfaceState GetUiState(IServerEntityManager entMan, EntityUid computer)
    {
        var uiComp = entMan.GetComponent<UserInterfaceComponent>(computer);

        var statesField = typeof(UserInterfaceComponent).GetField("States", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(statesField, Is.Not.Null);

        var states = statesField!.GetValue(uiComp) as IDictionary;
        Assert.That(states, Is.Not.Null);
        Assert.That(states!.Contains(ProgrammableComputerUiKey.Key), Is.True,
            "Programmable computer UI state was not populated.");

        var state = states[ProgrammableComputerUiKey.Key];
        Assert.That(state, Is.TypeOf<ProgrammableComputerBoundUserInterfaceState>());
        return (ProgrammableComputerBoundUserInterfaceState) state!;
    }

    public static string GetTerminalText(ProgrammableComputerBoundUserInterfaceState state)
    {
        return new string(state.TerminalCells.Select(cell => cell.Glyph).ToArray());
    }

    public static void InstallRequiredHardware(IServerEntityManager entMan, EntityUid computer)
    {
        var itemSlots = entMan.System<ItemSlotsSystem>();

        var cpu = entMan.SpawnEntity("ProgrammableComputerCpuTier1", MapCoordinates.Nullspace);
        var ram = entMan.SpawnEntity("ProgrammableComputerRamTier1", MapCoordinates.Nullspace);

        Assert.That(itemSlots.TryInsert(computer, ProgrammableComputerComponent.CpuSlotName, cpu, null), Is.True);
        Assert.That(itemSlots.TryInsert(computer, ProgrammableComputerComponent.RamSlotOneName, ram, null), Is.True);
    }

    public static void InsertModule(IServerEntityManager entMan, EntityUid computer, EntityUid module, string slotName)
    {
        var itemSlots = entMan.System<ItemSlotsSystem>();
        Assert.That(itemSlots.TryInsert(computer, slotName, module, null), Is.True,
            $"Failed to insert module into slot {slotName}");
    }
}