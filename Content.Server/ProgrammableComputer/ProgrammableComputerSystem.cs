using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Construction.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.ProgrammableComputer;
using Content.Shared.UserInterface;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;

namespace Content.Server.ProgrammableComputer;

public sealed partial class ProgrammableComputerSystem : EntitySystem
{
    private static readonly TimeSpan BootStageDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan RuntimePackageFetchTimeout = TimeSpan.FromSeconds(10);
    private const string ComputerPlatformVersion = "1.0.0";
    private const int ComputerPlatformAbiMajor = 1;
    private const int ComputerPlatformAbiMinor = 0;
    private const int MinRuntimeRamKiB = 64;
    private const string RuntimePackageName = "monolith-runtime";
    private const int RuntimePackageAbiMajor = ComputerPlatformAbiMajor;
    private static readonly Uri RuntimePackageIndexUrl = new("https://raw.githubusercontent.com/6th-Maroon-Division/monolith-programmable-packages/main/index/index.json");
    private static readonly Uri RuntimePackageRawRootUrl = new("https://raw.githubusercontent.com/6th-Maroon-Division/monolith-programmable-packages/main/");
    private static readonly string[] RuntimePackageRequiredFiles =
    {
        "boot/init.lua",
        "api/path.lua",
        "api/keyboard.lua",
        "api/touch.lua",
    };
    private static readonly ResPath ProgrammableComputerResourceRoot = new("/ProgrammableComputer/");
    private static readonly Color DefaultTerminalForeground = Color.FromHex("#9cffad");
    private static readonly Color DefaultTerminalBackground = Color.FromHex("#070a0c");
    private static readonly Color[] TerminalPalette =
    {
        Color.Black,
        Color.FromHex("#800000"),
        Color.FromHex("#008000"),
        Color.FromHex("#808000"),
        Color.FromHex("#000080"),
        Color.FromHex("#800080"),
        Color.FromHex("#008080"),
        Color.FromHex("#c0c0c0"),
        Color.FromHex("#808080"),
        Color.Red,
        Color.Lime,
        Color.Yellow,
        Color.Blue,
        Color.Magenta,
        Color.Cyan,
        Color.White,
    };

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IHttpClientHolder _http = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IResourceManager _resMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly Dictionary<EntityUid, ComputerRuntime> _runtimes = new();
    private Dictionary<string, string>? _cachedRemoteRuntimeFiles;
    private string? _cachedRemoteRuntimeVersion;
    private string? _cachedPackageIndexJson;

    public override void Initialize()
    {
        SubscribeLocalEvent<ProgrammableComputerComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ProgrammableComputerComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<ProgrammableComputerComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerKeyMessage>(OnKeyInput);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerTouchMessage>(OnTouchInput);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerPowerActionMessage>(OnPowerAction);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerRefreshStateMessage>(OnRefreshState);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerRunCommandMessage>(OnRunCommand);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerRequestFileListMessage>(OnRequestFileList);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerDeleteFileMessage>(OnDeleteFile);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerDownloadFileMessage>(OnDownloadFile);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerUploadFileMessage>(OnUploadFile);
        InitializeAtmos();
        RefreshRuntimePackageCache();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        foreach (var (uid, runtime) in _runtimes)
        {
            if (!TryComp<ProgrammableComputerComponent>(uid, out var component))
                continue;

            if (runtime.IsBooting)
            {
                if (now >= runtime.BootReadyAt)
                    AdvanceBootSequence(uid, component, runtime, now);
                continue;
            }

            if (runtime.IsRunning)
            {
                PumpCoroutineInternal(uid, component, runtime, true,
                    LuaEventArg.FromString("tick"),
                    LuaEventArg.FromNumber(now.TotalSeconds));
            }
        }
    }

    private void OnComponentInit(EntityUid uid, ProgrammableComputerComponent component, ComponentInit args)
    {
        var runtime = EnsureRuntime(uid);
        ShowPoweredOffScreen(runtime);
        UpdateUi(uid, component);
    }

    private void OnComponentShutdown(EntityUid uid, ProgrammableComputerComponent component, ComponentShutdown args)
    {
        if (!_runtimes.Remove(uid, out var runtime))
            return;

        runtime.Dispose();
    }

