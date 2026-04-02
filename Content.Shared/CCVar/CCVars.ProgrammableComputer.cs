using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<bool> ProgrammableComputerNetworkEnabled =
        CVarDef.Create("programmable_computer.network.enabled", false, CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<string> ProgrammableComputerNetworkAllowList =
        CVarDef.Create("programmable_computer.network.allowlist", "", CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server, min: 100, max: 60000)]
    public static readonly CVarDef<int> ProgrammableComputerNetworkTimeoutMs =
        CVarDef.Create("programmable_computer.network.timeout_ms", 5000, CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server, min: 1, max: 600)]
    public static readonly CVarDef<int> ProgrammableComputerNetworkRateLimitPerMinute =
        CVarDef.Create("programmable_computer.network.rate_limit_per_minute", 20, CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server, min: 1024, max: 1048576)]
    public static readonly CVarDef<int> ProgrammableComputerNetworkMaxResponseBytes =
        CVarDef.Create("programmable_computer.network.max_response_bytes", 131072, CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<bool> ProgrammableComputerNetworkAllowInsecureHttp =
        CVarDef.Create("programmable_computer.network.allow_insecure_http", true, CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<bool> ProgrammableComputerNetworkWebSocketEnabled =
        CVarDef.Create("programmable_computer.network.websocket_enabled", true, CVar.SERVERONLY);

    [CVarControl(AdminFlags.Server, min: 256, max: 262144)]
    public static readonly CVarDef<int> ProgrammableComputerNetworkMaxWebSocketFrameBytes =
        CVarDef.Create("programmable_computer.network.websocket_max_frame_bytes", 8192, CVar.SERVERONLY);
}
