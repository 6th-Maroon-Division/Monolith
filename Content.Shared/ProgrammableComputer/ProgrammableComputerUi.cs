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

[Serializable, NetSerializable]
public sealed class ProgrammableComputerKeyMessage : BoundUserInterfaceMessage
{
    public readonly int KeyCode;
    public readonly bool Pressed;
    public readonly bool IsRepeat;
    public readonly bool Ctrl;
    public readonly bool Alt;
    public readonly bool Shift;
    public readonly bool Meta;

    public ProgrammableComputerKeyMessage(int keyCode, bool pressed, bool isRepeat, bool ctrl, bool alt, bool shift, bool meta)
    {
        KeyCode = keyCode;
        Pressed = pressed;
        IsRepeat = isRepeat;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
        Meta = meta;
    }
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
