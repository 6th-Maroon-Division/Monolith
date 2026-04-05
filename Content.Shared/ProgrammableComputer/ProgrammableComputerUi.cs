using Robust.Shared.Serialization;
using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Content.Shared.ProgrammableComputer;

// ─── File management DTOs ──────────────────────────────────────────────────

/// <summary>Entry representing a file stored on the programmable computer.</summary>
[Serializable, NetSerializable]
public sealed class ProgrammableComputerFileEntry
{
    public string Name = string.Empty;
    public uint Size;
    public DateTime Modified;

    public ProgrammableComputerFileEntry() { }
    public ProgrammableComputerFileEntry(string name, uint size, DateTime modified)
    {
        Name = name;
        Size = size;
        Modified = modified;
    }
}

// ─── Atmos device link DTOs ────────────────────────────────────────────────

/// <summary>A named link that the computer has established to an atmos device.</summary>
[Serializable, NetSerializable]
public sealed class AtmosLinkEntry
{
    public string Label = string.Empty;
    public string DeviceType = string.Empty;
    public string Address = string.Empty;
    public string EntityName = string.Empty;

    public AtmosLinkEntry() { }
    public AtmosLinkEntry(string label, string deviceType, string address, string entityName)
    {
        Label = label;
        DeviceType = deviceType;
        Address = address;
        EntityName = entityName;
    }
}

/// <summary>A device discovered during a nearby scan, not yet linked.</summary>
[Serializable, NetSerializable]
public sealed class AtmosNearbyEntry
{
    public NetEntity Target;
    public string EntityName = string.Empty;
    public string DeviceType = string.Empty;
    public string Address = string.Empty;

    public AtmosNearbyEntry() { }
    public AtmosNearbyEntry(NetEntity target, string entityName, string deviceType, string address)
    {
        Target = target;
        EntityName = entityName;
        DeviceType = deviceType;
        Address = address;
    }
}

// ─── Atmos BUI messages ─────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class ProgrammableComputerAtmosScanMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class ProgrammableComputerAtmosLinkMessage : BoundUserInterfaceMessage
{
    public readonly string Label;
    public readonly NetEntity Target;

    public ProgrammableComputerAtmosLinkMessage(string label, NetEntity target)
    {
        Label = label;
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerAtmosUnlinkMessage : BoundUserInterfaceMessage
{
    public readonly string Label;
    public ProgrammableComputerAtmosUnlinkMessage(string label) => Label = label;
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerAtmosRenameMessage : BoundUserInterfaceMessage
{
    public readonly string OldLabel;
    public readonly string NewLabel;
    public ProgrammableComputerAtmosRenameMessage(string oldLabel, string newLabel)
    {
        OldLabel = oldLabel;
        NewLabel = newLabel;
    }
}

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
public sealed class ProgrammableComputerRefreshStateMessage : BoundUserInterfaceMessage
{
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
public sealed class ProgrammableComputerTouchMessage : BoundUserInterfaceMessage
{
    public readonly int X;
    public readonly int Y;

    public ProgrammableComputerTouchMessage(int x, int y)
    {
        X = x;
        Y = y;
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

// ─── File management messages ──────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class ProgrammableComputerRequestFileListMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class ProgrammableComputerUploadFileMessage : BoundUserInterfaceMessage
{
    public readonly string FileName;
    public readonly byte[] Content;

    public ProgrammableComputerUploadFileMessage(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerDownloadFileMessage : BoundUserInterfaceMessage
{
    public readonly string FileName;

    public ProgrammableComputerDownloadFileMessage(string fileName)
    {
        FileName = fileName;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerFileContentMessage : BoundUserInterfaceMessage
{
    public readonly string FileName;
    public readonly byte[] Content;

    public ProgrammableComputerFileContentMessage(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerDeleteFileMessage : BoundUserInterfaceMessage
{
    public readonly string FileName;

    public ProgrammableComputerDeleteFileMessage(string fileName)
    {
        FileName = fileName;
    }
}

// ─── Terminal cell and BUI state ──────────────────────────────────────────

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
    public readonly int RamAvailableKiB;
    public readonly int DiskAvailableKiB;

    /// <summary>Always-present list of configured atmos device links.</summary>
    public readonly AtmosLinkEntry[] AtmosLinks;

    /// <summary>Non-null only after the client requests a scan; null means no scan has been done yet.</summary>
    public readonly AtmosNearbyEntry[]? AtmosNearby;

    /// <summary>List of files stored on the programmable computer.</summary>
    public readonly ProgrammableComputerFileEntry[] Files;

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
        bool booting,
        int ramAvailableKiB = 0,
        int diskAvailableKiB = 0,
        AtmosLinkEntry[]? atmosLinks = null,
        AtmosNearbyEntry[]? atmosNearby = null,
        ProgrammableComputerFileEntry[]? files = null)
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
        RamAvailableKiB = ramAvailableKiB;
        DiskAvailableKiB = diskAvailableKiB;
        AtmosLinks = atmosLinks ?? [];
        AtmosNearby = atmosNearby;
        Files = files ?? [];
    }

    public ProgrammableComputerBoundUserInterfaceState(
        ProgrammableComputerTerminalCell[] terminalCells,
        string hardwareSummary,
        string limitSummary,
        string prompt)
        : this(terminalCells, hardwareSummary, limitSummary, prompt, ProgrammableComputerComponent.TerminalWidth, ProgrammableComputerComponent.TerminalHeight, 1, 1, false, false, false, 0, 0, null, null, null)
    {
    }
}
