using Robust.Shared.Serialization;
using Robust.Shared.Maths;

namespace Content.Shared.ProgrammableComputer;

[Serializable, NetSerializable]
public enum ProgrammableComputerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ProgrammableComputerPowerAction : byte
{
    Start,
    Shutdown,
    Reboot,
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerPowerActionMessage : BoundUserInterfaceMessage
{
    public readonly ProgrammableComputerPowerAction Action;

    public ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction action)
    {
        Action = action;
    }
}

/// <summary>
/// Packed key event. Bit layout:
///   bits  0-6  : Keyboard.Key enum value (0-120)
///   bit   7    : Pressed (1=down, 0=up)
///   bit   8    : IsRepeat
///   bit   9    : Ctrl
///   bit   10   : Alt
///   bit   11   : Shift
///   bit   12   : Meta/System
///   bits 13-14 : Layout (0=us, 1=qwertz, 2=azerty)
///   bits 15-31 : reserved/zero
/// </summary>
[Serializable, NetSerializable]
public sealed class ProgrammableComputerKeyMessage : BoundUserInterfaceMessage
{
    public readonly int Packed;

    public ProgrammableComputerKeyMessage(int packed)
    {
        Packed = packed;
    }

    // Unpack helpers
    public int KeyCode   => Packed & 0x7F;
    public bool Pressed  => (Packed & (1 << 7))  != 0;
    public bool IsRepeat => (Packed & (1 << 8))  != 0;
    public bool Ctrl     => (Packed & (1 << 9))  != 0;
    public bool Alt      => (Packed & (1 << 10)) != 0;
    public bool Shift    => (Packed & (1 << 11)) != 0;
    public bool Meta     => (Packed & (1 << 12)) != 0;
    public int  Layout   => (Packed >> 13) & 0x3;
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerRunCommandMessage : BoundUserInterfaceMessage
{
    public readonly string Command;

    public ProgrammableComputerRunCommandMessage(string command)
    {
        Command = command;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerTextInputMessage : BoundUserInterfaceMessage
{
    public readonly string Text;

    public ProgrammableComputerTextInputMessage(string text)
    {
        Text = text;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerTerminalCell
{
    public readonly char Glyph;
    public readonly Color Foreground;
    public readonly Color Background;

    public ProgrammableComputerTerminalCell(char glyph, Color foreground, Color background)
    {
        Glyph = glyph;
        Foreground = foreground;
        Background = background;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly ProgrammableComputerTerminalCell[] TerminalCells;
    public readonly string HardwareSummary;
    public readonly string LimitSummary;
    public readonly string Prompt;
    public readonly int Width;
    public readonly int Height;
    public readonly int CursorX;
    public readonly int CursorY;
    public readonly bool CursorBlink;
    public readonly bool PoweredOn;
    public readonly bool Booting;

    public ProgrammableComputerBoundUserInterfaceState(
        ProgrammableComputerTerminalCell[] terminalCells,
        string hardwareSummary,
        string limitSummary,
        string prompt,
        int width,
        int height,
        int cursorX,
        int cursorY,
        bool cursorBlink,
        bool poweredOn,
        bool booting)
    {
        TerminalCells = terminalCells;
        HardwareSummary = hardwareSummary;
        LimitSummary = limitSummary;
        Prompt = prompt;
        Width = width;
        Height = height;
        CursorX = cursorX;
        CursorY = cursorY;
        CursorBlink = cursorBlink;
        PoweredOn = poweredOn;
        Booting = booting;
    }

    public ProgrammableComputerBoundUserInterfaceState(
        ProgrammableComputerTerminalCell[] terminalCells,
        string hardwareSummary,
        string limitSummary,
        string prompt)
        : this(terminalCells, hardwareSummary, limitSummary, prompt, ProgrammableComputerComponent.TerminalWidth, ProgrammableComputerComponent.TerminalHeight, 1, 1, false, false, false)
    {
    }
}
