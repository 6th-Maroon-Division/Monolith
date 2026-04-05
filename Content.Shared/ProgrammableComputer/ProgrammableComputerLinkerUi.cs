using Robust.Shared.Serialization;

namespace Content.Shared.ProgrammableComputer;

[Serializable, NetSerializable]
public enum ProgrammableComputerLinkerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerLinkerBufferedEntry
{
    public NetEntity Target;
    public string Name = string.Empty;
    public string DeviceType = string.Empty;
    public string Address = string.Empty;

    public ProgrammableComputerLinkerBufferedEntry()
    {
    }

    public ProgrammableComputerLinkerBufferedEntry(NetEntity target, string name, string deviceType, string address)
    {
        Target = target;
        Name = name;
        DeviceType = deviceType;
        Address = address;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerLinkerUiState : BoundUserInterfaceState
{
    public readonly ProgrammableComputerLinkerBufferedEntry[] Buffered;

    public ProgrammableComputerLinkerUiState(ProgrammableComputerLinkerBufferedEntry[] buffered)
    {
        Buffered = buffered;
    }
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerLinkerClearAllMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ProgrammableComputerLinkerRemoveBufferedMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Target;

    public ProgrammableComputerLinkerRemoveBufferedMessage(NetEntity target)
    {
        Target = target;
    }
}
