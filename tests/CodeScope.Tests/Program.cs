using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Reflection;

namespace CodeScope.Tests;

/// <summary>Entry point for the dependency-free test suite.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("CodeScope test suite");
        Console.WriteLine("====================");
        Console.WriteLine($"  runtime : {Environment.Version}");
        Console.WriteLine($"  backend : {Path.Combine(AppContext.BaseDirectory, "backend")}");

        var unit = new TestHarness("unit");
        UnitTests.Run(unit);

        var integration = new TestHarness("integration");
        IntegrationTests.Run(integration);

        var failures = unit.Failures + integration.Failures;
        var passed = unit.Passed + integration.Passed;

        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        if (failures == 0)
        {
            Console.WriteLine($"  ALL GREEN — {passed} checks passed");
            return 0;
        }

        Console.WriteLine($"  {passed} passed, {failures} FAILED");
        return 1;
    }
}