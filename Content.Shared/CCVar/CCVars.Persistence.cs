using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     How often dirty persistence anchors write their grid snapshot to disk.
    /// </summary>
    public static readonly CVarDef<float> PersistenceAnchorAutosaveInterval =
        CVarDef.Create("persistence.anchor_autosave_interval", 600f, CVar.ARCHIVE | CVar.SERVERONLY);
}