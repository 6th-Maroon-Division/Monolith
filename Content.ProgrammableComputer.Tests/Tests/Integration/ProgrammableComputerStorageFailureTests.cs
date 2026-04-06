using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.Server.ProgrammableComputer;
using Content.Shared.ProgrammableComputer;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerStorageFailureTests
{
    [Test]
    public async Task FsWrite_FailsWhenFileTooLarge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        var (computer, system) = await BootComputerWithDisk(server, entMan);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.DoesNotThrow(() => ProgrammableComputerIntegrationTestHelper.ExecuteLua(runtime, @"
local limits = computer.limits()
local tooLarge = string.rep('x', (limits.maxFileSizeKiB * 1024) + 1)
local ok, err = fs.write('/tmp/too-large.txt', tooLarge)
if ok ~= nil then error('expected fs.write to fail for oversized file') end
if type(err) ~= 'string' or err == '' then error('expected fs.write error text') end
if not string.find(err, 'per-file size limit', 1, true) then
  error('unexpected error for oversized file: ' .. tostring(err))
end
"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FsWrite_FailsWhenFileCountLimitReached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        var (computer, system) = await BootComputerWithDisk(server, entMan);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.DoesNotThrow(() => ProgrammableComputerIntegrationTestHelper.ExecuteLua(runtime, @"
local limits = computer.limits()
for i = 1, limits.maxFiles do
  local ok, err = fs.write('/tmp/count-' .. i .. '.txt', 'x')
  if ok == nil then error('unexpected early failure at file ' .. i .. ': ' .. tostring(err)) end
end

local ok, err = fs.write('/tmp/count-overflow.txt', 'x')
if ok ~= nil then error('expected file count limit failure') end
if not string.find(tostring(err), 'File count limit reached', 1, true) then
  error('unexpected file count error: ' .. tostring(err))
end
"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FsWrite_FailsWhenDiskCapacityExceeded()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        var (computer, system) = await BootComputerWithDisk(server, entMan);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.DoesNotThrow(() => ProgrammableComputerIntegrationTestHelper.ExecuteLua(runtime, @"
local limits = computer.limits()
local payload = string.rep('x', limits.maxFileSizeKiB * 1024)

local sawDiskFull = false
for i = 1, (limits.maxFiles + 16) do
  local ok, err = fs.write('/tmp/disk-' .. i .. '.txt', payload)
  if ok == nil then
    if string.find(tostring(err), 'Disk capacity exceeded', 1, true) then
      sawDiskFull = true
      break
    end

    if string.find(tostring(err), 'File count limit reached', 1, true) then
      break
    end

    error('unexpected disk write failure: ' .. tostring(err))
  end
end

if not sawDiskFull then
  error('expected to eventually hit disk capacity before exhausting test loop')
end
"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FsWrite_OverwriteReleasesSpace()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        var (computer, system) = await BootComputerWithDisk(server, entMan);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.DoesNotThrow(() => ProgrammableComputerIntegrationTestHelper.ExecuteLua(runtime, @"
local limits = computer.limits()
local payload = string.rep('x', limits.maxFileSizeKiB * 1024)

local okA, errA = fs.write('/tmp/a.txt', payload)
if okA == nil then error('failed to write a.txt: ' .. tostring(errA)) end
local okB, errB = fs.write('/tmp/b.txt', payload)
if okB == nil then error('failed to write b.txt: ' .. tostring(errB)) end

local okC, errC = fs.write('/tmp/c.txt', payload)
if okC ~= nil then error('expected c.txt to fail before reclaiming space') end
if not string.find(tostring(errC), 'Disk capacity exceeded', 1, true) then
  error('unexpected c.txt error: ' .. tostring(errC))
end

local okOverwrite, errOverwrite = fs.write('/tmp/a.txt', '')
if okOverwrite == nil then error('overwrite should succeed: ' .. tostring(errOverwrite)) end

local okRetry, errRetry = fs.write('/tmp/c.txt', payload)
if okRetry == nil then error('c.txt should succeed after overwrite reclaim: ' .. tostring(errRetry)) end
"));
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<(EntityUid Computer, ProgrammableComputerSystem System)> BootComputerWithDisk(
      IServerIntegrationInstance server,
      IServerEntityManager entMan)
    {
        EntityUid computer = default;
        ProgrammableComputerSystem system = default!;

        await server.WaitAssertion(() =>
        {
            computer = entMan.SpawnEntity("ComputerProgrammable", MapCoordinates.Nullspace);
            system = entMan.System<ProgrammableComputerSystem>();

            ProgrammableComputerIntegrationTestHelper.InstallRequiredHardware(entMan, computer);
            var disk = entMan.SpawnEntity("ProgrammableComputerDiskTier1", MapCoordinates.Nullspace);
            ProgrammableComputerIntegrationTestHelper.InsertModule(entMan, computer, disk, "disk_slot_1");

            entMan.EventBus.RaiseLocalEvent(computer, new ProgrammableComputerPowerActionMessage(ProgrammableComputerPowerAction.Start));
        });

        server.RunTicks(360);

        await server.WaitAssertion(() =>
        {
            var runtime = ProgrammableComputerIntegrationTestHelper.GetRuntime(system, computer);
            Assert.That(ProgrammableComputerIntegrationTestHelper.GetRuntimeBool(runtime, "IsRunning"), Is.True);
        });

        return (computer, system);
    }
}
