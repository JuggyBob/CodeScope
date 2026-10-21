using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Diagnostics;

namespace CodeScope.Tests;

/// <summary>
/// A dependency-free test runner: no NuGet, no test SDK, just a console program
/// that returns a non-zero exit code when anything fails. That keeps the suite
/// runnable on a machine with no package feed at all.
/// </summary>
public sealed class TestHarness
{
    private readonly List<(string Name, string Message)> _failures = new();
    private readonly List<string> _passed = new();
    private readonly string _suite;
    private readonly Stopwatch _watch = Stopwatch.StartNew();

    public TestHarness(string suite)
    {
        _suite = suite;
        Console.WriteLine();
        Console.WriteLine($"── {suite} {new string('─', Math.Max(0, 56 - suite.Length))}");
    }

    public int Failures => _failures.Count;

    public int Passed => _passed.Count;

    /// <summary>Run one named test, catching any exception as a failure.</summary>
    public void Test(string name, Action body)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            body();
            _passed.Add(name);
            Console.WriteLine($"  PASS  {name} ({watch.ElapsedMilliseconds} ms)");
        }
        catch (AssertionException ex)
        {
            _failures.Add((name, ex.Message));
            Console.WriteLine($"  FAIL  {name} ({watch.ElapsedMilliseconds} ms)");
            Console.WriteLine($"        {ex.Message}");
        }
        catch (Exception ex)
        {
            _failures.Add((name, ex.ToString()));
            Console.WriteLine($"  ERROR {name} ({watch.ElapsedMilliseconds} ms)");
            Console.WriteLine($"        {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
    }

    /// <summary>Print a summary and return the process exit code.</summary>
    public int Summarise()
    {
        _watch.Stop();
        Console.WriteLine();
        Console.WriteLine(new string('─', 64));

        if (_failures.Count == 0)
        {
            Console.WriteLine($"  {_suite}: {_passed.Count} passed in {_watch.Elapsed.TotalSeconds:0.0}s");
            return 0;
        }

        Console.WriteLine($"  {_suite}: {_passed.Count} passed, {_failures.Count} FAILED in {_watch.Elapsed.TotalSeconds:0.0}s");
        Console.WriteLine();
        foreach (var (name, message) in _failures)
        {
            Console.WriteLine($"  ✗ {name}");
            Console.WriteLine($"      {message.Replace("\n", "\n      ")}");
        }

        return 1;
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new AssertionException(message);
        }
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException($"{message}\n      expected: {Format(expected)}\n      actual:   {Format(actual)}");
        }
    }

    public static void NotEqual<T>(T unexpected, T actual, string message)
    {
        if (EqualityComparer<T>.Default.Equals(unexpected, actual))
        {
            throw new AssertionException($"{message}\n      both were: {Format(actual)}");
        }
    }

    public static void NotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new AssertionException(message);
        }
    }

    public static void Null(object? value, string message)
    {
        if (value is not null)
        {
            throw new AssertionException($"{message} (was: {value})");
        }
    }

    public static void Contains(string haystack, string needle, string message)
    {
        if (haystack is null || !haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssertionException($"{message}\n      looked for: {needle}");
        }
    }

    public static void DoesNotContain(string haystack, string needle, string message)
    {
        if (haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssertionException($"{message}\n      unexpectedly found: {needle}");
        }
    }

    public static void GreaterThan(long value, long floor, string message)
    {
        if (value <= floor)
        {
            throw new AssertionException($"{message} ({value} <= {floor})");
        }
    }

    private static string Format<T>(T value) => value switch
    {
        null => "<null>",
        string s => "\"" + s + "\"",
        _ => value.ToString() ?? "<null>",
    };
}

public sealed class AssertionException : Exception
{
    public AssertionException(string message)
        : base(message)
    {
    }
}