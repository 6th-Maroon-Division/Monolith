using System;
using System.Threading.Tasks;
using Content.IntegrationTests;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[SetUpFixture]
public sealed class ProgrammableComputerPoolSetup
{
    private static TimeSpan MaximumTotalTestingTimeLimit => TimeSpan.FromMinutes(120);
    private static TimeSpan HardStopTimeLimit => MaximumTotalTestingTimeLimit.Add(TimeSpan.FromMinutes(1));

    [OneTimeSetUp]
    public void Setup()
    {
        PoolManager.Startup();

        _ = Task.Delay(MaximumTotalTestingTimeLimit).ContinueWith(_ =>
        {
            TestContext.Error.WriteLine($"\n\n{nameof(ProgrammableComputerPoolSetup)}: ERROR: Tests are taking too long. Shutting down all tests. This may lead to weird failures/exceptions.\n\n");
            PoolManager.Shutdown();
        });

        _ = Task.Delay(HardStopTimeLimit).ContinueWith(_ =>
        {
            var deathReport = PoolManager.DeathReport();
            Environment.FailFast($"Tests took way too long.\nDeath Report:\n{deathReport}");
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        PoolManager.Shutdown();
    }
}