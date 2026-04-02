using Robust.Shared.GameStates;

namespace Content.Shared.ProgrammableComputer;

[RegisterComponent]
public sealed partial class ProgrammableComputerComponent : Component
{
    public const int TerminalWidth = 59;
    public const int TerminalHeight = 19;

    public const string CpuMachinePart = "ProgrammableComputerCpu";
    public const string RamMachinePart = "ProgrammableComputerRam";
    public const string DiskMachinePart = "ProgrammableComputerDisk";
    public const string NetworkMachinePart = "ProgrammableComputerNetwork";
    public const string ExpansionMachinePart = "ProgrammableComputerExpansion";

    public const string CpuSlotName = "cpu_slot";
    public const string RamSlotOneName = "ram_slot_1";
    public const string RamSlotTwoName = "ram_slot_2";
    public const string DiskSlotOneName = "disk_slot_1";
    public const string DiskSlotTwoName = "disk_slot_2";
    public const string NetworkSlotName = "network_slot";
    public const string ExpansionSlotName = "expansion_slot";

    [DataField]
    public string Prompt = "lua>";

    [DataField]
    public int TerminalHistoryLimit = 128;

    [DataField]
    public int MaxCommandLength = 512;

    [DataField]
    public int MaxEventsPerTick = 32;

    [ViewVariables]
    public List<string> TerminalHistory { get; } = new();
}
