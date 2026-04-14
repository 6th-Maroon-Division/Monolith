using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Content.Server._Crescent.ShipShields;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Gravity;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Mech.Systems;
using Content.Server.Salvage;
using Content.Server.Shuttles.Components;
using Content.Server._Mono.FireControl;
using Content.Server._Mono.Shuttles.Components;
using Content.Server._Mono.TargetSeekingAlert;
using Content.Server._NF.Station.Systems;
using Content.Server._NF.Shipyard.Systems;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage;
using Robust.Shared.EntitySerialization;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Containers;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Content.Shared._Mono.Shipyard;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Server.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed partial class PersistenceAnchorSystem : EntitySystem
{
    private const string RootDirectory = "/PersistenceAnchors";
    private const string ActiveDirectory = RootDirectory + "/Active";
    private const string ArchiveDirectory = RootDirectory + "/Archive";
    private const string ManifestPath = RootDirectory + "/manifest.json";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _xformSys = default!;
    [Dependency] private readonly DockingSystem _dockingSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly StationRenameWarpsSystems _renameWarps = default!;
    [Dependency] private readonly ShuttleConsoleLockSystem _shuttleLocks = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly ToggleableClothingSystem _toggleableClothingSystem = default!;
    [Dependency] private readonly GravityGeneratorSystem _gravityGenerators = default!;
    [Dependency] private readonly ShipShieldsSystem _shipShields = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly ExtensionCableSystem _extensionCables = default!;
    [Dependency] private readonly SalvageSystem _salvage = default!;
    [Dependency] private readonly FireControlSystem _fireControl = default!;
    [Dependency] private readonly MechSystem _mech = default!;

    private readonly Dictionary<EntityUid, string> _gridToAnchor = new();
    private readonly Dictionary<string, EntityUid> _anchorToGrid = new();
    private readonly Dictionary<EntityUid, string> _pendingRestoredGridAnchors = new();
    private readonly HashSet<string> _pendingRestoreHealAnchors = new();
    private readonly HashSet<EntityUid> _pendingRestoreUnlockGrids = new();
    private readonly HashSet<string> _dirtyAnchors = new();
    private readonly Dictionary<string, TimeSpan> _nextSaveAt = new();
    private readonly HashSet<EntityUid> _predefinedStations = new();
    private bool _suppressShutdownArchival;

    /// <summary>Set to true once ship snapshots have been restored for the current round.</summary>
    private bool _restoredThisRound;

    private PersistenceAnchorManifest _manifest = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public override void Initialize()
    {
        base.Initialize();

        EnsureStorageDirectories();
        LoadManifest();
        RebuildFromWorld();
        CapturePredefinedStations();

        SubscribeLocalEvent<PersistenceAnchorComponent, MapInitEvent>(OnAnchorMapInit);
        SubscribeLocalEvent<PersistenceAnchorComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<PersistenceAnchorComponent, ComponentShutdown>(OnAnchorShutdown);
        SubscribeLocalEvent<PersistenceAnchorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAnchorVerbs);
        SubscribeLocalEvent<PersistenceAnchorComponent, BoundUIOpenedEvent>(OnAnchorUiOpened);
        SubscribeLocalEvent<PersistenceAnchorComponent, PersistenceAnchorClaimOwnerMessage>(OnClaimOwnerMessage);
        SubscribeLocalEvent<PersistenceAnchorComponent, PersistenceAnchorUnlockGridMessage>(OnUnlockGridMessage);
        SubscribeLocalEvent<PersistenceAnchorComponent, EntInsertedIntoContainerMessage>(OnOwnerIdInserted);
        SubscribeLocalEvent<PersistenceAnchorComponent, EntRemovedFromContainerMessage>(OnOwnerIdRemoved);

        SubscribeLocalEvent<TransformComponent, MoveEvent>(OnEntityMove);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_dirtyAnchors.Count == 0)
            return;

        var due = new List<string>();
        foreach (var anchorId in _dirtyAnchors)
        {
            if (_nextSaveAt.TryGetValue(anchorId, out var next) && next > _timing.RealTime)
                continue;

            due.Add(anchorId);
        }

        foreach (var anchorId in due)
        {
            if (!_anchorToGrid.TryGetValue(anchorId, out var grid))
            {
                _dirtyAnchors.Remove(anchorId);
                continue;
            }

            if (!TryGetLiveAnchor(grid, anchorId, out var anchor, out var component))
            {
                _dirtyAnchors.Remove(anchorId);
                continue;
            }

            SaveSnapshot(anchor, component, immediate: false);
            _dirtyAnchors.Remove(anchorId);
        }
    }

    // ── Round lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// Before round cleanup (entities are flushed), force-save all dirty grids
    /// and clear in-memory collections so they can be rebuilt next round.
    /// </summary>
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _suppressShutdownArchival = true;

        // Force-save ALL registered anchors to capture the final world position, including
        // post-FTL positions. Grid entities have a null GridUid for themselves, so OnEntityMove
        // never marks the anchor dirty after an FTL jump; saving everything here guarantees
        // the snapshot reflects the true final state regardless of dirty tracking.
        foreach (var (anchorId, anchorGrid) in _anchorToGrid.ToList())
        {
            if (!TryGetLiveAnchor(anchorGrid, anchorId, out var anchor, out var component))
                continue;

            SaveSnapshot(anchor, component, immediate: true);
        }

        // Clear all in-memory state – entities are about to be flushed
        _gridToAnchor.Clear();
        _anchorToGrid.Clear();
        _pendingRestoredGridAnchors.Clear();
        _pendingRestoreHealAnchors.Clear();
        _pendingRestoreUnlockGrids.Clear();
        _dirtyAnchors.Clear();
        _nextSaveAt.Clear();
        _predefinedStations.Clear();
        _restoredThisRound = false;
    }

    /// <summary>
    /// When the first station map finishes loading, restore all active ship snapshots
    /// into the same map so they get MapInit'd alongside the station.
    /// </summary>
    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        _suppressShutdownArchival = false;

        if (_restoredThisRound)
            return;

        _restoredThisRound = true;
        CapturePredefinedStations();
        RestoreGridsFromSnapshot(ev.Map);
    }

    /// <summary>
    /// Capture station entities present at map-load time for this round.
    /// These are treated as predefined stations when classifying anchor placement.
    /// </summary>
    private void CapturePredefinedStations()
    {
        _predefinedStations.Clear();

        var stationQuery = EntityQueryEnumerator<StationDataComponent>();
        while (stationQuery.MoveNext(out var stationUid, out _))
        {
            _predefinedStations.Add(stationUid);
        }
    }

    /// <summary>
    /// After the round fully starts (MapInit done, players spawned), restore any
    /// saved dock links between anchored grids.
    /// </summary>
    private void OnRoundStarted(RoundStartedEvent ev)
    {
        _suppressShutdownArchival = false;
        UnlockRestoredGridLocks();
        RestoreDockLinks();
        HealRestoredSnapshots();
    }

    /// <summary>
    /// Load every active persistent grid snapshot that is not already in our in-memory registry
    /// into the given map.
    /// </summary>
    private void RestoreGridsFromSnapshot(MapId targetMap)
    {
        // Manifest corruption from earlier versions could contain many duplicate entries
        // for the same physical ship. Skip restoring obvious duplicates in one pass.
        var seenShipSignatures = new HashSet<string>(StringComparer.Ordinal);

        // Loading a snapshot can trigger persistence bookkeeping that mutates the manifest.
        // Iterate over a stable copy to avoid invalidating the dictionary enumerator mid-restore.
        foreach (var (anchorId, record) in _manifest.Records.ToArray())
        {
            if (record.Archived)
                continue;

            // Restore ships and player-made station grids.
            if (!ShouldRestoreRecord(record))
                continue;

            if (TryBuildShipRestoreSignature(record, out var signature)
                && !seenShipSignatures.Add(signature))
            {
                Log.Warning($"[Persistence] Archiving duplicate ship restore candidate '{record.GridName}' (anchor {anchorId}).");
                ArchiveManifestRecord(anchorId, record, "duplicate-restore");
                continue;
            }

            // Already registered (e.g. this is a server restart with grids still loaded)
            if (_anchorToGrid.ContainsKey(anchorId))
                continue;

            var snapshotPath = new ResPath(record.SnapshotPath);
            if (!_res.UserData.Exists(snapshotPath))
            {
                Log.Warning($"[Persistence] Snapshot missing for anchor {anchorId} ({record.GridName}) at {record.SnapshotPath}.");
                continue;
            }

            var loadOptions = DeserializationOptions.Default with
            {
                LogInvalidEntities = false,
                LogOrphanedGrids = false,
            };

            if (!_loader.TryLoadGrid(targetMap, snapshotPath, out var loadedGrid, loadOptions) || loadedGrid is null)
            {
                Log.Error($"[Persistence] Failed to load snapshot for anchor {anchorId} ({record.GridName}).");
                continue;
            }

            _pendingRestoredGridAnchors[loadedGrid.Value.Owner] = anchorId;
            _pendingRestoreHealAnchors.Add(anchorId);
            _pendingRestoreUnlockGrids.Add(loadedGrid.Value.Owner);

            Log.Info($"[Persistence] Restored persistent grid '{record.GridName}' (anchor {anchorId}) from snapshot.");
            // _anchorToGrid / _gridToAnchor will be populated naturally when the
            // PersistenceAnchorComponent on the loaded grid fires its MapInitEvent.
        }
    }

    /// <summary>
    /// Determine whether a manifest record should be restored via the persistent-grid restore path.
    /// </summary>
    private bool ShouldRestoreRecord(PersistenceAnchorRecord record)
    {
        if (record.Kind == PersistenceGridKind.Ship)
            return true;

        if (record.Kind != PersistenceGridKind.Station || string.IsNullOrWhiteSpace(record.SnapshotPath))
            return false;

        // Restore player-made station grids, but do not duplicate map-authored stations.
        if (record.PredefinedStation is true)
            return false;

        if (record.PredefinedStation is false)
            return true;

        return false;
    }

    /// <summary>
    /// Re-establish saved dock connections between persistent grids.
    /// Called once per round after MapInit, so all components are initialized.
    /// </summary>
    private void RestoreDockLinks()
    {
        foreach (var (anchorId, record) in _manifest.Records.ToArray())
        {
            if (record.Archived || record.DockLinks == null || record.DockLinks.Count == 0)
                continue;

            if (!_anchorToGrid.TryGetValue(anchorId, out var localGrid))
                continue;

            foreach (var link in record.DockLinks)
            {
                if (!_anchorToGrid.TryGetValue(link.OtherAnchorId, out var otherGrid))
                    continue;

                var localPort = FindDockPortByName(localGrid, link.LocalPortName);
                var otherPort = FindDockPortByName(otherGrid, link.OtherPortName);

                if (localPort == null || otherPort == null)
                {
                    Log.Warning($"[Persistence] Could not find dock ports for link {anchorId} <-> {link.OtherAnchorId}.");
                    continue;
                }

                if (localPort.Value.Comp.Docked)
                    continue; // already docked (e.g. loaded from file state)

                _dockingSystem.Dock(localPort.Value, otherPort.Value);
                Log.Info($"[Persistence] Restored dock link: {anchorId} <-> {link.OtherAnchorId}.");
            }
        }
    }

    /// <summary>
    /// After restore has completed and all entities are initialized, sanitize and re-save
    /// restored snapshots once to heal legacy invalid references.
    /// </summary>
    private void HealRestoredSnapshots()
    {
        foreach (var anchorId in _pendingRestoreHealAnchors.ToArray())
        {
            if (!_anchorToGrid.TryGetValue(anchorId, out var grid))
                continue;

            if (!TryGetLiveAnchor(grid, anchorId, out var anchor, out var component))
                continue;

            _shipyard.EnsureRestoredShuttleStation(grid);
            _deviceNetwork.ResyncGridDeviceNetwork(grid);
            _extensionCables.ResyncGridConnections(grid);
            _gravityGenerators.ResyncGridGravity(grid);
            _shipShields.ResyncGridShields(grid);
            _salvage.ResyncGridExpeditionConsoles(grid);
            _fireControl.ResyncGridFireControl(grid);
            _dockingSystem.ResyncGridDockAirlocks(grid);
            _mech.ResyncGridMechs(grid);

            SaveSnapshot(anchor, component, immediate: true);
            _pendingRestoreHealAnchors.Remove(anchorId);
        }
    }

    /// <summary>Find the first DockingComponent child of <paramref name="grid"/> whose entity name matches.</summary>
    private Entity<DockingComponent>? FindDockPortByName(EntityUid grid, string? portName)
    {
        if (string.IsNullOrEmpty(portName))
            return null;

        var query = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var dock, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (MetaData(uid).EntityName == portName)
                return (uid, dock);
        }

        return null;
    }

    // ── Startup rebuild ──────────────────────────────────────────────────────

    /// <summary>
    /// On system startup (server restarts without a round restart), walk already-loaded
    /// entities and rebuild <see cref="_gridToAnchor"/> / <see cref="_anchorToGrid"/>.
    /// </summary>
    private void RebuildFromWorld()
    {
        var query = EntityQueryEnumerator<PersistenceAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var xform))
        {
            if (string.IsNullOrWhiteSpace(anchor.AnchorId))
                continue;

            if (!_manifest.Records.ContainsKey(anchor.AnchorId))
                continue;

            if (xform.GridUid is not { } grid || !xform.Anchored)
                continue;

            _gridToAnchor[grid] = anchor.AnchorId;
            _anchorToGrid[anchor.AnchorId] = grid;
            _nextSaveAt[anchor.AnchorId] = _timing.RealTime + GetAutosaveInterval();

            Log.Debug($"[Persistence] Rebuilt tracking for anchor {anchor.AnchorId} on {ToPrettyString(grid)}.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public bool RemoveTracking(EntityUid anchorUid, out string reason)
    {
        reason = string.Empty;

        if (!TryComp(anchorUid, out PersistenceAnchorComponent? component))
        {
            reason = "Entity is not a persistence anchor.";
            return false;
        }

        ArchiveAnchor(anchorUid, component, "admin-remove");
        return true;
    }

    public bool SetBoundOwner(EntityUid anchorUid, string? ownerName, out string reason)
    {
        reason = string.Empty;

        if (!TryComp(anchorUid, out PersistenceAnchorComponent? component)
            || string.IsNullOrWhiteSpace(component.AnchorId))
        {
            reason = "Entity is not a registered persistence anchor.";
            return false;
        }

        if (!_manifest.Records.TryGetValue(component.AnchorId, out var record))
        {
            reason = "Anchor has no manifest record.";
            return false;
        }

        record.BoundOwner = ownerName;
        _manifest.Records[component.AnchorId] = record;
        WriteManifest();
        return true;
    }

    public bool ForceSaveAnchor(EntityUid anchorUid, out string reason)
    {
        reason = string.Empty;

        if (!TryComp(anchorUid, out PersistenceAnchorComponent? component))
        {
            reason = "Entity is not a persistence anchor.";
            return false;
        }

        var xform = Transform(anchorUid);
        if (!TryResolveAnchorGrid(anchorUid, xform, out var grid))
        {
            reason = "Persistence anchor must be on a grid before it can be saved.";
            return false;
        }

        if (!_gridToAnchor.TryGetValue(grid, out var anchorId)
            || string.IsNullOrWhiteSpace(component.AnchorId)
            || !string.Equals(anchorId, component.AnchorId, StringComparison.Ordinal))
        {
            TryRegisterAnchor(anchorUid, component, immediateSave: true);
        }

        if (string.IsNullOrWhiteSpace(component.AnchorId))
            component.AnchorId = Guid.NewGuid().ToString("N");

        var ensuredAnchorId = component.AnchorId;
        _gridToAnchor[grid] = ensuredAnchorId;
        _anchorToGrid[ensuredAnchorId] = grid;

        SaveSnapshot(anchorUid, component, immediate: true);

        _dirtyAnchors.Remove(ensuredAnchorId);

        return true;
    }

    public int ForceSaveAllAnchors()
    {
        var saved = 0;

        var query = EntityQueryEnumerator<PersistenceAnchorComponent>();
        while (query.MoveNext(out var anchorUid, out var component))
        {
            if (!ForceSaveAnchor(anchorUid, out _))
                continue;

            saved++;
        }

        return saved;
    }

    public string GetStatusReport()
    {
        if (_manifest.Records.Count == 0)
            return "No persistence anchor records found.";

        var lines = new List<string>
        {
            $"Persistence anchors: {_manifest.Records.Count}",
        };

        foreach (var (_, record) in _manifest.Records.OrderBy(pair => pair.Key))
        {
            var state = record.Archived ? "archived" : "active";
            var kind = record.Kind.ToString().ToLowerInvariant();
            var gridName = string.IsNullOrWhiteSpace(record.GridName) ? "<unnamed>" : record.GridName;
            var owner = string.IsNullOrWhiteSpace(record.BoundOwner) ? "" : $" owner={record.BoundOwner}";
            var stationText = string.Empty;
            if (record.Kind == PersistenceGridKind.Station)
            {
                var stationName = string.IsNullOrWhiteSpace(record.StationName) ? "<unknown>" : record.StationName;
                var stationUid = string.IsNullOrWhiteSpace(record.StationUid) ? "<unknown>" : record.StationUid;
                var stationProto = string.IsNullOrWhiteSpace(record.StationPrototype) ? "<none>" : record.StationPrototype;
                stationText = $" station={stationName} stationUid={stationUid} stationProto={stationProto}";
            }

            var predefinedText = record.Kind == PersistenceGridKind.Station
                ? $" predefinedStation={(record.PredefinedStation.HasValue ? (record.PredefinedStation.Value ? "yes" : "no") : "unknown")}"
                : string.Empty;
            var loaded = _anchorToGrid.TryGetValue(record.AnchorId, out var liveGrid);
            var loadedText = loaded ? $" loaded=yes liveGrid={liveGrid}" : " loaded=no";
            lines.Add($"- {record.AnchorId} [{kind}/{state}]{owner} grid={gridName}{stationText}{predefinedText}{loadedText} lastSaved={record.LastSavedUtc:O}");
        }

        return string.Join('\n', lines);
    }

    private void OnAnchorMapInit(Entity<PersistenceAnchorComponent> ent, ref MapInitEvent args)
    {
        // immediateSave: true so anchors placed mid-game (admin spawn, construction)
        // get an AnchorId assigned and a snapshot written immediately.
        // Prototype validation is still safe: TryRegisterAnchor returns early when
        // the entity has no valid shuttle/station grid.
        TryRegisterAnchor(ent, ent.Comp, immediateSave: true);
    }

    private void OnOwnerIdInserted(Entity<PersistenceAnchorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        RefreshAnchorUi(ent.Owner, ent.Comp);
    }

    private void OnOwnerIdRemoved(Entity<PersistenceAnchorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        RefreshAnchorUi(ent.Owner, ent.Comp);
    }

    private void OnAnchorStateChanged(Entity<PersistenceAnchorComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            if (_suppressShutdownArchival)
                return;

            ArchiveAnchor(ent, ent.Comp, "unanchored");
            return;
        }

        TryRegisterAnchor(ent, ent.Comp, immediateSave: true);
    }

    private void OnAnchorShutdown(Entity<PersistenceAnchorComponent> ent, ref ComponentShutdown args)
    {
        if (_suppressShutdownArchival)
            return;

        ArchiveAnchor(ent, ent.Comp, "shutdown");
    }

    private void OnEntityMove(EntityUid uid, TransformComponent component, ref MoveEvent args)
    {
        MarkDirty(component.GridUid);
    }

    private void OnDamageChanged(EntityUid uid, DamageableComponent component, ref DamageChangedEvent args)
    {
        MarkDirty(Transform(uid).GridUid);
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        MarkDirty(ev.Entity.Owner);
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        var uid = args.Entity.Owner;
        if (!Exists(uid))
            return;

        MarkDirty(Transform(uid).GridUid);
    }

    private void TryRegisterAnchor(EntityUid anchor, PersistenceAnchorComponent component, bool immediateSave)
    {
        var xform = Transform(anchor);
        if (!TryResolveAnchorGrid(anchor, xform, out var grid))
            return;

        // Only track station/shuttle grids. This keeps behavior intentional and
        // avoids trying to serialize arbitrary test grids during prototype sweeps.
        var isStationGrid = TryComp<StationMemberComponent>(grid, out _);
        var isShuttleGrid = TryComp<ShuttleComponent>(grid, out _);
        if (!isStationGrid && !isShuttleGrid)
            return;

        var wasPendingRestore = _pendingRestoredGridAnchors.TryGetValue(grid, out var restoredId);

        // For restored grids, always keep the manifest anchor id to prevent id drift that
        // can create duplicate active records and repeated restore cycles.
        if (wasPendingRestore && !string.Equals(component.AnchorId, restoredId, StringComparison.Ordinal))
            component.AnchorId = restoredId;

        if (string.IsNullOrWhiteSpace(component.AnchorId))
        {
            if (wasPendingRestore)
                component.AnchorId = restoredId;
            else if (!immediateSave)
                return;
            else
                component.AnchorId = Guid.NewGuid().ToString("N");
        }

        var anchorId = component.AnchorId;
        if (anchorId is null)
        {
            Log.Warning($"Grid {ToPrettyString(grid)} has null persistence anchor id after registration setup. Ignoring {ToPrettyString(anchor)}.");
            return;
        }

        if (_gridToAnchor.TryGetValue(grid, out var existingAnchor) && existingAnchor != anchorId)
        {
            Log.Warning($"Grid {ToPrettyString(grid)} already has persistence anchor {existingAnchor}. Ignoring {ToPrettyString(anchor)}.");
            return;
        }

        _pendingRestoredGridAnchors.Remove(grid);
        _gridToAnchor[grid] = anchorId;
        _anchorToGrid[anchorId] = grid;
        _nextSaveAt[anchorId] = _timing.RealTime + GetAutosaveInterval();

        if (wasPendingRestore)
            _shipyard.EnsureRestoredShuttleStation(grid);

        // Restored snapshots are already persisted; avoid an immediate re-save during
        // map/component startup where transient references may still be initializing.
        if (immediateSave && !wasPendingRestore)
            SaveSnapshot(anchor, component, immediate: true);

        UpdateVisual(anchor, PersistenceAnchorState.Clean);
    }

    private void SaveSnapshot(EntityUid anchor, PersistenceAnchorComponent component, bool immediate)
    {
        try
        {
            var xform = Transform(anchor);
            if (!TryResolveAnchorGrid(anchor, xform, out var grid) || string.IsNullOrWhiteSpace(component.AnchorId))
                return;

            EnsureStorageDirectories();

            var anchorId = component.AnchorId;
            var snapshotPath = GetActiveSnapshotPath(anchorId);

            var identity = EnsureComp<PersistenceAnchorIdentityComponent>(grid);
            if (string.IsNullOrWhiteSpace(identity.PersistentId))
            {
                identity.PersistentId = Guid.NewGuid().ToString("N");
            }

            var gridPersistentId = identity.PersistentId;
            // Some live ships may carry runtime references/components that are not fully serializable.
            // Use tolerant options so we persist as much as possible instead of failing the entire snapshot.
            var saveOptions = SerializationOptions.Default with
            {
                Category = FileCategory.Grid,
                MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
                EntityExceptionBehaviour = EntityExceptionBehaviour.IgnoreEntityAndChildren,
                ErrorOnOrphan = false,
                LogAutoInclude = null,
            };

            SanitizeGridForPersistence(grid);

            if (!_loader.TrySaveGrid(grid, snapshotPath, saveOptions))
            {
                Log.Error($"[Persistence] Failed to save snapshot for {ToPrettyString(grid)} (anchor {ToPrettyString(anchor)}).");
                return;
            }

            var isShuttle = TryComp<ShuttleComponent>(grid, out _);
            var isStation = TryComp<StationMemberComponent>(grid, out var stationMember);
            var gridKind = isShuttle ? PersistenceGridKind.Ship : PersistenceGridKind.Station;
            var stationUid = isStation ? stationMember!.Station : EntityUid.Invalid;
            MetaDataComponent? stationMeta = null;
            var hasValidStation = isStation && stationUid.IsValid() && TryComp(stationUid, out stationMeta);
            var predefinedStation = hasValidStation ? IsPredefinedStation(stationUid) : null;
            var gridMeta = MetaData(grid);
            var anchorMeta = MetaData(anchor);
            var worldPos = _xformSys.GetWorldPosition(grid);

            // Capture dock links to other anchored grids
            var dockLinks = CaptureDockLinks(grid);

            _manifest.Records[anchorId] = new PersistenceAnchorRecord
            {
                AnchorId = anchorId,
                AnchorPrototype = anchorMeta.EntityPrototype?.ID,
                AnchorName = anchorMeta.EntityName,
                GridName = gridMeta.EntityName,
                GridUid = grid.ToString(),
                GridPersistentId = gridPersistentId,
                SnapshotPath = snapshotPath.ToString(),
                Kind = gridKind,
                StationName = hasValidStation ? stationMeta!.EntityName : null,
                StationUid = hasValidStation ? stationUid.ToString() : null,
                StationPrototype = hasValidStation ? stationMeta!.EntityPrototype?.ID : null,
                PredefinedStation = predefinedStation,
                LastSavedUtc = DateTime.UtcNow,
                Archived = false,
                ArchiveReason = null,
                LastWorldX = worldPos.X,
                LastWorldY = worldPos.Y,
                DockLinks = dockLinks.Count > 0 ? dockLinks : null,
            };

            WriteManifest();
            _nextSaveAt[anchorId] = _timing.RealTime + GetAutosaveInterval();

            if (!immediate)
                Log.Debug($"[Persistence] Autosaved snapshot for {ToPrettyString(grid)} ({anchorId}).");

            UpdateVisual(anchor, PersistenceAnchorState.Clean);
        }
        catch (Exception e)
        {
            Log.Error($"[Persistence] Exception while saving snapshot for {ToPrettyString(anchor)}: {e}");
        }
    }

    /// <summary>
    /// Scan the grid for docked port pairs that connect to another anchored grid,
    /// returning one <see cref="DockLinkInfo"/> per connection.
    /// </summary>
    private List<DockLinkInfo> CaptureDockLinks(EntityUid grid)
    {
        var links = new List<DockLinkInfo>();
        var dockQuery = EntityQueryEnumerator<DockingComponent, TransformComponent>();

        while (dockQuery.MoveNext(out var dockUid, out var dock, out var dockXform))
        {
            if (dockXform.GridUid != grid)
                continue;

            if (!dock.Docked || dock.DockedWith is not { } partnerUid)
                continue;

            var partnerXform = Transform(partnerUid);
            if (partnerXform.GridUid is not { } partnerGrid)
                continue;

            if (!_gridToAnchor.TryGetValue(partnerGrid, out var otherAnchorId))
                continue;

            // Avoid recording duplicates for the same pair
            if (links.Any(l => l.OtherAnchorId == otherAnchorId))
                continue;

            links.Add(new DockLinkInfo
            {
                OtherAnchorId = otherAnchorId,
                LocalPortName = MetaData(dockUid).EntityName,
                OtherPortName = MetaData(partnerUid).EntityName,
            });
        }

        return links;
    }

    /// <summary>
    /// Archive a manifest record by anchor id without requiring a live entity.
    /// Used to clean up duplicate records that are skipped during restore.
    /// </summary>
    private void ArchiveManifestRecord(string anchorId, PersistenceAnchorRecord record, string reason)
    {
        EnsureStorageDirectories();

        var activePath = GetActiveSnapshotPath(anchorId);
        var archivePath = GetArchiveSnapshotPath(anchorId);
        if (_res.UserData.Exists(activePath))
        {
            if (_res.UserData.Exists(archivePath))
                _res.UserData.Delete(archivePath);

            _res.UserData.Rename(activePath, archivePath);
        }

        record.Archived = true;
        record.ArchiveReason = reason;
        record.ArchivedUtc = DateTime.UtcNow;
        record.ArchivePath = archivePath.ToString();
        _manifest.Records[anchorId] = record;
        WriteManifest();
    }

    private void ArchiveAnchor(EntityUid anchor, PersistenceAnchorComponent component, string reason)
    {
        if (string.IsNullOrWhiteSpace(component.AnchorId))
            return;

        var anchorId = component.AnchorId;

        if (_manifest.Records.TryGetValue(anchorId, out var record))
        {
            EnsureStorageDirectories();

            var activePath = GetActiveSnapshotPath(anchorId);
            var archivePath = GetArchiveSnapshotPath(anchorId);
            if (_res.UserData.Exists(activePath))
            {
                if (_res.UserData.Exists(archivePath))
                    _res.UserData.Delete(archivePath);

                _res.UserData.Rename(activePath, archivePath);
            }

            record.Archived = true;
            record.ArchiveReason = reason;
            record.ArchivedUtc = DateTime.UtcNow;
            record.ArchivePath = archivePath.ToString();
            _manifest.Records[anchorId] = record;
            WriteManifest();
        }

        if (_anchorToGrid.TryGetValue(anchorId, out var grid))
            _gridToAnchor.Remove(grid);

        _anchorToGrid.Remove(anchorId);
        _dirtyAnchors.Remove(anchorId);
        _nextSaveAt.Remove(anchorId);

        UpdateVisual(anchor, PersistenceAnchorState.Inactive);
    }

    private void MarkDirty(EntityUid? gridUid)
    {
        if (gridUid is null)
            return;

        if (!_gridToAnchor.TryGetValue(gridUid.Value, out var anchorId))
            return;

        if (_dirtyAnchors.Add(anchorId))
        {
            // Update visual to show pending-save state
            if (TryGetLiveAnchor(gridUid.Value, anchorId, out var anchor, out _))
                UpdateVisual(anchor, PersistenceAnchorState.Dirty);
        }
    }

    private void OnAnchorUiOpened(Entity<PersistenceAnchorComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.Actor is not { Valid: true })
            return;

        RefreshAnchorUi(ent.Owner, ent.Comp);
    }

    private void OnClaimOwnerMessage(Entity<PersistenceAnchorComponent> ent, ref PersistenceAnchorClaimOwnerMessage args)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.AnchorId))
            return;

        if (args.Actor is not { Valid: true } actor)
            return;

        var anchorId = ent.Comp.AnchorId;
        if (!_manifest.Records.TryGetValue(anchorId, out var record))
            return;

        var insertedId = GetInsertedOwnerId(ent.Owner);
        if (insertedId is not { Valid: true } targetId || !TryComp<IdCardComponent>(targetId, out var idCard))
        {
            _popup.PopupEntity(Loc.GetString("persistence-anchor-owner-claim-requires-id"), ent.Owner, actor);
            RefreshAnchorUi(ent.Owner, ent.Comp);
            return;
        }

        var ownerName = string.IsNullOrWhiteSpace(idCard.FullName) ? Name(targetId) : idCard.FullName;
        record.BoundOwner = ownerName;
        _manifest.Records[anchorId] = record;
        WriteManifest();

        var xform = Transform(ent.Owner);
        if (xform.GridUid is { } grid)
        {
            _shipyard.SetShuttleOwner(grid, ownerName);

            _shipyard.ReissueDeedToCard(targetId, grid);
        }

        _popup.PopupEntity(Loc.GetString("persistence-anchor-owner-claim-success", ("owner", ownerName)), ent.Owner, actor);
        RefreshAnchorUi(ent.Owner, ent.Comp);
    }

    private void OnUnlockGridMessage(Entity<PersistenceAnchorComponent> ent, ref PersistenceAnchorUnlockGridMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } grid)
            return;

        var changed = UnlockGridLocks(grid);
        if (changed)
            _popup.PopupEntity(Loc.GetString("persistence-anchor-unlock-success"), ent.Owner, actor);
        else
            _popup.PopupEntity(Loc.GetString("persistence-anchor-unlock-noop"), ent.Owner, actor);

        RefreshAnchorUi(ent.Owner, ent.Comp);
    }

    private void RefreshAnchorUi(EntityUid anchor, PersistenceAnchorComponent component)
    {
        if (!_ui.HasUi(anchor, PersistenceAnchorUiKey.Key))
            return;

        var xform = Transform(anchor);
        if (xform.GridUid is not { } grid || string.IsNullOrWhiteSpace(component.AnchorId))
            return;

        var anchorId = component.AnchorId;
        var gridName = MetaData(grid).EntityName;
        string? owner = null;
        string? insertedIdName = null;
        var hasInsertedIdCard = false;

        var targetId = GetInsertedOwnerId(anchor);
        if (targetId is { Valid: true } inserted && TryComp<IdCardComponent>(inserted, out var idCard))
        {
            hasInsertedIdCard = true;
            insertedIdName = string.IsNullOrWhiteSpace(idCard.FullName)
                ? Name(inserted)
                : idCard.FullName;
        }

        if (_manifest.Records.TryGetValue(anchorId, out var record))
        {
            if (!string.IsNullOrWhiteSpace(record.GridName))
                gridName = record.GridName;

            owner = record.BoundOwner;
        }

        var locked = IsGridLocked(grid);
        _ui.SetUiState(anchor, PersistenceAnchorUiKey.Key,
            new PersistenceAnchorBoundUserInterfaceState(
                anchorId,
                gridName,
                owner,
                insertedIdName,
                hasInsertedIdCard,
                locked,
                canClaimOwner: true,
                canUnlock: locked));
    }

    private EntityUid? GetInsertedOwnerId(EntityUid anchor)
    {
        if (!_containers.TryGetContainer(anchor, PersistenceAnchorComponent.OwnerIdCardSlotId, out var container)
            || container is not ContainerSlot slot)
            return null;

        return slot.ContainedEntity;
    }

    private bool TryResolveAnchorGrid(EntityUid anchor, TransformComponent xform, out EntityUid grid)
    {
        if (xform.GridUid is { } directGrid)
        {
            grid = directGrid;
            return true;
        }

        if (_xformSys.GetGrid(anchor) is { } entGrid)
        {
            grid = entGrid;
            return true;
        }

        if (_xformSys.GetGrid(xform.Coordinates) is { } coordGrid)
        {
            grid = coordGrid;
            return true;
        }

        var coordEntity = xform.Coordinates.EntityId;
        if (coordEntity.IsValid() && HasComp<MapGridComponent>(coordEntity))
        {
            grid = coordEntity;
            return true;
        }

        if (xform.ParentUid is { Valid: true } parent && HasComp<MapGridComponent>(parent))
        {
            grid = parent;
            return true;
        }

        var mapCoords = _xformSys.GetMapCoordinates(anchor, xform);
        if (_mapManager.TryFindGridAt(mapCoords, out var mapGrid, out _))
        {
            grid = mapGrid;
            return true;
        }

        var bestDistSq = float.MaxValue;
        EntityUid bestGrid = EntityUid.Invalid;
        var anchorPos = mapCoords.Position;

        var shuttleQuery = EntityQueryEnumerator<ShuttleComponent, TransformComponent, MapGridComponent>();
        while (shuttleQuery.MoveNext(out var shuttleGrid, out _, out var shuttleXform, out _))
        {
            if (shuttleXform.MapID != mapCoords.MapId)
                continue;

            var gridPos = _xformSys.GetWorldPosition(shuttleGrid);
            var distSq = Vector2.DistanceSquared(anchorPos, gridPos);
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            bestGrid = shuttleGrid;
        }

        var stationQuery = EntityQueryEnumerator<StationMemberComponent, TransformComponent, MapGridComponent>();
        while (stationQuery.MoveNext(out var stationGrid, out _, out var stationXform, out _))
        {
            if (stationXform.MapID != mapCoords.MapId)
                continue;

            var gridPos = _xformSys.GetWorldPosition(stationGrid);
            var distSq = Vector2.DistanceSquared(anchorPos, gridPos);
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            bestGrid = stationGrid;
        }

        if (bestGrid.IsValid())
        {
            grid = bestGrid;
            return true;
        }

        grid = EntityUid.Invalid;
        return false;
    }

    private void UnlockRestoredGridLocks()
    {
        foreach (var grid in _pendingRestoreUnlockGrids.ToArray())
        {
            if (!grid.IsValid() || !Exists(grid) || TerminatingOrDeleted(grid))
            {
                _pendingRestoreUnlockGrids.Remove(grid);
                continue;
            }

            UnlockGridLocks(grid);
            _renameWarps.SyncWarpPointsToGrid(grid);
            _pendingRestoreUnlockGrids.Remove(grid);
        }
    }

    private bool UnlockGridLocks(EntityUid grid)
    {
        return _shuttleLocks.ForceUnlockGrid(grid);
    }

    private bool IsGridLocked(EntityUid grid)
    {
        if (TryComp<ShipGridLockComponent>(grid, out var gridLock))
            return gridLock.Locked;

        var query = EntityQueryEnumerator<ShuttleConsoleLockComponent, TransformComponent>();
        while (query.MoveNext(out _, out var lockComp, out var xform))
        {
            if (xform.GridUid == grid && lockComp.Locked)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Set the visual state of the persistence anchor entity.
    /// Drives the <see cref="PersistenceAnchorVisuals.State"/> appearance key.
    /// </summary>
    private void UpdateVisual(EntityUid anchor, PersistenceAnchorState state)
    {
        if (!TryComp<AppearanceComponent>(anchor, out var appearance))
            return;

        _appearance.SetData(anchor, PersistenceAnchorVisuals.State, state, appearance);
    }

    private bool TryGetLiveAnchor(EntityUid grid, string anchorId, out EntityUid anchor, out PersistenceAnchorComponent component)
    {
        var query = EntityQueryEnumerator<PersistenceAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchorComp, out var xform))
        {
            if (anchorComp.AnchorId != anchorId)
                continue;

            if (xform.GridUid != grid)
                continue;

            anchor = uid;
            component = anchorComp;
            return true;
        }

        anchor = EntityUid.Invalid;
        component = default!;
        return false;
    }

    private TimeSpan GetAutosaveInterval()
    {
        return TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.PersistenceAnchorAutosaveInterval));
    }

    private static bool TryBuildShipRestoreSignature(PersistenceAnchorRecord record, out string signature)
    {
        if (record.Kind != PersistenceGridKind.Ship || string.IsNullOrWhiteSpace(record.GridPersistentId))
        {
            signature = string.Empty;
            return false;
        }

        signature = $"gid:{record.GridPersistentId}";
        return true;
    }

    private void SanitizeGridForPersistence(EntityUid grid)
    {
        var changedRepairData = false;
        var changedStorage = false;
        var changedDocking = false;
        var changedStationMember = false;
        var changedJoints = false;
        var changedGuestAccess = false;
        var changedTargetSeeker = false;
        var changedJobSlots = false;
        var changedToggleableClothing = false;

        var repairQuery = EntityQueryEnumerator<ShipRepairDataComponent, TransformComponent>();
        while (repairQuery.MoveNext(out var uid, out var repairData, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            var componentChanged = false;

            foreach (var chunk in repairData.Chunks.Values)
            {
                foreach (var spec in chunk.Entities.Values)
                {
                    if (spec.OriginalEntity is { } originalNet && !originalNet.IsValid())
                    {
                        spec.OriginalEntity = null;
                        componentChanged = true;
                    }
                }
            }

            if (componentChanged)
            {
                changedRepairData = true;
                Dirty(uid, repairData);
            }
        }

        var storageQuery = EntityQueryEnumerator<StorageComponent, TransformComponent>();
        while (storageQuery.MoveNext(out var uid, out var storage, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            var sanitized = new Dictionary<EntityUid, ItemStorageLocation>();
            var componentChanged = false;

            foreach (var (itemUid, location) in storage.StoredItems)
            {
                if (!itemUid.IsValid() || !Exists(itemUid) || TerminatingOrDeleted(itemUid))
                {
                    componentChanged = true;
                    continue;
                }

                // Ensure storage state is coherent with its backing container and current grid snapshot.
                if (!storage.Container.Contains(itemUid))
                {
                    componentChanged = true;
                    continue;
                }

                var itemXform = Transform(itemUid);
                if (itemXform.GridUid != grid)
                {
                    componentChanged = true;
                    continue;
                }

                if (!sanitized.TryAdd(itemUid, location))
                    componentChanged = true;
            }

            if (!componentChanged)
                continue;

            storage.StoredItems = sanitized;

            changedStorage = true;
            Dirty(uid, storage);
        }

        var dockingQuery = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (dockingQuery.MoveNext(out var uid, out var docking, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (docking.DockedWith is not { } dockedWith)
                continue;

            if (dockedWith.IsValid() && Exists(dockedWith))
                continue;

            docking.DockedWith = null;
            docking.DockJoint = null;
            docking.DockJointId = null;
            changedDocking = true;
            Dirty(uid, docking);
        }

        if (TryComp<StationMemberComponent>(grid, out var stationMember)
            && stationMember.Station.IsValid()
            && !Exists(stationMember.Station))
        {
            stationMember.Station = EntityUid.Invalid;
            changedStationMember = true;
            Dirty(grid, stationMember);
        }

        var jointQuery = EntityQueryEnumerator<JointComponent, TransformComponent>();
        while (jointQuery.MoveNext(out var uid, out var jointComp, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (jointComp.Relay is not { } relay)
                continue;

            if (relay.IsValid() && Exists(relay))
                continue;

            jointComp.Relay = null;
            changedJoints = true;
            Dirty(uid, jointComp);
        }

        var guestAccessQuery = EntityQueryEnumerator<ShipGuestAccessComponent, TransformComponent>();
        while (guestAccessQuery.MoveNext(out var uid, out var guestAccess, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            var componentChanged = false;
            var sanitizedCards = new HashSet<EntityUid>();
            foreach (var cardUid in guestAccess.GuestIdCards)
            {
                if (!cardUid.IsValid() || !Exists(cardUid) || TerminatingOrDeleted(cardUid))
                {
                    componentChanged = true;
                    continue;
                }

                if (!sanitizedCards.Add(cardUid))
                    componentChanged = true;
            }

            var sanitizedCyborgs = new HashSet<EntityUid>();
            foreach (var cyborgUid in guestAccess.GuestCyborgs)
            {
                if (!cyborgUid.IsValid() || !Exists(cyborgUid) || TerminatingOrDeleted(cyborgUid))
                {
                    componentChanged = true;
                    continue;
                }

                if (!sanitizedCyborgs.Add(cyborgUid))
                    componentChanged = true;
            }

            if (!componentChanged)
                continue;

            guestAccess.GuestIdCards = sanitizedCards;
            guestAccess.GuestCyborgs = sanitizedCyborgs;
            changedGuestAccess = true;
            Dirty(uid, guestAccess);
        }

        if (TryComp<TargetSeekerAlertGridComponent>(grid, out var targetSeeker))
        {
            var componentChanged = false;
            var sanitizedAlerters = new List<EntityUid>(targetSeeker.Alerters.Count);
            var seen = new HashSet<EntityUid>();

            foreach (var alerterUid in targetSeeker.Alerters)
            {
                if (!alerterUid.IsValid() || !Exists(alerterUid) || TerminatingOrDeleted(alerterUid) || !HasComp<TargetSeekerAlertComponent>(alerterUid))
                {
                    componentChanged = true;
                    continue;
                }

                if (Transform(alerterUid).GridUid != grid)
                {
                    componentChanged = true;
                    continue;
                }

                if (!seen.Add(alerterUid))
                {
                    componentChanged = true;
                    continue;
                }

                sanitizedAlerters.Add(alerterUid);
            }

            if (componentChanged)
            {
                targetSeeker.Alerters = sanitizedAlerters;
                targetSeeker.ActiveAlerters.RemoveWhere(x => !x.Owner.IsValid() || !Exists(x.Owner) || Transform(x.Owner).GridUid != grid);
                targetSeeker.CurrentSeekers.RemoveWhere(x => !x.Owner.IsValid() || !Exists(x.Owner));
                changedTargetSeeker = true;
                Dirty(grid, targetSeeker);
            }
        }

        var jobSlotsQuery = EntityQueryEnumerator<ShuttleConsoleJobSlotsComponent, TransformComponent>();
        while (jobSlotsQuery.MoveNext(out var uid, out var jobSlots, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (jobSlots.OwningStation is not { } stationUid)
                continue;

            if (stationUid.IsValid() && Exists(stationUid) && !TerminatingOrDeleted(stationUid))
                continue;

            jobSlots.OwningStation = null;
            changedJobSlots = true;
            Dirty(uid, jobSlots);
        }

        var toggleableQuery = EntityQueryEnumerator<ToggleableClothingComponent, TransformComponent>();
        while (toggleableQuery.MoveNext(out var uid, out var toggleable, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (!_toggleableClothingSystem.SanitizeForPersistence((uid, toggleable)))
                continue;

            changedToggleableClothing = true;
        }

        if (changedRepairData || changedStorage || changedDocking || changedStationMember || changedJoints || changedGuestAccess || changedTargetSeeker || changedJobSlots || changedToggleableClothing)
            Log.Debug($"[Persistence] Sanitized grid {ToPrettyString(grid)} before snapshot (repairData={changedRepairData}, storage={changedStorage}, docking={changedDocking}, stationMember={changedStationMember}, joints={changedJoints}, guestAccess={changedGuestAccess}, targetSeeker={changedTargetSeeker}, jobSlots={changedJobSlots}, toggleableClothing={changedToggleableClothing}).");
    }

    private void EnsureStorageDirectories()
    {
        _res.UserData.CreateDir(new ResPath(RootDirectory));
        _res.UserData.CreateDir(new ResPath(ActiveDirectory));
        _res.UserData.CreateDir(new ResPath(ArchiveDirectory));
    }

    private void LoadManifest()
    {
        if (!_res.UserData.TryReadAllText(new ResPath(ManifestPath), out var json) || string.IsNullOrWhiteSpace(json))
        {
            _manifest = new PersistenceAnchorManifest();
            return;
        }

        try
        {
            _manifest = JsonSerializer.Deserialize<PersistenceAnchorManifest>(json, JsonOptions) ?? new PersistenceAnchorManifest();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load persistence anchor manifest: {e}");
            _manifest = new PersistenceAnchorManifest();
        }

        ConsolidateDuplicateActiveShipRecords();
    }

    /// <summary>
    /// Consolidate duplicate active ship records that point at the same physical vessel.
    /// Keeps the most recently saved record and archives the rest.
    /// </summary>
    private void ConsolidateDuplicateActiveShipRecords()
    {
        var grouped = new Dictionary<string, List<(string AnchorId, PersistenceAnchorRecord Record)>>(StringComparer.Ordinal);

        foreach (var (anchorId, record) in _manifest.Records)
        {
            if (record.Archived)
                continue;

            if (!TryBuildShipRestoreSignature(record, out var signature))
                continue;

            if (!grouped.TryGetValue(signature, out var list))
            {
                list = new List<(string AnchorId, PersistenceAnchorRecord Record)>();
                grouped[signature] = list;
            }

            list.Add((anchorId, record));
        }

        var archivedCount = 0;

        foreach (var (_, group) in grouped)
        {
            if (group.Count <= 1)
                continue;

            var ordered = group
                .OrderByDescending(entry => entry.Record.LastSavedUtc)
                .ThenBy(entry => entry.AnchorId, StringComparer.Ordinal)
                .ToList();

            foreach (var duplicate in ordered.Skip(1))
            {
                ArchiveManifestRecord(duplicate.AnchorId, duplicate.Record, "duplicate-manifest");
                archivedCount++;
            }
        }

        if (archivedCount > 0)
            Log.Warning($"[Persistence] Archived {archivedCount} duplicate active ship record(s) while loading manifest.");
    }

    private void WriteManifest()
    {
        var json = JsonSerializer.Serialize(_manifest, JsonOptions);
        _res.UserData.WriteAllText(new ResPath(ManifestPath), json);
    }

    private static ResPath GetActiveSnapshotPath(string anchorId)
    {
        return new ResPath($"{ActiveDirectory}/{anchorId}.yml");
    }

    private static ResPath GetArchiveSnapshotPath(string anchorId)
    {
        return new ResPath($"{ArchiveDirectory}/{anchorId}.yml");
    }

    private bool? IsPredefinedStation(EntityUid stationUid)
    {
        if (!stationUid.IsValid())
            return null;

        if (_predefinedStations.Count == 0)
            return null;

        return _predefinedStations.Contains(stationUid);
    }

    // ── Deed claim verb ──────────────────────────────────────────────────────

    /// <summary>
    /// Adds an "Claim Ship Deed" <see cref="AlternativeVerb"/> on persistence anchors whose
    /// grid has a <see cref="ShuttleDeedComponent"/>. When used while holding an ID card,
    /// copies the deed info (shuttle name, owner) onto the card so the restored ship console
    /// can be unlocked the same way it was before the round reset.
    /// </summary>
    private void OnGetAnchorVerbs(Entity<PersistenceAnchorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // The anchor must be on a grid that has a shuttle deed.
        var anchorXform = Transform(ent.Owner);
        if (anchorXform.GridUid is not { } grid || !HasComp<ShuttleDeedComponent>(grid))
            return;

        // Find the first usable ID card in the user's hands (bare card or inside a PDA).
        EntityUid? idCard = null;
        foreach (var hand in _handsSystem.EnumerateHands(args.User))
        {
            if (hand.HeldEntity == null)
                continue;

            if (HasComp<IdCardComponent>(hand.HeldEntity.Value))
            {
                idCard = hand.HeldEntity.Value;
                break;
            }

            if (TryComp<PdaComponent>(hand.HeldEntity.Value, out var pda) && pda.ContainedId is { } pdaCard)
            {
                idCard = pdaCard;
                break;
            }
        }

        if (idCard == null)
            return;

        var cardRef = idCard.Value;
        var gridRef = grid;
        var userRef = args.User;
        var anchorRef = ent.Owner;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("persistence-anchor-claim-deed-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () =>
            {
                if (!_shipyard.ReissueDeedToCard(cardRef, gridRef))
                {
                    _popup.PopupEntity(Loc.GetString("persistence-anchor-claim-deed-fail"), anchorRef, userRef);
                    return;
                }

                _popup.PopupEntity(Loc.GetString("persistence-anchor-claim-deed-success"), anchorRef, userRef);
            },
        });
    }

    [Serializable, DataDefinition]
    private sealed partial class PersistenceAnchorManifest
    {
        [DataField]
        public Dictionary<string, PersistenceAnchorRecord> Records { get; set; } = new();
    }

    [Serializable, DataDefinition]
    private sealed partial class PersistenceAnchorRecord
    {
        [DataField]
        public string AnchorId { get; set; } = string.Empty;

        [DataField]
        public string? AnchorPrototype { get; set; }

        [DataField]
        public string? AnchorName { get; set; }

        [DataField]
        public string? GridName { get; set; }

        [DataField]
        public string? GridUid { get; set; }

        [DataField]
        public string? GridPersistentId { get; set; }

        [DataField]
        public PersistenceGridKind Kind { get; set; }

        [DataField]
        public string SnapshotPath { get; set; } = string.Empty;

        [DataField]
        public string? ArchivePath { get; set; }

        [DataField]
        public string? StationName { get; set; }

        [DataField]
        public string? StationUid { get; set; }

        [DataField]
        public string? StationPrototype { get; set; }

        [DataField]
        public bool? PredefinedStation { get; set; }

        [DataField]
        public bool Archived { get; set; }

        [DataField]
        public string? ArchiveReason { get; set; }

        [DataField]
        public DateTime LastSavedUtc { get; set; }

        [DataField]
        public DateTime? ArchivedUtc { get; set; }

        /// <summary>World X position at last save (used to restore ship position next round).</summary>
        [DataField]
        public float LastWorldX { get; set; }

        /// <summary>World Y position at last save (used to restore ship position next round).</summary>
        [DataField]
        public float LastWorldY { get; set; }

        /// <summary>Saved dock connections to other anchored grids at the time of the last save.</summary>
        [DataField]
        public List<DockLinkInfo>? DockLinks { get; set; }

        /// <summary>Optional display name of the bound owner (player or org) for this anchor.</summary>
        [DataField]
        public string? BoundOwner { get; set; }
    }

    [Serializable]
    private sealed class DockLinkInfo
    {
        /// <summary>AnchorId of the other anchored grid that this grid was docked to.</summary>
        public string OtherAnchorId { get; set; } = string.Empty;

        /// <summary>Entity name of the local DockingComponent port.</summary>
        public string? LocalPortName { get; set; }

        /// <summary>Entity name of the partner DockingComponent port on the other grid.</summary>
        public string? OtherPortName { get; set; }
    }

    [Serializable]
    private enum PersistenceGridKind
    {
        Ship,
        Station,
    }
}