    private void OnBeforeUiOpen(EntityUid uid, ProgrammableComputerComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnKeyInput(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerKeyMessage args)
    {
        var runtime = EnsureRuntime(uid);
        if (!runtime.IsRunning)
            return;

        var evName = args.Pressed ? "key" : "key_up";
        PumpCoroutine(uid, component, runtime,
            LuaEventArg.FromString(evName),
            LuaEventArg.FromNumber(args.KeyCode),
            LuaEventArg.FromBoolean(args.IsRepeat),
            LuaEventArg.FromBoolean(args.Ctrl),
            LuaEventArg.FromBoolean(args.Alt),
            LuaEventArg.FromBoolean(args.Shift),
            LuaEventArg.FromBoolean(args.Meta),
            LuaEventArg.FromNumber(args.Layout));
    }

    private void OnTouchInput(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerTouchMessage args)
    {
        var runtime = EnsureRuntime(uid);
        if (!runtime.IsRunning)
            return;

        var capabilities = GetCapabilities(uid);
        if (capabilities.GpuTier < 2)
            return;

        PumpCoroutine(uid, component, runtime,
            LuaEventArg.FromString("touch"),
            LuaEventArg.FromNumber(args.X),
            LuaEventArg.FromNumber(args.Y),
            LuaEventArg.FromNumber(1));
    }

    private void OnPowerAction(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerPowerActionMessage args)
    {
        var runtime = EnsureRuntime(uid);

        switch (args.Action)
        {
            case ProgrammableComputerPowerAction.Start:
                if (!runtime.IsPoweredOn && !runtime.IsBooting)
                    StartBootSequence(uid, component, runtime);
                break;
            case ProgrammableComputerPowerAction.Shutdown:
                ShutdownRuntime(uid, component, runtime, "System halted.");
                break;
            case ProgrammableComputerPowerAction.Reboot:
                if (runtime.IsPoweredOn || runtime.IsBooting)
                    StartBootSequence(uid, component, runtime);
                break;
        }
    }

    private void OnRefreshState(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerRefreshStateMessage args)
    {
        UpdateUi(uid, component);
    }

    private void OnRunCommand(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerRunCommandMessage args)
    {
        var runtime = EnsureRuntime(uid);
        if (!runtime.IsRunning)
            return;

        PumpCoroutine(uid, component, runtime,
            LuaEventArg.FromString("run_command"),
            LuaEventArg.FromString(args.Command));
    }

    private void OnRequestFileList(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerRequestFileListMessage args)
    {
        UpdateUi(uid, component);
    }

    private void OnDeleteFile(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerDeleteFileMessage args)
    {
        var runtime = EnsureRuntime(uid);
        runtime.FileSystem.Remove(args.FileName);
        UpdateUi(uid, component);
    }

    private void OnDownloadFile(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerDownloadFileMessage args)
    {
        var runtime = EnsureRuntime(uid);
        if (!runtime.FileSystem.TryRead(args.FileName, out var content))
            return;

        _ui.ServerSendUiMessage(uid, ProgrammableComputerUiKey.Key,
            new ProgrammableComputerFileContentMessage(args.FileName, Encoding.UTF8.GetBytes(content)),
            args.Actor);
    }

    private void OnUploadFile(EntityUid uid, ProgrammableComputerComponent component, ProgrammableComputerUploadFileMessage args)
    {
        var runtime = EnsureRuntime(uid);
        var capabilities = GetCapabilities(uid);
        runtime.FileSystem.TryWrite(args.FileName, Encoding.UTF8.GetString(args.Content), capabilities, out _);
        UpdateUi(uid, component);
    }

    private void StartBootSequence(EntityUid uid, ProgrammableComputerComponent component, ComputerRuntime runtime)
    {
        var capabilities = GetCapabilities(uid);
        var (terminalWidth, terminalHeight) = GetTerminalSizeForGpuTier(capabilities.GpuTier);
        runtime.IsPoweredOn = true;
        runtime.IsBooting = true;
        runtime.IsRunning = false;
        runtime.StartedAt = DateTimeOffset.UtcNow;
        runtime.RamLimitBytes = Math.Max(MinRuntimeRamKiB, capabilities.TotalRamKiB) * 1024;
        runtime.RamUsedBytes = 0;
        runtime.MainThread = null;
        runtime.BootCapabilities = capabilities;
        runtime.BootStage = 0;
        runtime.BootReadyAt = _timing.CurTime + BootStageDelay;
        runtime.Terminal = new TerminalBuffer(terminalWidth, terminalHeight);
        runtime.Terminal.Clear();
        runtime.Terminal.Write("6MD Modular Bios v6.7FU");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("Copyright (C) 1996-2026, 6MD Software, Inc.");
        runtime.Terminal.NewLine();
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("CPU Module   : Detecting...");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("RAM Module   : Detecting...");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("Disk Module  : Detecting...");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("Net Module   : Detecting...");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("GPU Module   : Detecting...");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("Expansion    : Detecting...");

        UpdateUi(uid, component);
    }

    private void AdvanceBootSequence(EntityUid uid, ProgrammableComputerComponent component, ComputerRuntime runtime, TimeSpan now)
    {
        switch (runtime.BootStage)
        {
            case 0:
                runtime.Terminal.SetCursorPos(1, 4);
                runtime.Terminal.ClearLine(4);
                runtime.Terminal.Write($"CPU Module   : {(runtime.BootCapabilities.CpuTier > 0 ? $"Tier {runtime.BootCapabilities.CpuTier} [OK]" : "EMPTY [FAIL]")}");
                runtime.BootStage = 1;
                runtime.BootReadyAt = now + BootStageDelay;
                UpdateUi(uid, component);
                return;

            case 1:
                runtime.Terminal.SetCursorPos(1, 5);
                runtime.Terminal.ClearLine(5);
                runtime.Terminal.Write($"RAM Module   : {(runtime.BootCapabilities.TotalRamKiB > 0 ? $"{runtime.BootCapabilities.RamSlotsInstalled} slot(s), {runtime.BootCapabilities.TotalRamKiB} KB [OK]" : "EMPTY [FAIL]")}");
                runtime.BootStage = 2;
                runtime.BootReadyAt = now + BootStageDelay;
                UpdateUi(uid, component);
                return;

            case 2:
                runtime.Terminal.SetCursorPos(1, 6);
                runtime.Terminal.ClearLine(6);
                runtime.Terminal.Write($"Disk Module  : {(runtime.BootCapabilities.TotalDiskKiB > 0 ? $"{runtime.BootCapabilities.DiskSlotsInstalled} slot(s), {runtime.BootCapabilities.TotalDiskKiB} KB [OK]" : "EMPTY [OPTIONAL]")}");
                runtime.BootStage = 3;
                runtime.BootReadyAt = now + BootStageDelay;
                UpdateUi(uid, component);
                return;

            case 3:
                runtime.Terminal.SetCursorPos(1, 7);
                runtime.Terminal.ClearLine(7);
                runtime.Terminal.Write($"Net Module   : {(runtime.BootCapabilities.NetworkTier > 0 ? $"Tier {runtime.BootCapabilities.NetworkTier} [OK]" : "EMPTY")}");
                runtime.BootStage = 4;
                runtime.BootReadyAt = now + BootStageDelay;
                UpdateUi(uid, component);
                return;

            case 4:
                runtime.Terminal.SetCursorPos(1, 8);
                runtime.Terminal.ClearLine(8);
                runtime.Terminal.Write($"GPU Module   : {(runtime.BootCapabilities.GpuTier > 0 ? $"Tier {runtime.BootCapabilities.GpuTier} [OK]" : "EMPTY [OPTIONAL]")}");
                runtime.BootStage = 5;
                runtime.BootReadyAt = now + BootStageDelay;
                UpdateUi(uid, component);
                return;

            case 5:
                runtime.Terminal.SetCursorPos(1, 9);
                runtime.Terminal.ClearLine(9);
                runtime.Terminal.Write($"Expansion    : {(runtime.BootCapabilities.ExpansionModules > 0 ? $"{runtime.BootCapabilities.ExpansionModules} module(s) [OK]" : "EMPTY")}");
                runtime.Terminal.SetCursorPos(1, 11);
                runtime.Terminal.ClearLine(11);
                if (HasRequiredHardware(runtime.BootCapabilities))
                    runtime.Terminal.Write("POST complete. Initializing Lua VM...");
                else
                    runtime.Terminal.Write($"POST failed. Missing required {GetMissingRequiredHardware(runtime.BootCapabilities)}.");

                runtime.BootStage = 6;
                runtime.BootReadyAt = now + BootStageDelay;
                UpdateUi(uid, component);
                return;

            default:
                if (HasRequiredHardware(runtime.BootCapabilities))
                {
                    BootRuntime(uid, component, runtime);
                }
                else
                {
                    FailBootSequence(uid, component, runtime, $"POST failed. Missing required {GetMissingRequiredHardware(runtime.BootCapabilities)}.");
                }
                return;
        }
    }

    private void BootRuntime(EntityUid uid, ProgrammableComputerComponent component, ComputerRuntime runtime)
    {
        if (!runtime.IsPoweredOn)
            return;

        InitializeMoonSharpScript(uid, runtime);

        runtime.IsBooting = false;

        var defaultStorageCaps = new ComputerCapabilities { MaxFiles = 32, MaxFileSizeKiB = 64, TotalDiskKiB = 256 };
        MountBundledPrograms(runtime, defaultStorageCaps);
        MountRemoteRuntimePrograms(runtime, defaultStorageCaps);

        if (!runtime.FileSystem.TryRead("/boot/init.lua", out var source))
        {
            runtime.Terminal.Write("No /boot/init.lua found.");
            UpdateUi(uid, component);
            return;
        }

        if (!TryLoadMoonSharpMainChunk(runtime, source, out var syntaxError))
        {
            runtime.Terminal.Write("Syntax error in /boot/init.lua:");
            runtime.Terminal.NewLine();
            runtime.Terminal.Write(syntaxError);
            UpdateUi(uid, component);
            return;
        }

        runtime.IsRunning = true;
        runtime.Terminal.Clear();

        // First resume: starts execution until the first event.pull() yield.
        PumpCoroutine(uid, component, runtime);
    }

    private void MountBundledPrograms(ComputerRuntime runtime, ComputerCapabilities capabilities)
    {
        foreach (var resourcePath in _resMan.ContentFindFiles(ProgrammableComputerResourceRoot))
        {
            if (!TryGetVfsPathFromResource(resourcePath, out var vfsPath))
                continue;

            if (runtime.FileSystem.Exists(vfsPath))
                continue;

            EnsureVfsDirectory(runtime.FileSystem, GetVfsParentDirectory(vfsPath));

            if (!TryLoadBundledProgram(resourcePath, out var source))
                continue;

            runtime.FileSystem.TryWrite(vfsPath, source, capabilities, out _);
        }
    }

    private void MountRemoteRuntimePrograms(ComputerRuntime runtime, ComputerCapabilities capabilities)
    {
        if (_cachedRemoteRuntimeFiles == null || _cachedRemoteRuntimeFiles.Count == 0)
            return;

        foreach (var (relativePath, source) in _cachedRemoteRuntimeFiles)
        {
            var vfsPath = "/" + relativePath.TrimStart('/');
            EnsureVfsDirectory(runtime.FileSystem, GetVfsParentDirectory(vfsPath));
            runtime.FileSystem.TryWrite(vfsPath, source, capabilities, out _);
        }
    }

    private void RefreshRuntimePackageCache()
    {
        _cachedRemoteRuntimeFiles = null;
        _cachedRemoteRuntimeVersion = null;
        _cachedPackageIndexJson = null;

        if (!TryFetchPackageIndexJson(out var indexJson, out var error))
        {
            Log.Warning($"Programmable runtime package index fetch failed: {error}");
            return;
        }

        _cachedPackageIndexJson = indexJson;

        if (!TryResolveCompatibleRuntimePackageVersion(indexJson, out var version, out error))
        {
            Log.Warning($"Programmable runtime package index fetch failed: {error}");
            return;
        }

        if (!TryFetchRuntimePackageFiles(version, out var files, out error))
        {
            Log.Warning($"Programmable runtime package fetch failed for version {version}: {error}");
            return;
        }

        _cachedRemoteRuntimeVersion = version;
        _cachedRemoteRuntimeFiles = files;
        Log.Info($"Programmable runtime package cache loaded: {RuntimePackageName} {version} (ABI {RuntimePackageAbiMajor}).");
    }

    private bool TryFetchPackageIndexJson(out string indexJson, out string error)
    {
        return TryHttpGetText(RuntimePackageIndexUrl, out indexJson, out error);
    }

    private bool TryResolveCompatibleRuntimePackageVersion(string indexJson, out string version, out string error)
    {
        version = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(indexJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("packages", out var packages))
            {
                error = "missing packages object";
                return false;
            }

            if (!packages.TryGetProperty(RuntimePackageName, out var runtimePackage))
            {
                error = $"missing package '{RuntimePackageName}'";
                return false;
            }

            if (!runtimePackage.TryGetProperty("versions", out var versionsElement) || versionsElement.ValueKind != JsonValueKind.Array)
            {
                error = "missing versions array";
                return false;
            }

            SemVersion? selected = null;
            foreach (var item in versionsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String)
                    continue;

                var candidateText = versionElement.GetString();
                if (string.IsNullOrWhiteSpace(candidateText) || !TryParseSemVersion(candidateText, out var candidate))
                    continue;

                var minAbi = TryReadOptionalInt(item, "minAbi") ?? candidate.Major;
                var maxAbi = TryReadOptionalInt(item, "maxAbi") ?? candidate.Major;

                if (RuntimePackageAbiMajor < minAbi || RuntimePackageAbiMajor > maxAbi)
                    continue;

                if (selected == null || candidate.CompareTo(selected.Value) > 0)
                    selected = candidate;
            }

            if (selected == null)
            {
                error = $"no compatible version found for ABI major {RuntimePackageAbiMajor}";
                return false;
            }

            version = selected.Value.ToString();
            error = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            error = $"index parse error: {e.Message}";
            return false;
        }
    }

    private bool TryGetCachedPackageIndexDocument(out JsonDocument document, out string error)
    {
        if (string.IsNullOrWhiteSpace(_cachedPackageIndexJson))
        {
            document = default!;
            error = "package index is not cached";
            return false;
        }

        try
        {
            document = JsonDocument.Parse(_cachedPackageIndexJson);
            error = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            document = default!;
            error = $"index parse error: {e.Message}";
            return false;
        }
    }

    private bool TryFetchRuntimePackageFiles(string version, out Dictionary<string, string> files, out string error)
    {
        files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var relative in RuntimePackageRequiredFiles)
        {
            var path = $"packages/{RuntimePackageName}/{version}/{relative}";
            var fileUrl = new Uri(RuntimePackageRawRootUrl, path);

            if (!TryHttpGetText(fileUrl, out var source, out error))
                return false;

            files[relative] = source;
        }

        error = string.Empty;
        return true;
    }

    private bool TryHttpGetText(Uri url, out string text, out string error)
    {
        text = string.Empty;

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            using var cts = new CancellationTokenSource(RuntimePackageFetchTimeout);
            var response = _http.Client.Send(message, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                error = $"HTTP {(int)response.StatusCode} for {url}";
                return false;
            }

            text = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            error = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private static bool TryParseSemVersion(string version, out SemVersion parsed)
    {
        parsed = default;
        var pieces = version.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length != 3)
            return false;

        if (!int.TryParse(pieces[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        parsed = new SemVersion(major, minor, patch);
        return true;
    }

    private bool TryGetPackageNames(out List<string> names, out string error)
    {
        names = new List<string>();

        if (!TryGetCachedPackageIndexDocument(out var document, out error))
            return false;

        using (document)
        {
            if (!document.RootElement.TryGetProperty("packages", out var packages) || packages.ValueKind != JsonValueKind.Object)
            {
                error = "missing packages object";
                return false;
            }

            foreach (var property in packages.EnumerateObject())
                names.Add(property.Name);
        }

        names.Sort(StringComparer.Ordinal);
        error = string.Empty;
        return true;
    }

    private bool TryGetPackageLatestVersion(string packageName, out string version, out string error)
    {
        version = string.Empty;

        if (!TryGetCachedPackageIndexDocument(out var document, out error))
            return false;

        using (document)
        {
            if (!TryGetPackageElement(document.RootElement, packageName, out var packageElement, out error))
                return false;

            if (!packageElement.TryGetProperty("latest", out var latestElement) || latestElement.ValueKind != JsonValueKind.String)
            {
                error = $"package '{packageName}' is missing latest version";
                return false;
            }

            var latest = latestElement.GetString();
            if (string.IsNullOrWhiteSpace(latest))
            {
                error = $"package '{packageName}' latest version is empty";
                return false;
            }

            version = latest;
            error = string.Empty;
            return true;
        }
    }

    private bool TryGetPackageVersionFiles(string packageName, string? requestedVersion, out string resolvedVersion, out List<string> files, out string error)
    {
        resolvedVersion = string.Empty;
        files = new List<string>();

        if (!TryGetCachedPackageIndexDocument(out var document, out error))
            return false;

        using (document)
        {
            if (!TryGetPackageElement(document.RootElement, packageName, out var packageElement, out error))
                return false;

            if (!TryResolvePackageVersion(packageElement, packageName, requestedVersion, out resolvedVersion, out error))
                return false;

            if (!TryGetVersionElement(packageElement, resolvedVersion, out var versionElement, out error))
                return false;

            if (versionElement.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileElement in filesElement.EnumerateArray())
                {
                    if (!fileElement.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
                        continue;

                    var relative = pathElement.GetString();
                    if (string.IsNullOrWhiteSpace(relative))
                        continue;

                    files.Add(relative);
                }
            }

            if (files.Count == 0)
            {
                // Backward compatibility with early index schema.
                files.Add("lib.lua");
                files.Add("dependencies.txt");
                files.Add("meta.yml");
            }

            files = files
                .Where(f => IsSafePackageRelativePath(f))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            if (files.Count == 0)
            {
                error = $"package '{packageName}' version '{resolvedVersion}' has no valid files";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    private static bool TryGetPackageElement(JsonElement root, string packageName, out JsonElement packageElement, out string error)
    {
        packageElement = default;

        if (!root.TryGetProperty("packages", out var packages) || packages.ValueKind != JsonValueKind.Object)
        {
            error = "missing packages object";
            return false;
        }

        if (!packages.TryGetProperty(packageName, out packageElement) || packageElement.ValueKind != JsonValueKind.Object)
        {
            error = $"package '{packageName}' not found";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryResolvePackageVersion(JsonElement packageElement, string packageName, string? requestedVersion, out string resolvedVersion, out string error)
    {
        resolvedVersion = string.Empty;

        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            resolvedVersion = requestedVersion.Trim();
            error = string.Empty;
            return true;
        }

        if (!packageElement.TryGetProperty("latest", out var latestElement) || latestElement.ValueKind != JsonValueKind.String)
        {
            error = $"package '{packageName}' is missing latest version";
            return false;
        }

        var latest = latestElement.GetString();
        if (string.IsNullOrWhiteSpace(latest))
        {
            error = $"package '{packageName}' latest version is empty";
            return false;
        }

        resolvedVersion = latest;
        error = string.Empty;
        return true;
    }

    private static bool TryGetVersionElement(JsonElement packageElement, string version, out JsonElement versionElement, out string error)
    {
        versionElement = default;

        if (!packageElement.TryGetProperty("versions", out var versionsElement) || versionsElement.ValueKind != JsonValueKind.Array)
        {
            error = "missing versions array";
            return false;
        }

        foreach (var candidate in versionsElement.EnumerateArray())
        {
            if (!candidate.TryGetProperty("version", out var versionProperty) || versionProperty.ValueKind != JsonValueKind.String)
                continue;

            if (string.Equals(versionProperty.GetString(), version, StringComparison.Ordinal))
            {
                versionElement = candidate;
                error = string.Empty;
                return true;
            }
        }

        error = $"version '{version}' not found";
        return false;
    }

    private static bool IsSafePackageRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (path.StartsWith('/') || path.StartsWith('\\'))
            return false;

        var normalized = path.Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part == "." || part == "..")
                return false;
        }

        return true;
    }

    private bool TryFetchPackageFile(string packageName, string version, string relativePath, out string content, out string error)
    {
        content = string.Empty;

        if (!IsSafePackageRelativePath(relativePath))
        {
            error = "invalid package path";
            return false;
        }

        var path = $"packages/{packageName}/{version}/{relativePath}";
        var fileUrl = new Uri(RuntimePackageRawRootUrl, path);
        return TryHttpGetText(fileUrl, out content, out error);
    }

    private static int? TryReadOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var numberValue) => numberValue,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var stringValue) => stringValue,
            _ => null,
        };
    }

    private static bool TryGetVfsPathFromResource(ResPath resourcePath, out string vfsPath)
    {
        var root = ProgrammableComputerResourceRoot.ToString();
        var full = resourcePath.ToString();
        if (!full.StartsWith(root, StringComparison.Ordinal))
        {
            vfsPath = string.Empty;
            return false;
        }

        var relative = full[root.Length..].TrimStart('/');
        if (relative.Length == 0)
        {
            vfsPath = string.Empty;
            return false;
        }

        vfsPath = "/" + relative;
        return true;
    }

    private bool TryLoadBundledProgram(ResPath resourcePath, out string source)
    {
        source = string.Empty;

        if (!_resMan.TryContentFileRead(resourcePath, out var stream))
            return false;

        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        source = reader.ReadToEnd();
        return true;
    }

    private static void EnsureVfsDirectory(VirtualFileSystem fs, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            return;

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;
        foreach (var part in parts)
        {
            current += "/" + part;
            fs.TryCreateDirectory(current);
        }
    }

    private static string GetVfsParentDirectory(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx <= 0 ? "/" : path[..idx];
    }

    private static bool HasRequiredHardware(ComputerCapabilities capabilities)
    {
        return capabilities.CpuTier > 0
               && capabilities.TotalRamKiB > 0;
    }

    private static (int Width, int Height) GetTerminalSizeForGpuTier(int gpuTier)
    {
        return gpuTier switch
        {
            >= 3 => (100, 30),
            >= 2 => (80, 25),
            _ => (ProgrammableComputerComponent.TerminalWidth, ProgrammableComputerComponent.TerminalHeight),
        };
    }

    private static string GetMissingRequiredHardware(ComputerCapabilities capabilities)
    {
        var missingCpu = capabilities.CpuTier <= 0;
        var missingRam = capabilities.TotalRamKiB <= 0;

        if (missingCpu && missingRam)
            return "CPU and RAM";

        if (missingCpu)
            return "CPU";

        if (missingRam)
            return "RAM";

        return "hardware";
    }

    private void PumpCoroutine(EntityUid uid, ProgrammableComputerComponent component, ComputerRuntime runtime, params LuaEventArg[] args)
    {
        PumpCoroutineInternal(uid, component, runtime, true, args);
    }

    private void PumpCoroutineInternal(EntityUid uid, ProgrammableComputerComponent component, ComputerRuntime runtime, bool updateUi, params LuaEventArg[] args)
    {
        if (runtime.MainThread == null)
            return;

        if (TryResumeMoonSharp(runtime, args, out var isDead, out var error))
        {
            if (isDead)
            {
                ShutdownRuntime(uid, component, runtime, "[Program ended]");
                return;
            }
        }
        else
        {
            runtime.IsRunning = false;
            var x = runtime.Terminal.CursorX;
            if (x > 1)
                runtime.Terminal.NewLine();
            runtime.Terminal.Write(error);
        }

        if (updateUi || !runtime.IsRunning)
            UpdateUi(uid, component);
    }

    private void ShutdownRuntime(EntityUid uid, ProgrammableComputerComponent component, ComputerRuntime runtime, string message)
    {
        runtime.IsRunning = false;
        runtime.IsBooting = false;
        runtime.IsPoweredOn = false;
        runtime.MainThread = null;
        runtime.Terminal.Clear();
        runtime.Terminal.Write(message);
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("Press Start to power on.");
        UpdateUi(uid, component);
    }

    private void FailBootSequence(EntityUid uid, ProgrammableComputerComponent component, ComputerRuntime runtime, string message)
    {
        runtime.IsRunning = false;
        runtime.IsBooting = false;
        runtime.IsPoweredOn = false;
        runtime.MainThread = null;

        runtime.Terminal.SetCursorPos(1, 13);
        runtime.Terminal.ClearLine(13);
        runtime.Terminal.Write("Press Start to power on.");

        UpdateUi(uid, component);
    }

    private static void ShowPoweredOffScreen(ComputerRuntime runtime)
    {
        runtime.IsRunning = false;
        runtime.IsBooting = false;
        runtime.IsPoweredOn = false;
        runtime.MainThread = null;
        runtime.Terminal.Clear();
        runtime.Terminal.Write("Programmable computer is powered off.");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write("Press Start to power on.");
    }

    private void UpdateUi(EntityUid uid, ProgrammableComputerComponent component)
    {
        if (!_runtimes.TryGetValue(uid, out var runtime))
            return;

        var ramAvailableKiB = runtime.RamLimitBytes / 1024;
        var diskAvailableKiB = runtime.BootCapabilities.TotalDiskKiB;

        var cells = runtime.Terminal.GetCells();
        var state = new ProgrammableComputerBoundUserInterfaceState(
            cells,
            string.Empty,
            string.Empty,
            string.Empty,
            runtime.Terminal.Width,
            runtime.Terminal.Height,
            runtime.Terminal.CursorX,
            runtime.Terminal.CursorY,
            runtime.Terminal.CursorBlink && runtime.IsPoweredOn,
            runtime.IsPoweredOn,
            runtime.IsBooting,
            ramAvailableKiB,
            diskAvailableKiB,
            BuildAtmosLinkEntries(uid),
            BuildAtmosNearbyEntries(uid),
            BuildFileEntries(runtime));

        _ui.SetUiState(uid, ProgrammableComputerUiKey.Key, state);
    }

    public void RefreshUi(EntityUid uid)
    {
        if (!TryComp<ProgrammableComputerComponent>(uid, out var comp))
            return;

        UpdateUi(uid, comp);
    }

    private ProgrammableComputerFileEntry[] BuildFileEntries(ComputerRuntime runtime)
    {
        var now = DateTime.UtcNow;
        return runtime.FileSystem.EnumerateFiles()
            .Select(kvp => new ProgrammableComputerFileEntry(kvp.Key, (uint)Encoding.UTF8.GetByteCount(kvp.Value), now))
            .ToArray();
    }

    private ComputerRuntime EnsureRuntime(EntityUid uid)
    {
        if (_runtimes.TryGetValue(uid, out var existing))
            return existing;

        var runtime = CreateRuntime(uid);
        _runtimes[uid] = runtime;
        return runtime;
    }

    private ComputerRuntime CreateRuntime(EntityUid uid)
    {
        return new ComputerRuntime();
    }

    // ─── MoonSharp initialization and API registration ──────────────────────

    private void InitializeMoonSharpScript(EntityUid uid, ComputerRuntime runtime)
    {
        var script = new Script(CoreModules.Preset_HardSandbox | CoreModules.Coroutine | CoreModules.ErrorHandling | CoreModules.LoadMethods);
        script.Options.CheckThreadAccess = false;
        script.Options.ScriptLoader = new DenyAllScriptLoader();

        runtime.Script = script;
        RegisterMoonSharpApi(uid, runtime, script);
    }

    private void RegisterMoonSharpApi(EntityUid uid, ComputerRuntime runtime, Script script)
    {
        var hostTable = new Table(script);

        // Terminal API
        hostTable.Set("term_write", DynValue.NewCallback((ctx, args) =>
        {
            var text = args.Count > 0 ? ToDynString(args[0]) : string.Empty;
            runtime.Terminal.Write(text);
            return DynValue.Void;
        }, "term_write"));

        hostTable.Set("term_write_line", DynValue.NewCallback((ctx, args) =>
        {
            var text = args.Count > 0 ? ToDynString(args[0]) : string.Empty;
            runtime.Terminal.Write(text);
            runtime.Terminal.NewLine();
            return DynValue.Void;
        }, "term_write_line"));

        hostTable.Set("term_new_line", DynValue.NewCallback((ctx, args) =>
        {
            runtime.Terminal.NewLine();
            return DynValue.Void;
        }, "term_new_line"));

        hostTable.Set("term_clear", DynValue.NewCallback((ctx, args) =>
        {
            runtime.Terminal.Clear();
            return DynValue.Void;
        }, "term_clear"));

        hostTable.Set("term_clear_line", DynValue.NewCallback((ctx, args) =>
        {
            runtime.Terminal.ClearLine(runtime.Terminal.CursorY);
            return DynValue.Void;
        }, "term_clear_line"));

        hostTable.Set("term_set_cursor_pos", DynValue.NewCallback((ctx, args) =>
        {
            var x = args.Count > 0 ? (int)args[0].Number : 1;
            var y = args.Count > 1 ? (int)args[1].Number : 1;
            runtime.Terminal.SetCursorPos(x, y);
            return DynValue.Void;
        }, "term_set_cursor_pos"));

        hostTable.Set("term_get_cursor_pos", DynValue.NewCallback((ctx, args) =>
        {
            return DynValue.NewTuple(DynValue.NewNumber(runtime.Terminal.CursorX), DynValue.NewNumber(runtime.Terminal.CursorY));
        }, "term_get_cursor_pos"));

        hostTable.Set("term_get_size", DynValue.NewCallback((ctx, args) =>
        {
            return DynValue.NewTuple(DynValue.NewNumber(runtime.Terminal.Width), DynValue.NewNumber(runtime.Terminal.Height));
        }, "term_get_size"));

        hostTable.Set("term_set_cursor_blink", DynValue.NewCallback((ctx, args) =>
        {
            if (GetCapabilities(uid).GpuTier < 1)
                throw new ScriptRuntimeException("term.setCursorBlink requires GPU tier 1+");

            runtime.Terminal.CursorBlink = args.Count > 0 && IsTruthy(args[0]);
            return DynValue.Void;
        }, "term_set_cursor_blink"));

        hostTable.Set("term_set_text_color", DynValue.NewCallback((ctx, args) =>
        {
            if (GetCapabilities(uid).GpuTier < 1)
                throw new ScriptRuntimeException("term.setTextColor requires GPU tier 1+");

            if (args.Count == 0 || !TryResolveTerminalColor(args[0], out var color))
                throw new ScriptRuntimeException("term.setTextColor(color) requires a palette index or #RRGGBB value");

            runtime.Terminal.SetTextColor(color);
            return DynValue.Void;
        }, "term_set_text_color"));

        hostTable.Set("term_set_background_color", DynValue.NewCallback((ctx, args) =>
        {
            if (GetCapabilities(uid).GpuTier < 1)
                throw new ScriptRuntimeException("term.setBackgroundColor requires GPU tier 1+");

            if (args.Count == 0 || !TryResolveTerminalColor(args[0], out var color))
                throw new ScriptRuntimeException("term.setBackgroundColor(color) requires a palette index or #RRGGBB value");

            runtime.Terminal.SetBackgroundColor(color);
            return DynValue.Void;
        }, "term_set_background_color"));

        hostTable.Set("term_get_text_color", DynValue.NewCallback((ctx, args) =>
        {
            if (GetCapabilities(uid).GpuTier < 1)
                return DynValue.NewString(DefaultTerminalForeground.ToHex());

            return DynValue.NewString(runtime.Terminal.ForegroundColor.ToHex());
        }, "term_get_text_color"));

        hostTable.Set("term_get_background_color", DynValue.NewCallback((ctx, args) =>
        {
            if (GetCapabilities(uid).GpuTier < 1)
                return DynValue.NewString(DefaultTerminalBackground.ToHex());

            return DynValue.NewString(runtime.Terminal.BackgroundColor.ToHex());
        }, "term_get_background_color"));

        hostTable.Set("term_reset_colors", DynValue.NewCallback((ctx, args) =>
        {
            if (GetCapabilities(uid).GpuTier < 1)
                throw new ScriptRuntimeException("term.resetColors requires GPU tier 1+");

            runtime.Terminal.ResetColors();
            return DynValue.Void;
        }, "term_reset_colors"));

        hostTable.Set("term_scroll", DynValue.NewCallback((ctx, args) =>
        {
            var n = args.Count > 0 ? (int)args[0].Number : 1;
            runtime.Terminal.Scroll(n);
            return DynValue.Void;
        }, "term_scroll"));

        // Computer API
        hostTable.Set("computer_tier", DynValue.NewCallback((ctx, args) =>
        {
            var capabilities = GetCapabilities(uid);
            return DynValue.NewTuple(
                DynValue.NewNumber(capabilities.CpuTier),
                DynValue.NewNumber(capabilities.TotalRamKiB),
                DynValue.NewNumber(capabilities.TotalDiskKiB),
                DynValue.NewNumber(capabilities.NetworkTier),
                DynValue.NewNumber(capabilities.GpuTier),
                DynValue.NewNumber(capabilities.ExpansionModules)
            );
        }, "computer_tier"));

        hostTable.Set("computer_limits", DynValue.NewCallback((ctx, args) =>
        {
            var capabilities = GetCapabilities(uid);
            return DynValue.NewTuple(
                DynValue.NewNumber(capabilities.InstructionBudget),
                DynValue.NewNumber(capabilities.TimeSliceMs),
                DynValue.NewNumber(capabilities.TotalRamKiB),
                DynValue.NewNumber(capabilities.TotalDiskKiB),
                DynValue.NewNumber(capabilities.MaxFiles),
                DynValue.NewNumber(capabilities.MaxFileSizeKiB)
            );
        }, "computer_limits"));

        hostTable.Set("computer_version", DynValue.NewCallback((ctx, args) =>
        {
            var runtimePackageVersion = _cachedRemoteRuntimeVersion ?? "bundled";
            var runtimePackageSource = _cachedRemoteRuntimeVersion == null ? "bundled" : "remote";

            return DynValue.NewTuple(
                DynValue.NewString(ComputerPlatformVersion),
                DynValue.NewNumber(ComputerPlatformAbiMajor),
                DynValue.NewNumber(ComputerPlatformAbiMinor),
                DynValue.NewString(runtimePackageVersion),
                DynValue.NewString(runtimePackageSource)
            );
        }, "computer_version"));

        hostTable.Set("computer_exec", DynValue.NewCallback((ctx, args) =>
        {
            var source = args.Count > 0 ? ToDynString(args[0]) : string.Empty;
            var countAsTransient = args.Count <= 1 || IsTruthy(args[1]);
            var sourceBytes = Encoding.UTF8.GetByteCount(source);

            if (countAsTransient && !TryReserveRam(runtime, sourceBytes, out var oomError))
            {
                CrashRuntimeForOutOfMemory(uid, runtime, oomError);
                return DynValue.NewTuple(DynValue.NewBoolean(false), DynValue.NewBoolean(false), DynValue.NewString(string.Empty), DynValue.NewString(oomError));
            }

            try
            {
                TryExecuteMoonSharpSnippet(runtime, source, out var hasResult, out var resultText, out var execError);
                return DynValue.NewTuple(
                    DynValue.NewBoolean(string.IsNullOrEmpty(execError)),
                    DynValue.NewBoolean(hasResult),
                    DynValue.NewString(hasResult ? resultText : string.Empty),
                    DynValue.NewString(execError)
                );
            }
            finally
            {
                if (countAsTransient)
                    ReleaseRam(runtime, sourceBytes);
            }
        }, "computer_exec"));

        hostTable.Set("computer_reboot", DynValue.NewCallback((ctx, args) =>
        {
            if (TryComp<ProgrammableComputerComponent>(uid, out var comp))
                StartBootSequence(uid, comp, runtime);
            return DynValue.Void;
        }, "computer_reboot"));

        hostTable.Set("computer_shutdown", DynValue.NewCallback((ctx, args) =>
        {
            if (TryComp<ProgrammableComputerComponent>(uid, out var comp))
                ShutdownRuntime(uid, comp, runtime, "System halted by Lua.");
            return DynValue.Void;
        }, "computer_shutdown"));

        // File system API
        hostTable.Set("fs_list", DynValue.NewCallback((ctx, args) =>
        {
            var path = args.Count > 0 ? ToDynString(args[0]) : "/";
            if (!runtime.FileSystem.TryListDirectory(path, out var entries, out _))
                return DynValue.Nil;

            var table = new Table(script);
            var index = 1;
            foreach (var entry in entries)
            {
                table.Set(DynValue.NewNumber(index++), DynValue.NewString(entry));
            }
            return DynValue.NewTable(table);
        }, "fs_list"));

        hostTable.Set("fs_read", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.Nil;

            return runtime.FileSystem.TryRead(ToDynString(args[0]), out var text)
                ? DynValue.NewString(text)
                : DynValue.Nil;
        }, "fs_read"));

        hostTable.Set("fs_list_ex", DynValue.NewCallback((ctx, args) =>
        {
            var path = args.Count > 0 ? ToDynString(args[0]) : "/";
            if (!runtime.FileSystem.TryListDirectory(path, out var entries, out var error))
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(error));

            var table = new Table(script);
            var index = 1;
            foreach (var entry in entries)
            {
                table.Set(DynValue.NewNumber(index++), DynValue.NewString(entry));
            }

            return DynValue.NewTuple(DynValue.NewTable(table), DynValue.Nil);
        }, "fs_list_ex"));

        hostTable.Set("fs_read_ex", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("path is required"));

            return runtime.FileSystem.TryRead(ToDynString(args[0]), out var text)
                ? DynValue.NewTuple(DynValue.NewString(text), DynValue.Nil)
                : DynValue.NewTuple(DynValue.Nil, DynValue.NewString("File not found."));
        }, "fs_read_ex"));

        hostTable.Set("fs_write", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2)
                return DynValue.NewBoolean(false);

            var caps = GetCapabilities(uid);
            var ok = runtime.FileSystem.TryWrite(ToDynString(args[0]), ToDynString(args[1]), caps, out var _);
            return DynValue.NewBoolean(ok);
        }, "fs_write"));

        hostTable.Set("fs_write_ex", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("path and text are required"));

            var caps = GetCapabilities(uid);
            var ok = runtime.FileSystem.TryWrite(ToDynString(args[0]), ToDynString(args[1]), caps, out var error);
            return ok
                ? DynValue.NewTuple(DynValue.NewBoolean(true), DynValue.Nil)
                : DynValue.NewTuple(DynValue.Nil, DynValue.NewString(error));
        }, "fs_write_ex"));

        hostTable.Set("fs_mkdir", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewBoolean(false);
            return DynValue.NewBoolean(runtime.FileSystem.TryCreateDirectory(ToDynString(args[0])));
        }, "fs_mkdir"));

        hostTable.Set("fs_mkdir_ex", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("path is required"));

            var path = ToDynString(args[0]);
            var ok = runtime.FileSystem.TryCreateDirectory(path);
            return ok
                ? DynValue.NewTuple(DynValue.NewBoolean(true), DynValue.Nil)
                : DynValue.NewTuple(DynValue.Nil, DynValue.NewString("Parent directory does not exist."));
        }, "fs_mkdir_ex"));

        hostTable.Set("fs_remove", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewBoolean(false);
            return DynValue.NewBoolean(runtime.FileSystem.TryRemove(ToDynString(args[0]), out _));
        }, "fs_remove"));

        hostTable.Set("fs_remove_ex", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("path is required"));

            var ok = runtime.FileSystem.TryRemove(ToDynString(args[0]), out var error);
            return ok
                ? DynValue.NewTuple(DynValue.NewBoolean(true), DynValue.Nil)
                : DynValue.NewTuple(DynValue.Nil, DynValue.NewString(error));
        }, "fs_remove_ex"));

        hostTable.Set("fs_exists", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewBoolean(false);
            return DynValue.NewBoolean(runtime.FileSystem.Exists(ToDynString(args[0])));
        }, "fs_exists"));

        // OS API
        hostTable.Set("os_clock", DynValue.NewCallback((ctx, args) =>
        {
            return DynValue.NewNumber((DateTimeOffset.UtcNow - runtime.StartedAt).TotalSeconds);
        }, "os_clock"));

        hostTable.Set("os_time_now", DynValue.NewCallback((ctx, args) =>
        {
            return DynValue.NewNumber(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }, "os_time_now"));

        hostTable.Set("os_time_from_table", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0 || args[0].Type != DataType.Table)
                throw new ScriptRuntimeException("os.time([table]) expects a table");

            var table = args[0].Table;
            var year = table.Get("year").Number;
            var month = table.Get("month").Type == DataType.Nil ? 1 : table.Get("month").Number;
            var day = table.Get("day").Type == DataType.Nil ? 1 : table.Get("day").Number;
            var hour = table.Get("hour").Type == DataType.Nil ? 12 : table.Get("hour").Number;
            var min = table.Get("min").Type == DataType.Nil ? 0 : table.Get("min").Number;
            var sec = table.Get("sec").Type == DataType.Nil ? 0 : table.Get("sec").Number;

            var value = new DateTimeOffset((int)year, (int)month, (int)day, (int)hour, (int)min, (int)sec, TimeSpan.Zero);
            return DynValue.NewNumber(value.ToUnixTimeSeconds());
        }, "os_time_from_table"));

        hostTable.Set("os_difftime", DynValue.NewCallback((ctx, args) =>
        {
            var left = args.Count > 0 ? args[0].Number : 0;
            var right = args.Count > 1 ? args[1].Number : 0;
            return DynValue.NewNumber(left - right);
        }, "os_difftime"));

        hostTable.Set("os_date", DynValue.NewCallback((ctx, args) =>
        {
            var format = args.Count > 0 ? ToDynString(args[0]) : "%c";
            var timestamp = args.Count > 1 ? (long)args[1].Number : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;

            if (format == "*t")
            {
                var resultTable = new Table(script);
                resultTable.Set("year", DynValue.NewNumber(date.Year));
                resultTable.Set("month", DynValue.NewNumber(date.Month));
                resultTable.Set("day", DynValue.NewNumber(date.Day));
                resultTable.Set("hour", DynValue.NewNumber(date.Hour));
                resultTable.Set("min", DynValue.NewNumber(date.Minute));
                resultTable.Set("sec", DynValue.NewNumber(date.Second));
                resultTable.Set("wday", DynValue.NewNumber((int)date.DayOfWeek + 1));
                resultTable.Set("yday", DynValue.NewNumber(date.DayOfYear));
                return DynValue.NewTable(resultTable);
            }

            var rendered = format
                .Replace("%Y", date.Year.ToString("D4"))
                .Replace("%m", date.Month.ToString("D2"))
                .Replace("%d", date.Day.ToString("D2"))
                .Replace("%H", date.Hour.ToString("D2"))
                .Replace("%M", date.Minute.ToString("D2"))
                .Replace("%S", date.Second.ToString("D2"))
                .Replace("%c", date.ToString("u"));
            return DynValue.NewString(rendered);
        }, "os_date"));

        hostTable.Set("os_remove", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("os.remove(path) requires a path"));

            var path = ToDynString(args[0]);
            return runtime.FileSystem.TryRemove(path, out var removeError)
                ? DynValue.NewTuple(DynValue.NewBoolean(true), DynValue.Nil)
                : DynValue.NewTuple(DynValue.Nil, DynValue.NewString(removeError));
        }, "os_remove"));

        hostTable.Set("os_rename", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("os.rename(old, new) requires two paths"));

            var oldPath = ToDynString(args[0]);
            var newPath = ToDynString(args[1]);
            if (!runtime.FileSystem.TryRead(oldPath, out var text))
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("cannot open " + oldPath));

            var caps = GetCapabilities(uid);
            if (!runtime.FileSystem.TryWrite(newPath, text, caps, out var writeError))
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(writeError));

            runtime.FileSystem.TryRemove(oldPath, out _);
            return DynValue.NewTuple(DynValue.NewBoolean(true), DynValue.Nil);
        }, "os_rename"));

        hostTable.Set("os_execute", DynValue.NewCallback((ctx, args) =>
        {
            return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("disabled in sandbox"), DynValue.NewNumber(0));
        }, "os_execute"));

        hostTable.Set("os_exit", DynValue.NewCallback((ctx, args) =>
        {
            if (TryComp<ProgrammableComputerComponent>(uid, out var comp))
                ShutdownRuntime(uid, comp, runtime, "System halted by Lua.");
            return DynValue.Void;
        }, "os_exit"));

        // Network API
        hostTable.Set("net_is_available", DynValue.NewCallback((ctx, args) =>
        {
            var caps = GetCapabilities(uid);
            var available = IsNetworkAllowed(caps, out var reason);
            return DynValue.NewTuple(DynValue.NewBoolean(available), DynValue.NewString(reason));
        }, "net_is_available"));

        hostTable.Set("net_get", DynValue.NewCallback((ctx, args) =>
        {
            var url = args.Count > 0 ? ToDynString(args[0]) : string.Empty;
            var response = ExecuteHttpRequest(uid, runtime, new HttpRequestSpec { Method = "GET", Url = url });
            return DynValue.NewTuple(
                DynValue.NewBoolean(response.Ok),
                DynValue.NewNumber(response.Status),
                DynValue.NewString(response.Body),
                DynValue.NewString(response.Error ?? string.Empty)
            );
        }, "net_get"));

        hostTable.Set("net_post", DynValue.NewCallback((ctx, args) =>
        {
            var url = args.Count > 0 ? ToDynString(args[0]) : string.Empty;
            var body = args.Count > 1 ? ToDynString(args[1]) : string.Empty;
            var response = ExecuteHttpRequest(uid, runtime, new HttpRequestSpec { Method = "POST", Url = url, Body = body });
            return DynValue.NewTuple(
                DynValue.NewBoolean(response.Ok),
                DynValue.NewNumber(response.Status),
                DynValue.NewString(response.Body),
                DynValue.NewString(response.Error ?? string.Empty)
            );
        }, "net_post"));

        hostTable.Set("net_request", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0 || args[0].Type != DataType.Table)
                throw new ScriptRuntimeException("net.request(options) requires an options table");

            var options = args[0].Table;
            var url = options.Get("url").Type == DataType.Nil ? string.Empty : ToDynString(options.Get("url"));
            var method = options.Get("method").Type == DataType.Nil ? "GET" : ToDynString(options.Get("method"));
            var body = options.Get("body").Type == DataType.Nil ? null : ToDynString(options.Get("body"));
            var response = ExecuteHttpRequest(uid, runtime, new HttpRequestSpec { Url = url, Method = method, Body = body });
            return DynValue.NewTuple(
                DynValue.NewBoolean(response.Ok),
                DynValue.NewNumber(response.Status),
                DynValue.NewString(response.Body),
                DynValue.NewString(response.Error ?? string.Empty)
            );
        }, "net_request"));

        hostTable.Set("net_ws", DynValue.NewCallback((ctx, args) =>
        {
            var url = args.Count > 0 ? ToDynString(args[0]) : string.Empty;
            return DynValue.NewNumber(ConnectWebSocket(uid, runtime, url));
        }, "net_ws"));

        hostTable.Set("net_send", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2)
                return DynValue.NewBoolean(false);
            SendWebSocket(runtime, (int)args[0].Number, ToDynString(args[1]));
            return DynValue.NewBoolean(true);
        }, "net_send"));

        hostTable.Set("net_receive", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.Nil;
            var timeout = args.Count > 1 ? (int)args[1].Number : _cfg.GetCVar(CCVars.ProgrammableComputerNetworkTimeoutMs);
            var message = ReceiveWebSocket(runtime, (int)args[0].Number, timeout);
            return message == null ? DynValue.Nil : DynValue.NewString(message);
        }, "net_receive"));

        hostTable.Set("net_close", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count > 0)
                CloseWebSocket(runtime, (int)args[0].Number);
            return DynValue.NewBoolean(true);
        }, "net_close"));

        // Package manager API
        hostTable.Set("pkg_list", DynValue.NewCallback((ctx, args) =>
        {
            if (!TryGetPackageNames(out var names, out var error))
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(error));

            var table = new Table(script);
            var index = 1;
            foreach (var name in names)
                table.Set(DynValue.NewNumber(index++), DynValue.NewString(name));

            return DynValue.NewTuple(DynValue.NewTable(table), DynValue.Nil);
        }, "pkg_list"));

        hostTable.Set("pkg_latest", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("package name is required"));

            var packageName = ToDynString(args[0]);
            if (!TryGetPackageLatestVersion(packageName, out var version, out var error))
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(error));

            return DynValue.NewTuple(DynValue.NewString(version), DynValue.Nil);
        }, "pkg_latest"));

        hostTable.Set("pkg_files", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("package name is required"));

            var packageName = ToDynString(args[0]);
            var requestedVersion = args.Count > 1 && args[1].Type != DataType.Nil
                ? ToDynString(args[1])
                : null;

            if (!TryGetPackageVersionFiles(packageName, requestedVersion, out var resolvedVersion, out var files, out var error))
                return DynValue.NewTuple(DynValue.Nil, DynValue.Nil, DynValue.NewString(error));

            var fileTable = new Table(script);
            for (var i = 0; i < files.Count; i++)
                fileTable.Set(DynValue.NewNumber(i + 1), DynValue.NewString(files[i]));

            return DynValue.NewTuple(DynValue.NewString(resolvedVersion), DynValue.NewTable(fileTable), DynValue.Nil);
        }, "pkg_files"));

        hostTable.Set("pkg_fetch", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 3)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("package, version, and path are required"));

            var packageName = ToDynString(args[0]);
            var version = ToDynString(args[1]);
            var relativePath = ToDynString(args[2]);

            if (!TryFetchPackageFile(packageName, version, relativePath, out var content, out var error))
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(error));

            return DynValue.NewTuple(DynValue.NewString(content), DynValue.Nil);
        }, "pkg_fetch"));

        RegisterAtmosApi(uid, runtime, hostTable);

        script.Globals.Set("__host", DynValue.NewTable(hostTable));

        // Initialize Lua standard library functions
        script.DoString("""
local h = __host

event = {}
function event.pull(filter)
  while true do
    local result = {coroutine.yield()}
    if filter == nil or result[1] == filter then
      return table.unpack(result)
    end
  end
end

term = {}
function term.write(x) h.term_write(tostring(x or "")) end
function term.writeLine(x) h.term_write_line(tostring(x or "")) end
function term.newLine() h.term_new_line() end
function term.clear() h.term_clear() end
function term.clearLine() h.term_clear_line() end
function term.setCursorPos(x, y) h.term_set_cursor_pos(x, y) end
function term.getCursorPos() return h.term_get_cursor_pos() end
function term.getSize() return h.term_get_size() end
function term.setCursorBlink(v) h.term_set_cursor_blink(v) end
function term.setTextColor(v) h.term_set_text_color(v) end
function term.setBackgroundColor(v) h.term_set_background_color(v) end
function term.getTextColor() return h.term_get_text_color() end
function term.getBackgroundColor() return h.term_get_background_color() end
function term.resetColors() h.term_reset_colors() end
function term.scroll(n) h.term_scroll(n) end

colors = {
  black = 0, maroon = 1, green = 2, olive = 3, navy = 4, purple = 5, teal = 6, silver = 7,
  gray = 8, red = 9, lime = 10, yellow = 11, blue = 12, magenta = 13, cyan = 14, white = 15
}
term.colors = colors

function print(...)
  local parts = {}
  for i = 1, select('#', ...) do
    parts[i] = tostring(select(i, ...))
  end
  term.writeLine(table.concat(parts, '\t'))
end

computer = {}
function computer.tier()
    local cpuTier, ramKiB, diskKiB, networkTier, gpuTier, expansion = h.computer_tier()
    return { cpuTier = cpuTier, ramKiB = ramKiB, diskKiB = diskKiB, networkTier = networkTier, gpuTier = gpuTier, expansion = expansion }
end
function computer.limits()
  local instructionBudget, timeSliceMs, memoryKiB, diskKiB, maxFiles, maxFileSizeKiB = h.computer_limits()
  return { instructionBudget = instructionBudget, timeSliceMs = timeSliceMs, memoryKiB = memoryKiB, diskKiB = diskKiB, maxFiles = maxFiles, maxFileSizeKiB = maxFileSizeKiB }
end
function computer.version()
    local platform, abiMajor, abiMinor, runtimePackageVersion, runtimePackageSource = h.computer_version()
    return {
        platform = platform,
        abiMajor = abiMajor,
        abiMinor = abiMinor,
        runtimePackageVersion = runtimePackageVersion,
        runtimePackageSource = runtimePackageSource,
    }
end
function computer.exec(source, isTransient)
  local ok, hasResult, result, err = h.computer_exec(source, isTransient ~= false)
  return { ok = ok, hasResult = hasResult, result = result or "", error = err or "" }
end
function computer.reboot() h.computer_reboot() end
function computer.shutdown() h.computer_shutdown() end

fs = {}
function fs.list(path)
    local entries, err = h.fs_list_ex(path or "/")
    if entries == nil then return nil, err end
    return entries
end
function fs.read(path)
    local text, err = h.fs_read_ex(path)
    if text == nil then return nil, err end
    return text
end
function fs.write(path, text)
    local ok, err = h.fs_write_ex(path, text)
    if ok == nil then return nil, err end
    return true
end
function fs.mkdir(path)
    local ok, err = h.fs_mkdir_ex(path)
    if ok == nil then return nil, err end
    return true
end
function fs.remove(path)
    local ok, err = h.fs_remove_ex(path)
    if ok == nil then return nil, err end
    return true
end
function fs.exists(path) return h.fs_exists(path) end

io = {}
function io.open(path, mode)
  mode = mode or "r"
  local canRead = mode:find("r") or mode:find("+")
  local canWrite = mode:find("w") or mode:find("a") or mode:find("+")
    local content = ""
    if mode:sub(1,1) == "r" then
        local text, err = fs.read(path)
        if text == nil then return nil, err or ("cannot open " .. tostring(path)) end
        content = text
    else
        local text = fs.read(path)
        content = text or ""
    end
  if mode:find("w") then content = "" end
  local pos = mode:find("a") and (#content + 1) or 1
  local closed = false
  local function ensure_open() if closed then error("attempt to use a closed file") end end
  local handle = {}
  function handle:read(fmt)
    ensure_open()
    if not canRead then error("file not open for reading") end
    fmt = fmt or "*l"
    if fmt == "*a" then local out = content:sub(pos); pos = #content + 1; if out == "" then return nil end; return out end
    if fmt == "*n" then local s,e = content:find("[%+%-]?%d+%.?%d*", pos); if not s then return nil end; pos = e + 1; return tonumber(content:sub(s,e)) end
    local nl = content:find("\n", pos, true)
    if not nl then if pos > #content then return nil end; local out = content:sub(pos); pos = #content + 1; return out end
    local out = content:sub(pos, nl - 1); pos = nl + 1; return fmt == "*L" and (out .. "\n") or out
  end
  function handle:write(...)
    ensure_open()
    if not canWrite then error("file not open for writing") end
    local chunks = {}
    for i = 1, select('#', ...) do chunks[i] = tostring(select(i, ...)) end
    local text = table.concat(chunks)
    local before = content:sub(1, math.max(0, pos - 1))
    local afterStart = math.min(#content + 1, pos + #text)
    local after = content:sub(afterStart)
    content = before .. text .. after
    pos = pos + #text
    return self
  end
    function handle:flush()
        ensure_open()
        if not canWrite then return true end
        local ok, err = fs.write(path, content)
        if ok == nil then return nil, err end
        return true
    end
    function handle:close()
        if closed then return true end
        if canWrite then
            local ok, err = self:flush()
            if ok == nil then return nil, err end
        end
        closed = true
        return true
    end
  function handle:seek(whence, offset)
    ensure_open()
    whence = whence or "cur"
    offset = offset or 0
    local base = whence == "set" and 1 or (whence == "cur" and pos or (whence == "end" and (#content + 1) or nil))
    if base == nil then return nil, "invalid whence" end
    local nextPos = base + offset
    if nextPos < 1 then return nil, "invalid seek position" end
    pos = nextPos
    return pos - 1
  end
  return handle
end
function io.lines(path)
  local file, err = io.open(path, "r")
  if not file then error(err) end
  return function()
    local line = file:read("*l")
    if line == nil then file:close(); return nil end
    return line
  end
end
function io.read(...) return nil end
function io.write(...) term.write(table.concat({...}, "")) end
function io.flush() return true end
function io.close() return true end

os = {}
function os.clock() return h.os_clock() end
function os.time(tbl)
    if tbl == nil then return h.os_time_now() end
    return h.os_time_from_table(tbl)
end
function os.difftime(t1, t2) return h.os_difftime(t1, t2) end
function os.date(fmt, ts) return h.os_date(fmt, ts) end
function os.remove(path) return h.os_remove(path) end
function os.rename(oldPath, newPath) return h.os_rename(oldPath, newPath) end
function os.getenv(_) return nil end
function os.execute(_) return h.os_execute() end
function os.exit(_) h.os_exit() end
function os.sleep(seconds)
    seconds = tonumber(seconds) or 0
    if seconds <= 0 then
        return true
    end

    local deadline = os.clock() + seconds
    repeat
        event.pull("tick")
    until os.clock() >= deadline
    return true
end

function loadfile(path)
    local source, err = fs.read(path)
    if source == nil then
        return nil, err or ("cannot open " .. tostring(path))
    end
    return load(source, path)
end

function dofile(path)
    local fn, err = loadfile(path)
    if not fn then error(err) end
    return fn()
end

net = {}
function net.isAvailable() local a,r = h.net_is_available(); return { available = a, reason = r } end
local function resp(ok, status, body, err) return { ok = ok, status = status, body = body, error = err or "" } end
function net.get(url) return resp(h.net_get(url)) end
function net.post(url, body) return resp(h.net_post(url, body)) end
function net.request(options) return resp(h.net_request(options)) end
function net.ws(url) return h.net_ws(url) end
function net.send(handle, payload) return h.net_send(handle, payload) end
function net.receive(handle, timeout) return h.net_receive(handle, timeout) end
function net.close(handle) return h.net_close(handle) end

package = {}
local package_receipt_dir = '/packages/.receipts'

local function semver_parts(v)
    local a, b, c = tostring(v or ""):match("^(%d+)%.(%d+)%.(%d+)$")
    if not a then return nil end
    return tonumber(a), tonumber(b), tonumber(c)
end

local function semver_gte(left, right)
    local la, lb, lc = semver_parts(left)
    local ra, rb, rc = semver_parts(right)
    if not la or not ra then
        return tostring(left or "") >= tostring(right or "")
    end
    if la ~= ra then return la > ra end
    if lb ~= rb then return lb > rb end
    return lc >= rc
end

local function parse_dependency_line(line)
    local trimmed = tostring(line or ""):gsub("^%s+", ""):gsub("%s+$", "")
    if trimmed == "" or trimmed:sub(1, 1) == "#" then
        return nil, nil
    end

    local name, min = trimmed:match("^([%w%-%._]+)%s*>=%s*([%d%.]+)$")
    if name then
        return name, min
    end

    local bare = trimmed:match("^([%w%-%._]+)$")
    if bare then
        return bare, nil
    end

    return nil, nil
end

local function package_target_path(packageName, version, relative)
    if relative:match("^bin/") or relative:match("^api/") or relative == "boot/init.lua" then
        return "/" .. relative
    end

    return "/packages/" .. packageName .. "/" .. version .. "/" .. relative
end

local function ensure_parent_dir(path)
    local normalized = tostring(path or "")
    local parent = normalized:match("^(.*)/[^/]+$")
    if not parent or parent == "" then
        return true
    end

    local current = ""
    for part in parent:gmatch("[^/]+") do
        current = current .. "/" .. part
        fs.mkdir(current)
    end

    return true
end

local function receipt_path(name)
    return package_receipt_dir .. '/' .. tostring(name) .. '.txt'
end

local function package_write_receipt(name, version, files)
    ensure_parent_dir(receipt_path(name))
    local lines = { 'version=' .. tostring(version or '') }
    for i = 1, #files do
        lines[#lines + 1] = tostring(files[i])
    end
    return fs.write(receipt_path(name), table.concat(lines, '\n'))
end

local function package_read_receipt(name)
    local path = receipt_path(name)
    local content = fs.read(path)
    if content == nil then
        return nil
    end

    local info = {
        name = tostring(name),
        version = '',
        files = {},
        path = path,
    }

    local first = true
    for line in tostring(content):gmatch('[^\r\n]+') do
        if first and line:match('^version=') then
            info.version = line:sub(9)
            first = false
        else
            if line ~= '' then
                info.files[#info.files + 1] = line
            end
            first = false
        end
    end

    return info
end

local function package_receipt_names()
    local entries = fs.list(package_receipt_dir)
    if entries == nil then
        return {}
    end

    local names = {}
    for i = 1, #entries do
        local entry = tostring(entries[i])
        local name = entry:match('^(.*)%.txt$')
        if name and name ~= '' then
            names[#names + 1] = name
        end
    end

    table.sort(names)
    return names
end

local function package_file_referenced_elsewhere(filePath, excludingName)
    local names = package_receipt_names()
    for i = 1, #names do
        local name = names[i]
        if name ~= excludingName then
            local receipt = package_read_receipt(name)
            if receipt ~= nil then
                for j = 1, #receipt.files do
                    if receipt.files[j] == filePath then
                        return true
                    end
                end
            end
        end
    end

    return false
end

local function prune_empty_dirs(path)
    local current = tostring(path or ''):match('^(.*)/[^/]+$')
    while current and current ~= '' and current ~= '/' do
        local entries = fs.list(current)
        if entries == nil or #entries > 0 then
            break
        end

        fs.remove(current)
        current = current:match('^(.*)/[^/]+$')
    end
end

function package.list()
    local names, err = h.pkg_list()
    if names == nil then return nil, err end
    return names
end

function package.latest(name)
    local version, err = h.pkg_latest(name)
    if version == nil then return nil, err end
    return version
end

function package.files(name, version)
    local resolved, files, err = h.pkg_files(name, version)
    if resolved == nil then return nil, nil, err end
    return resolved, files
end

function package.installed()
    local names = package_receipt_names()
    local result = {}
    for i = 1, #names do
        local receipt = package_read_receipt(names[i])
        if receipt ~= nil then
            result[#result + 1] = {
                name = receipt.name,
                version = receipt.version,
                files = receipt.files,
            }
        end
    end
    return result
end

function package.install(name, version, state)
    if type(name) ~= "string" or name == "" then
        return nil, "package name is required"
    end

    state = state or { installing = {}, installed = {} }
    local key = name .. "@" .. tostring(version or "latest")
    if state.installed[key] then
        return true
    end

    if state.installing[key] then
        return nil, "dependency cycle detected for " .. key
    end

    state.installing[key] = true

    local existing = package_read_receipt(name)
    if existing ~= nil and (version == nil or existing.version == tostring(version)) then
        state.installing[key] = nil
        state.installed[key] = true
        return {
            ok = true,
            name = name,
            version = existing.version,
            filesInstalled = 0,
            alreadyInstalled = true,
        }
    end

    local resolvedVersion, files, err = package.files(name, version)
    if resolvedVersion == nil then
        state.installing[key] = nil
        return nil, err
    end

    local depsText, depsErr = h.pkg_fetch(name, resolvedVersion, "dependencies.txt")
    if depsText ~= nil then
        for line in tostring(depsText):gmatch("[^\r\n]+") do
            local depName, depMinVersion = parse_dependency_line(line)
            if depName then
                local depVersion, depErr = package.latest(depName)
                if depVersion == nil then
                    state.installing[key] = nil
                    return nil, "failed to resolve dependency " .. depName .. ": " .. tostring(depErr)
                end

                if depMinVersion and not semver_gte(depVersion, depMinVersion) then
                    state.installing[key] = nil
                    return nil, "dependency " .. depName .. " requires >= " .. depMinVersion .. " but latest is " .. depVersion
                end

                local ok, depInstallErr = package.install(depName, depVersion, state)
                if not ok then
                    state.installing[key] = nil
                    return nil, depInstallErr
                end
            end
        end
    elseif depsErr ~= nil and tostring(depsErr) ~= "" then
        state.installing[key] = nil
        return nil, "failed to read dependencies for " .. name .. ": " .. tostring(depsErr)
    end

    local count = 0
    local installedFiles = {}
    for i = 1, #files do
        local relative = tostring(files[i])
        if relative ~= "dependencies.txt" and relative ~= "meta.yml" then
            local content, fetchErr = h.pkg_fetch(name, resolvedVersion, relative)
            if content == nil then
                state.installing[key] = nil
                return nil, "failed to fetch " .. relative .. ": " .. tostring(fetchErr)
            end

            local target = package_target_path(name, resolvedVersion, relative)
            ensure_parent_dir(target)
            local ok, writeErr = fs.write(target, content)
            if ok == nil then
                state.installing[key] = nil
                return nil, "failed to write " .. target .. ": " .. tostring(writeErr)
            end

            count = count + 1
            installedFiles[#installedFiles + 1] = target
        end
    end

    local receiptOk, receiptErr = package_write_receipt(name, resolvedVersion, installedFiles)
    if receiptOk == nil then
        state.installing[key] = nil
        return nil, "failed to write receipt: " .. tostring(receiptErr)
    end

    state.installing[key] = nil
    state.installed[key] = true
    return {
        ok = true,
        name = name,
        version = resolvedVersion,
        filesInstalled = count,
    }
end

function package.remove(name)
    if type(name) ~= 'string' or name == '' then
        return nil, 'package name is required'
    end

    local receipt = package_read_receipt(name)
    if receipt == nil then
        return nil, 'package is not installed'
    end

    local removed = 0
    for i = #receipt.files, 1, -1 do
        local path = tostring(receipt.files[i])
        if not package_file_referenced_elsewhere(path, name) and fs.exists(path) then
            local ok, err = fs.remove(path)
            if ok == nil then
                return nil, 'failed to remove ' .. path .. ': ' .. tostring(err)
            end
            removed = removed + 1
            prune_empty_dirs(path)
        end
    end

    local ok, err = fs.remove(receipt.path)
    if ok == nil then
        return nil, 'failed to remove receipt: ' .. tostring(err)
    end
    prune_empty_dirs(receipt.path)

    return {
        ok = true,
        name = receipt.name,
        version = receipt.version,
        filesRemoved = removed,
    }
end

atmos = {}
function atmos.list() return h.atmos_list() end
function atmos.read(label) return h.atmos_read(label) end
function atmos.pump_enabled(label, enabled) h.atmos_pump_enabled(label, enabled) end
function atmos.pump_read(label) return h.atmos_pump_read(label) end
function atmos.pump_rate(label, rate) h.atmos_pump_rate(label, rate) end
function atmos.pump_pressure(label, kpa) h.atmos_pump_pressure(label, kpa) end
function atmos.valve(label, open) h.atmos_valve(label, open) end
function atmos.scrubber(label, params) h.atmos_scrubber(label, params) end
function atmos.scrubber_read(label) return h.atmos_scrubber_read(label) end
function atmos.filter(label, params) h.atmos_filter(label, params) end
function atmos.filter_read(label) return h.atmos_filter_read(label) end
function atmos.vent(label, params) h.atmos_vent(label, params) end
function atmos.vent_read(label) return h.atmos_vent_read(label) end
function atmos.injector(label, params) h.atmos_injector(label, params) end
function atmos.injector_read(label) return h.atmos_injector_read(label) end
function atmos.mixer(label, params) h.atmos_mixer(label, params) end
function atmos.mixer_read(label) return h.atmos_mixer_read(label) end
function atmos.regulator(label, params) h.atmos_regulator(label, params) end
function atmos.regulator_read(label) return h.atmos_regulator_read(label) end
""");
    }

    // ─── Script loading and execution ─────────────────────────────────────

    private bool TryLoadMoonSharpMainChunk(ComputerRuntime runtime, string source, out string error)
    {
        if (runtime.Script == null)
        {
            error = "MoonSharp script was not initialized.";
            return false;
        }

        try
        {
            var closure = runtime.Script.LoadString(source, null, "@/boot/init.lua");
            runtime.MainFunction = closure;
            var coroutine = runtime.Script.CreateCoroutine(closure);
            runtime.MainCoroutine = coroutine.Coroutine;
            runtime.MainThread = runtime.MainCoroutine;
            error = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private bool TryResumeMoonSharp(ComputerRuntime runtime, ReadOnlySpan<LuaEventArg> args, out bool isDead, out string error)
    {
        isDead = false;

        if (runtime.MainCoroutine == null || runtime.Script == null)
        {
            error = "Runtime error: Lua coroutine is missing.";
            return false;
        }

        try
        {
            var dynArgs = new DynValue[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                dynArgs[i] = args[i].Kind switch
                {
                    LuaEventArgKind.String => DynValue.NewString(args[i].StringValue ?? string.Empty),
                    LuaEventArgKind.Number => DynValue.NewNumber(args[i].NumberValue),
                    LuaEventArgKind.Boolean => DynValue.NewBoolean(args[i].BoolValue),
                    _ => DynValue.Nil,
                };
            }

            var result = runtime.MainCoroutine.Resume(dynArgs);
            isDead = runtime.MainCoroutine.State == CoroutineState.Dead;
            error = string.Empty;
            return true;
        }
        catch (ScriptRuntimeException e)
        {
            error = "Runtime error: " + (e.DecoratedMessage ?? e.Message);
            return false;
        }
        catch (Exception e)
        {
            error = "Fatal error: " + e.Message;
            return false;
        }
    }

    private bool TryExecuteMoonSharpSnippet(ComputerRuntime runtime, string source, out bool hasResult, out string result, out string error)
    {
        hasResult = false;
        result = string.Empty;

        if (runtime.Script == null)
        {
            error = "MoonSharp script was not initialized.";
            return false;
        }

        try
        {
            var closure = runtime.Script.LoadString(source, null, "@exec");
            var results = runtime.Script.Call(closure);
            hasResult = results.Type != DataType.Void;
            if (hasResult)
                result = ToDynString(results);

            error = string.Empty;
            return true;
        }
        catch (ScriptRuntimeException e)
        {
            error = "Error: " + (e.DecoratedMessage ?? e.Message);
            return false;
        }
        catch (Exception e)
        {
            error = "Error: " + e.Message;
            return false;
        }
    }

    // ─── Helper methods for DynValue conversion ──────────────────────────

    private static string ToDynString(DynValue value)
    {
        if (value.Type == DataType.String)
            return value.String;
        return value.ToPrintString();
    }

    private static bool IsTruthy(DynValue value)
    {
        return value.Type switch
        {
            DataType.Nil => false,
            DataType.Void => false,
            DataType.Boolean => value.Boolean,
            DataType.Number => Math.Abs(value.Number) > double.Epsilon,
            _ => true,
        };
    }

    private static bool TryResolveTerminalColor(DynValue value, out Color color)
    {
        if (value.Type == DataType.Number)
        {
            var index = (int)value.Number;
            if (index >= 0 && index < TerminalPalette.Length)
            {
                color = TerminalPalette[index];
                return true;
            }

            color = DefaultTerminalForeground;
            return false;
        }

        if (value.Type == DataType.String)
        {
            try
            {
                color = Color.FromHex(value.String);
                return true;
            }
            catch
            {
            }
        }

        color = DefaultTerminalForeground;
        return false;
    }

    // ─── HTTP / WebSocket ───────────────────────────────────────────────────

    private HttpResponse ExecuteHttpRequest(EntityUid uid, ComputerRuntime runtime, HttpRequestSpec request)
    {
        var capabilities = GetCapabilities(uid);
        if (!IsNetworkAllowed(capabilities, out var reason))
            return HttpResponse.Fail(reason);

        if (!TryValidateRequest(runtime, request.Url, out var uri, out var error) || uri == null)
            return HttpResponse.Fail(error);

        if (!TryTakeRequestToken(runtime))
            return HttpResponse.Fail("Rate limit exceeded.");

        try
        {
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), uri);
            if (!string.IsNullOrEmpty(request.Body))
                message.Content = new StringContent(request.Body, Encoding.UTF8, "text/plain");

            using var cts = new CancellationTokenSource(_cfg.GetCVar(CCVars.ProgrammableComputerNetworkTimeoutMs));
            var response = _http.Client.Send(message, cts.Token);

            var maxResponseBytes = _cfg.GetCVar(CCVars.ProgrammableComputerNetworkMaxResponseBytes);
            var responseBody = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            if (Encoding.UTF8.GetByteCount(responseBody) > maxResponseBytes)
                responseBody = responseBody[..Math.Min(responseBody.Length, 1024)];

            return HttpResponse.OkResult((int)response.StatusCode, responseBody);
        }
        catch (Exception e)
        {
            return HttpResponse.Fail(e.Message);
        }
    }

    private int ConnectWebSocket(EntityUid uid, ComputerRuntime runtime, string url)
    {
        var capabilities = GetCapabilities(uid);
        if (!IsNetworkAllowed(capabilities, out var reason))
            throw new Exception(reason);

        if (!TryValidateRequest(runtime, url, out var uri, out var error) || uri == null)
            throw new Exception(error);

        if (!(uri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase)))
            throw new Exception("WebSocket URL must be ws:// or wss://");

        if (!_cfg.GetCVar(CCVars.ProgrammableComputerNetworkWebSocketEnabled))
            throw new Exception("WebSocket is disabled by server policy");

        if (!TryTakeRequestToken(runtime))
            throw new Exception("Rate limit exceeded.");

        var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(_cfg.GetCVar(CCVars.ProgrammableComputerNetworkTimeoutMs));
        socket.ConnectAsync(uri, cts.Token).GetAwaiter().GetResult();
        return runtime.AddWebSocket(socket);
    }

    private void SendWebSocket(ComputerRuntime runtime, int handle, string payload)
    {
        if (!runtime.WebSockets.TryGetValue(handle, out var socket))
            throw new Exception("Invalid websocket handle");

        var maxFrame = _cfg.GetCVar(CCVars.ProgrammableComputerNetworkMaxWebSocketFrameBytes);
        var bytes = Encoding.UTF8.GetBytes(payload);
        if (bytes.Length > maxFrame)
            throw new Exception("Payload exceeds websocket frame size limit");

        socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private string? ReceiveWebSocket(ComputerRuntime runtime, int handle, int timeoutMs)
    {
        if (!runtime.WebSockets.TryGetValue(handle, out var socket))
            throw new Exception("Invalid websocket handle");

        var buffer = new byte[Math.Max(256, _cfg.GetCVar(CCVars.ProgrammableComputerNetworkMaxWebSocketFrameBytes))];
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var result = socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token)
                .GetAwaiter()
                .GetResult();
            return result.Count <= 0 ? null : Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private void CloseWebSocket(ComputerRuntime runtime, int handle)
    {
        if (!runtime.WebSockets.Remove(handle, out var socket))
            return;

        try
        {
            socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch { /* best-effort */ }

        socket.Dispose();
    }

    // ─── Network policy ─────────────────────────────────────────────────────

    private bool IsNetworkAllowed(ComputerCapabilities capabilities, out string reason)
    {
        if (capabilities.NetworkTier <= 0)
        {
            reason = "No network module installed.";
            return false;
        }

        if (!_cfg.GetCVar(CCVars.ProgrammableComputerNetworkEnabled))
        {
            reason = "Network access disabled by server policy.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool TryValidateRequest(ComputerRuntime runtime, string url, out Uri? uri, out string error)
    {
        error = string.Empty;
        uri = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            error = "Invalid URL.";
            return false;
        }

        if (uri.Host.Length == 0)
        {
            error = "URL host is missing.";
            return false;
        }

        var allowInsecure = _cfg.GetCVar(CCVars.ProgrammableComputerNetworkAllowInsecureHttp);
        var scheme = uri.Scheme.ToLowerInvariant();
        var allowedScheme = scheme is "https" or "wss" || (allowInsecure && scheme is "http" or "ws");
        if (!allowedScheme)
        {
            error = "URL scheme blocked by policy.";
            return false;
        }

        var allowList = _cfg.GetCVar(CCVars.ProgrammableComputerNetworkAllowList)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (allowList.Length == 0)
        {
            error = "No allowlisted hosts configured.";
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        if (!allowList.Any(pattern => HostMatches(host, pattern.ToLowerInvariant())))
        {
            error = "Host is not allowlisted.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReserveRam(ComputerRuntime runtime, int bytes, out string error)
    {
        if (bytes <= 0)
        {
            error = string.Empty;
            return true;
        }

        var next = runtime.RamUsedBytes + bytes;
        if (next <= runtime.RamLimitBytes)
        {
            runtime.RamUsedBytes = next;
            error = string.Empty;
            return true;
        }

        error = $"Out of memory: requested {bytes} bytes, used {runtime.RamUsedBytes}/{runtime.RamLimitBytes}.";
        return false;
    }

    private static void ReleaseRam(ComputerRuntime runtime, int bytes)
    {
        if (bytes <= 0)
            return;

        runtime.RamUsedBytes = Math.Max(0, runtime.RamUsedBytes - bytes);
    }

    private void CrashRuntimeForOutOfMemory(EntityUid uid, ComputerRuntime runtime, string detail)
    {
        if (!TryComp<ProgrammableComputerComponent>(uid, out var component))
            return;

        ShutdownRuntime(uid, component, runtime, "System halted: out of memory.");
        runtime.Terminal.NewLine();
        runtime.Terminal.Write(detail);
        UpdateUi(uid, component);
    }

    private static bool HostMatches(string host, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return host.EndsWith(pattern[1..], StringComparison.Ordinal);
        return host.Equals(pattern, StringComparison.Ordinal);
    }

    private bool TryTakeRequestToken(ComputerRuntime runtime)
    {
        var limit = _cfg.GetCVar(CCVars.ProgrammableComputerNetworkRateLimitPerMinute);
        var now = DateTimeOffset.UtcNow;
        while (runtime.RequestWindow.Count > 0 && (now - runtime.RequestWindow.Peek()).TotalMinutes >= 1)
            runtime.RequestWindow.Dequeue();

        if (runtime.RequestWindow.Count >= limit)
            return false;

        runtime.RequestWindow.Enqueue(now);
        return true;
    }

    // ─── Hardware / capabilities ─────────────────────────────────────────────

    private ComputerCapabilities GetCapabilities(EntityUid uid)
    {
        var result = new ComputerCapabilities();
        ApplySlot(uid, ProgrammableComputerComponent.CpuSlotName, ref result);
        ApplySlot(uid, ProgrammableComputerComponent.RamSlotOneName, ref result);
        ApplySlot(uid, ProgrammableComputerComponent.RamSlotTwoName, ref result);
        ApplySlot(uid, ProgrammableComputerComponent.DiskSlotOneName, ref result);
        ApplySlot(uid, ProgrammableComputerComponent.DiskSlotTwoName, ref result);
        ApplySlot(uid, ProgrammableComputerComponent.NetworkSlotName, ref result);
        ApplySlot(uid, ProgrammableComputerComponent.GpuSlotName, ref result);
        ApplySlot(uid, ProgrammableComputerComponent.ExpansionSlotName, ref result);
        return result;
    }

    private void ApplySlot(EntityUid uid, string slotId, ref ComputerCapabilities capabilities)
    {
        if (!_itemSlots.TryGetSlot(uid, slotId, out var slot) || slot.Item is not { } item)
            return;

        if (!TryComp<MachinePartComponent>(item, out var part))
            return;

        switch (part.PartType)
        {
            case ProgrammableComputerComponent.CpuMachinePart:
                capabilities.CpuTier = Math.Max(capabilities.CpuTier, part.Rating);
                capabilities.InstructionBudget = Math.Max(capabilities.InstructionBudget, part.Rating switch
                {
                    1 => 10_000,
                    2 => 50_000,
                    _ => 200_000,
                });
                capabilities.TimeSliceMs = Math.Max(capabilities.TimeSliceMs, part.Rating switch
                {
                    1 => 5,
                    2 => 15,
                    _ => 40,
                });
                break;

            case ProgrammableComputerComponent.RamMachinePart:
                capabilities.RamSlotsInstalled++;
                capabilities.TotalRamKiB += part.Rating switch
                {
                    1 => 64,
                    2 => 256,
                    _ => 1024,
                };
                break;

            case ProgrammableComputerComponent.DiskMachinePart:
                capabilities.DiskSlotsInstalled++;
                capabilities.TotalDiskKiB += part.Rating switch
                {
                    1 => 128,
                    2 => 1024,
                    _ => 8192,
                };
                capabilities.MaxFiles += part.Rating switch
                {
                    1 => 32,
                    2 => 128,
                    _ => 512,
                };
                capabilities.MaxFileSizeKiB = Math.Max(capabilities.MaxFileSizeKiB, part.Rating switch
                {
                    1 => 16,
                    2 => 64,
                    _ => 256,
                });
                break;

            case ProgrammableComputerComponent.NetworkMachinePart:
                capabilities.NetworkTier = Math.Max(capabilities.NetworkTier, part.Rating);
                break;

            case ProgrammableComputerComponent.GpuMachinePart:
                capabilities.GpuTier = Math.Max(capabilities.GpuTier, part.Rating);
                break;

            case ProgrammableComputerComponent.ExpansionMachinePart:
                capabilities.ExpansionModules++;
                break;
        }
    }

    private static string FormatKiB(int value) =>
        value >= 1024 ? $"{value / 1024.0:F1} MiB" : $"{value} KiB";

    // ─── Inner types ─────────────────────────────────────────────────────────

    private enum LuaEventArgKind
    {
        String,
        Number,
        Boolean,
    }

    private readonly record struct LuaEventArg(LuaEventArgKind Kind, string? StringValue, double NumberValue, bool BoolValue)
    {
        public static LuaEventArg FromString(string value) => new(LuaEventArgKind.String, value, 0, false);
        public static LuaEventArg FromNumber(double value) => new(LuaEventArgKind.Number, null, value, false);
        public static LuaEventArg FromBoolean(bool value) => new(LuaEventArgKind.Boolean, null, 0, value);
    }

    private struct ComputerCapabilities
    {
        public int CpuTier;
        public int RamSlotsInstalled;
        public int DiskSlotsInstalled;
        public int NetworkTier;
        public int GpuTier;
        public int ExpansionModules;
        public int InstructionBudget;
        public int TimeSliceMs;
        public int TotalRamKiB;
        public int TotalDiskKiB;
        public int MaxFiles;
        public int MaxFileSizeKiB;
    }

    private sealed class ComputerRuntime : IDisposable
    {
        public Script? Script;
        public DynValue? MainFunction;
        public Coroutine? MainCoroutine;
        public VirtualFileSystem FileSystem { get; } = new();
        public Dictionary<int, ClientWebSocket> WebSockets { get; } = new();
        public Queue<DateTimeOffset> RequestWindow { get; } = new();
        public int NextSocketHandle { get; private set; } = 1;
        public TerminalBuffer Terminal { get; set; } = new();
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public int RamLimitBytes { get; set; }
        public int RamUsedBytes { get; set; }
        public object? MainThread;
        public bool IsRunning;
        public bool IsPoweredOn;
        public bool IsBooting;
        public TimeSpan BootReadyAt;
        public int BootStage;
        public ComputerCapabilities BootCapabilities;

        public int AddWebSocket(ClientWebSocket socket)
        {
            var handle = NextSocketHandle++;
            WebSockets[handle] = socket;
            return handle;
        }

        public void Dispose()
        {
            foreach (var (_, socket) in WebSockets)
            {
                try { socket.Dispose(); }
                catch { /* ignore */ }
            }
            WebSockets.Clear();
        }
    }

    /// <summary>Fixed terminal cell grid with cursor and color tracking.</summary>
    private sealed class TerminalBuffer
    {
        private readonly TerminalCell[,] _cells;

        public int Width { get; }
        public int Height { get; }

        public bool CursorBlink = true;
        public int CursorX { get; private set; } = 1;
        public int CursorY { get; private set; } = 1;
        public Color ForegroundColor { get; private set; } = DefaultTerminalForeground;
        public Color BackgroundColor { get; private set; } = DefaultTerminalBackground;

        public TerminalBuffer(int width = ProgrammableComputerComponent.TerminalWidth, int height = ProgrammableComputerComponent.TerminalHeight)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            _cells = new TerminalCell[Height, Width];
            Clear();
        }

        public void Clear()
        {
            FillScreen();
            CursorX = 1;
            CursorY = 1;
        }

        public void ClearLine(int y)
        {
            if (y < 1 || y > Height) return;

            for (var c = 0; c < Width; c++)
                _cells[y - 1, c] = BlankCell();
        }

        public void SetTextColor(Color color)
        {
            ForegroundColor = color;
        }

        public void SetBackgroundColor(Color color)
        {
            BackgroundColor = color;
        }

        public void ResetColors()
        {
            ForegroundColor = DefaultTerminalForeground;
            BackgroundColor = DefaultTerminalBackground;
        }

        public void SetCursorPos(int x, int y)
        {
            CursorX = Math.Clamp(x, 1, Width);

            if (y < 1)
            {
                CursorY = 1;
                return;
            }

            if (y <= Height)
            {
                CursorY = y;
                return;
            }

            // Moving the cursor below the viewport should behave like a terminal:
            // scroll up by the overflow and keep the cursor on the last visible row.
            Scroll(y - Height);
            CursorY = Height;
        }

        public void Write(string text)
        {
            foreach (var ch in text)
            {
                if (ch == '\n')
                {
                    NewLine();
                    continue;
                }

                if (ch == '\r')
                {
                    CursorX = 1;
                    continue;
                }

                if (CursorX > Width)
                    NewLine();

                if (CursorX >= 1 && CursorX <= Width && CursorY >= 1 && CursorY <= Height)
                    _cells[CursorY - 1, CursorX - 1] = new TerminalCell(ch, ForegroundColor, BackgroundColor);

                CursorX++;
            }
        }

        public void NewLine()
        {
            CursorX = 1;
            CursorY++;
            if (CursorY > Height)
            {
                ScrollInternal(1);
                CursorY = Height;
            }
        }

        /// <summary>Explicit scroll: shifts content but leaves cursor at same coordinates.</summary>
        public void Scroll(int n)
        {
            if (n == 0) return;
            var count = Math.Abs(n);
            for (var i = 0; i < count; i++)
            {
                if (n > 0)
                    ScrollInternal(1);
                else
                    ScrollInternal(-1);
            }
        }

        private void ScrollInternal(int dir)
        {
            if (dir > 0)
            {
                for (var row = 0; row < Height - 1; row++)
                    for (var col = 0; col < Width; col++)
                        _cells[row, col] = _cells[row + 1, col];
                for (var col = 0; col < Width; col++)
                    _cells[Height - 1, col] = BlankCell();
            }
            else
            {
                for (var row = Height - 1; row > 0; row--)
                    for (var col = 0; col < Width; col++)
                        _cells[row, col] = _cells[row - 1, col];
                for (var col = 0; col < Width; col++)
                    _cells[0, col] = BlankCell();
            }
        }

        public ProgrammableComputerTerminalCell[] GetCells()
        {
            var cells = new ProgrammableComputerTerminalCell[Width * Height];
            var index = 0;

            for (var row = 0; row < Height; row++)
            {
                for (var col = 0; col < Width; col++)
                {
                    var cell = _cells[row, col];
                    cells[index++] = new ProgrammableComputerTerminalCell(cell.Glyph, cell.Foreground, cell.Background);
                }
            }

            return cells;
        }

        private void FillScreen()
        {
            for (var row = 0; row < Height; row++)
            {
                for (var col = 0; col < Width; col++)
                {
                    _cells[row, col] = BlankCell();
                }
            }
        }

        private TerminalCell BlankCell()
        {
            return new TerminalCell('\0', ForegroundColor, BackgroundColor);
        }

        private readonly record struct TerminalCell(char Glyph, Color Foreground, Color Background);
    }

    private sealed class VirtualFileSystem
    {
        private readonly Dictionary<string, string> _files = new();
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { "/" };

        public int UsedKiB => (int)Math.Ceiling(_files.Sum(f => Encoding.UTF8.GetByteCount(f.Value)) / 1024.0);

        public bool Exists(string path)
        {
            var normalized = Normalize(path);
            return _files.ContainsKey(normalized) || _directories.Contains(normalized);
        }

        public IEnumerable<string> ListDirectory(string path)
        {
            return TryListDirectory(path, out var entries, out _)
                ? entries
                : ["Directory not found."];
        }

        public bool TryListDirectory(string path, out IReadOnlyList<string> entries, out string error)
        {
            var normalized = Normalize(path);
            if (!_directories.Contains(normalized))
            {
                entries = Array.Empty<string>();
                error = "Directory not found.";
                return false;
            }

            var prefix = normalized == "/" ? "/" : normalized + "/";
            var results = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var directory in _directories)
            {
                if (!directory.StartsWith(prefix, StringComparison.Ordinal) || directory == normalized)
                    continue;
                var rest = directory[prefix.Length..];
                if (rest.Length == 0) continue;
                var idx = rest.IndexOf('/');
                results.Add((idx >= 0 ? rest[..idx] : rest) + "/");
            }

            foreach (var file in _files.Keys)
            {
                if (!file.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var rest = file[prefix.Length..];
                var idx = rest.IndexOf('/');
                var head = idx >= 0 ? rest[..idx] : rest;
                if (head.Length > 0) results.Add(head);
            }

            entries = results.Count == 0
                ? ["(empty)"]
                : results.ToArray();
            error = string.Empty;
            return true;
        }

        public bool TryRead(string path, out string text) =>
            _files.TryGetValue(Normalize(path), out text!);

        public IEnumerable<KeyValuePair<string, string>> EnumerateFiles()
        {
            return _files.OrderBy(kvp => kvp.Key, StringComparer.Ordinal);
        }

        public bool TryWrite(string path, string text, ComputerCapabilities capabilities, out string error)
        {
            error = string.Empty;
            var normalized = Normalize(path);
            var parent = ParentDirectory(normalized);

            if (!_directories.Contains(parent))
            {
                error = "Directory does not exist.";
                return false;
            }

            var bytes = Encoding.UTF8.GetByteCount(text);
            if (bytes > capabilities.MaxFileSizeKiB * 1024)
            {
                error = "File exceeds per-file size limit.";
                return false;
            }

            if (!_files.ContainsKey(normalized) && _files.Count >= capabilities.MaxFiles)
            {
                error = "File count limit reached.";
                return false;
            }

            var oldBytes = _files.TryGetValue(normalized, out var old) ? Encoding.UTF8.GetByteCount(old) : 0;
            var used = _files.Sum(f => Encoding.UTF8.GetByteCount(f.Value));
            if (used - oldBytes + bytes > capabilities.TotalDiskKiB * 1024)
            {
                error = "Disk capacity exceeded.";
                return false;
            }

            _files[normalized] = text;
            return true;
        }

        public bool Remove(string path)
        {
            return TryRemove(path, out _);
        }

        public bool TryRemove(string path, out string error)
        {
            var normalized = Normalize(path);
            if (_files.Remove(normalized))
            {
                error = string.Empty;
                return true;
            }

            if (!_directories.Contains(normalized) || normalized == "/")
            {
                error = "Path not found.";
                return false;
            }

            var prefix = normalized + "/";
            if (_files.Keys.Any(f => f.StartsWith(prefix, StringComparison.Ordinal)))
            {
                error = "Directory is not empty.";
                return false;
            }
            if (_directories.Any(d => d != normalized && d.StartsWith(prefix, StringComparison.Ordinal)))
            {
                error = "Directory is not empty.";
                return false;
            }

            _directories.Remove(normalized);
            error = string.Empty;
            return true;
        }

        public bool TryCreateDirectory(string path)
        {
            var normalized = Normalize(path);
            if (_directories.Contains(normalized))
                return true;

            var parent = ParentDirectory(normalized);
            if (!_directories.Contains(parent))
                return false;

            _directories.Add(normalized);
            return true;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            path = path.Replace('\\', '/');
            if (!path.StartsWith('/'))
                path = "/" + path;

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var cleaned = new List<string>();
            foreach (var part in parts)
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (cleaned.Count > 0) cleaned.RemoveAt(cleaned.Count - 1);
                }
                else
                {
                    cleaned.Add(part);
                }
            }
            return "/" + string.Join('/', cleaned);
        }

        private static string ParentDirectory(string normalized)
        {
            var idx = normalized.LastIndexOf('/');
            return idx <= 0 ? "/" : normalized[..idx];
        }
    }

    private record struct HttpRequestSpec
    {
        public string Url;
        public string Method;
        public string? Body;
    }

    private record struct HttpResponse
    {
        public bool Ok;
        public int Status;
        public string Body;
        public string? Error;

        public static HttpResponse Fail(string error) =>
            new() { Ok = false, Status = 0, Body = string.Empty, Error = error };

        public static HttpResponse OkResult(int status, string body) =>
            new() { Ok = true, Status = status, Body = body, Error = null };
    }

    private readonly record struct SemVersion(int Major, int Minor, int Patch) : IComparable<SemVersion>
    {
        public int CompareTo(SemVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0)
                return major;

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0)
                return minor;

            return Patch.CompareTo(other.Patch);
        }

        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
    }
}

internal sealed class DenyAllScriptLoader : IScriptLoader
{
    public object LoadFile(string file, Table globalContext)
    {
        throw new ScriptRuntimeException("Script file loading is disabled in programmable computer sandbox.");
    }

    [Obsolete]
    public string ResolveFileName(string filename, Table globalContext)
    {
        throw new ScriptRuntimeException("Script file loading is disabled in programmable computer sandbox.");
    }

    public string ResolveModuleName(string modname, Table globalContext)
    {
        throw new ScriptRuntimeException("Module loading is disabled in programmable computer sandbox.");
    }
}
