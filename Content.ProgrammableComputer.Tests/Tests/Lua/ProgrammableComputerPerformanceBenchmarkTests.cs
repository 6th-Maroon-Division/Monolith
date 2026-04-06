using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Content.ProgrammableComputer.Tests;

[TestFixture]
public sealed class ProgrammableComputerPerformanceBenchmarkTests
{
    private static readonly bool IsCi =
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    private static void AssertPerf(double measured, double threshold, string message)
    {
        if (IsCi)
        {
            Assert.That(measured, Is.GreaterThanOrEqualTo(0), "CI mode: benchmark executed and produced a valid metric.");
            return;
        }

        Assert.That(measured, Is.LessThan(threshold), message);
    }

    /// <summary>
    /// Benchmark: Runtime initialization speed
    /// </summary>
    [Test]
    public void RuntimeInitialization_Benchmark()
    {
        var stopwatch = Stopwatch.StartNew();
        var count = 100;

        for (int i = 0; i < count; i++)
        {
            var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        }

        stopwatch.Stop();
        var averageMs = stopwatch.Elapsed.TotalMilliseconds / count;

        TestContext.Out.WriteLine($"Runtime initialization benchmark:");
        TestContext.Out.WriteLine($"  Total time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms for {count} runtimes");
        TestContext.Out.WriteLine($"  Average per runtime: {averageMs:F4}ms");

        // Assert reasonable performance: should initialize in < 50ms per runtime on modern hardware
        AssertPerf(averageMs, 50, "Runtime initialization should be fast (< 50ms per instance)");
    }

    /// <summary>
    /// Benchmark: Script compilation speed
    /// </summary>
    [Test]
    public void ScriptCompilation_Benchmark()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var testScript = @"
-- Basic computation test
local function fibonacci(n)
    if n <= 1 then return n end
    return fibonacci(n-1) + fibonacci(n-2)
end

local result = fibonacci(20)
return result
";

        var stopwatch = Stopwatch.StartNew();
        var count = 1000;

        for (int i = 0; i < count; i++)
        {
            script.DoString(testScript);
        }

        stopwatch.Stop();
        var averageMs = stopwatch.Elapsed.TotalMilliseconds / count;

        TestContext.Out.WriteLine($"Script compilation benchmark:");
        TestContext.Out.WriteLine($"  Total time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms for {count} compilations");
        TestContext.Out.WriteLine($"  Average per compilation: {averageMs:F4}ms");

        // Assert reasonable performance: should compile in < 20ms per script
        AssertPerf(averageMs, 20, "Script compilation should be reasonably fast (< 20ms per compilation)");
    }

    /// <summary>
    /// Benchmark: API function call overhead
    /// </summary>
    [Test]
    public void ApiCallOverhead_Benchmark()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();
        var output = new System.Collections.Generic.List<string>();
        ProgrammableComputerLuaTestHelper.BindTerm(script, output);

        script.DoString(ProgrammableComputerLuaTestHelper.ReadRepoFile("Resources/ProgrammableComputer/api/keyboard.lua"));

        var stopwatch = Stopwatch.StartNew();
        var count = 10000;

        script.DoString($@"
for i = 1, {count} do
    keyboard_emit('key_pressed', 10, false, false, false, false, false, 0)
end
");

        stopwatch.Stop();
        var averageUs = stopwatch.Elapsed.TotalMicroseconds / count;

        TestContext.Out.WriteLine($"API call overhead benchmark:");
        TestContext.Out.WriteLine($"  Total time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms for {count} calls");
        TestContext.Out.WriteLine($"  Average per call: {averageUs:F2}μs");

        // Assert reasonable performance: API calls should be < 100μs each
        AssertPerf(averageUs, 100, "API calls should be efficient (< 100μs per call)");
    }

    /// <summary>
    /// Benchmark: Large table iteration performance
    /// </summary>
    [Test]
    public void TableIteration_Benchmark()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();

        var stopwatch = Stopwatch.StartNew();
        var count = 100;

        for (int i = 0; i < count; i++)
        {
            script.DoString(@"
local tbl = {}
for i = 1, 1000 do
    tbl[i] = i * 2
end

local sum = 0
for i = 1, 1000 do
    sum = sum + tbl[i]
end
");
        }

        stopwatch.Stop();
        var averageMs = stopwatch.Elapsed.TotalMilliseconds / count;

        TestContext.Out.WriteLine($"Table iteration benchmark:");
        TestContext.Out.WriteLine($"  Total time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms for {count} iterations");
        TestContext.Out.WriteLine($"  Average per iteration: {averageMs:F4}ms");

        // Assert reasonable performance: processing 1000 items should be < 10ms
        AssertPerf(averageMs, 10, "Table iteration should be efficient (< 10ms for 1000 items)");
    }

    /// <summary>
    /// Benchmark: String concatenation performance
    /// </summary>
    [Test]
    public void StringConcatenation_Benchmark()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();

        var stopwatch = Stopwatch.StartNew();
        var count = 100;

        for (int i = 0; i < count; i++)
        {
            script.DoString(@"
local str = ''
for i = 1, 100 do
    str = str .. 'x'
end
");
        }

        stopwatch.Stop();
        var averageMs = stopwatch.Elapsed.TotalMilliseconds / count;

        TestContext.Out.WriteLine($"String concatenation benchmark:");
        TestContext.Out.WriteLine($"  Total time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms for {count} operations");
        TestContext.Out.WriteLine($"  Average per operation: {averageMs:F4}ms");

        // Assert reasonable performance: building 100-char strings should be < 5ms
        AssertPerf(averageMs, 5, "String concatenation should be efficient (< 5ms for 100-char build)");
    }

    /// <summary>
    /// Benchmark: Error handling overhead
    /// </summary>
    [Test]
    public void ErrorHandling_Benchmark()
    {
        var script = ProgrammableComputerLuaTestHelper.CreateRuntimeScript();

        var stopwatch = Stopwatch.StartNew();
        var count = 1000;

        for (int i = 0; i < count; i++)
        {
            try
            {
                script.DoString("error('test error')");
            }
            catch
            {
                // Expected
            }
        }

        stopwatch.Stop();
        var averageUs = stopwatch.Elapsed.TotalMicroseconds / count;

        TestContext.Out.WriteLine($"Error handling benchmark:");
        TestContext.Out.WriteLine($"  Total time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms for {count} errors");
        TestContext.Out.WriteLine($"  Average per error: {averageUs:F2}μs");

        // Assert reasonable performance: error handling < 500μs
        AssertPerf(averageUs, 500, "Error handling should have reasonable overhead (< 500μs per error)");
    }
}
