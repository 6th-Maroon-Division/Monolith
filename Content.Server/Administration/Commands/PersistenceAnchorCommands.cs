using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Server)]
public sealed class RemovePersistenceAnchorCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "persistenceanchorremove";
    public string Description => "Archives and removes tracking for a persistence anchor.";
    public string Help => "persistenceanchorremove <netEntityUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netUid) || !_entManager.TryGetEntity(netUid, out var uid) || uid is not { } resolvedUid)
        {
            shell.WriteError($"Invalid entity uid '{args[0]}'.");
            return;
        }

        var system = _systems.GetEntitySystem<PersistenceAnchorSystem>();
        if (!system.RemoveTracking(resolvedUid, out var reason))
        {
            shell.WriteError(reason);
            return;
        }

        shell.WriteLine($"Archived persistence tracking for {resolvedUid}.\n");
    }
}

[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceAnchorStatusCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "persistenceanchorstatus";
    public string Description => "Shows active and archived persistence-anchor records.";
    public string Help => "persistenceanchorstatus";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var system = _systems.GetEntitySystem<PersistenceAnchorSystem>();
        shell.WriteLine(system.GetStatusReport());
    }
}

[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceAnchorBindCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "persistenceanchorbind";
    public string Description => "Binds or clears the named owner for a persistence anchor (for tracking ownership across rounds).";
    public string Help => "persistenceanchorbind <netEntityUid> [ownerName]   — omit ownerName to unbind";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netUid) || !_entManager.TryGetEntity(netUid, out var uid) || uid is not { } resolvedUid)
        {
            shell.WriteError($"Invalid entity uid '{args[0]}'.");
            return;
        }

        var ownerName = args.Length == 2 ? args[1] : null;
        var system = _systems.GetEntitySystem<PersistenceAnchorSystem>();

        if (!system.SetBoundOwner(resolvedUid, ownerName, out var reason))
        {
            shell.WriteError(reason);
            return;
        }

        shell.WriteLine(ownerName != null
            ? $"Bound persistence anchor to owner '{ownerName}'."
            : "Cleared persistence anchor owner binding.");
    }
}

[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceAnchorSaveCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "persistenceanchorsave";
    public string Description => "Force-saves persistence anchor state immediately instead of waiting for the autosave interval.";
    public string Help => "persistenceanchorsave <netEntityUid|all>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var system = _systems.GetEntitySystem<PersistenceAnchorSystem>();

        if (string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            var saved = system.ForceSaveAllAnchors();
            shell.WriteLine($"Force-saved {saved} persistence anchor(s).");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netUid) || !_entManager.TryGetEntity(netUid, out var uid) || uid is not { } resolvedUid)
        {
            shell.WriteError($"Invalid entity uid '{args[0]}'.");
            return;
        }

        if (!system.ForceSaveAnchor(resolvedUid, out var reason))
        {
            shell.WriteError(reason);
            return;
        }

        shell.WriteLine($"Force-saved persistence anchor state for {resolvedUid}.");
    }
}
