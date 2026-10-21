using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Text;

namespace CodeScope.Tests;

/// <summary>Creates the throwaway project trees the integration tests analyse.</summary>
public static class Fixtures
{
    /// <summary>A directory name with a space and non-ASCII characters in it.</summary>
    public static string AwkwardName => "proj ünïcode test";

    /// <summary>
    /// A small multi-language project with no Git repository, so the
    /// history-dependent sections have nothing to work with.
    /// </summary>
    public static string CreatePlainProject(string root)
    {
        var dir = Path.Combine(root, AwkwardName);
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        Directory.CreateDirectory(Path.Combine(dir, "web"));

        File.WriteAllText(Path.Combine(dir, "src", "main.go"),
            """
            package main

            import "fmt"

            // main greets the caller.
            func main() {
                for i := 0; i < 3; i++ {
                    if i%2 == 0 {
                        fmt.Println("even", i)
                    } else {
                        fmt.Println("odd", i)
                    }
                }
            }
            """);

        File.WriteAllText(Path.Combine(dir, "src", "helper.go"),
            """
            package main

            func add(a, b int) int {
                if a > b {
                    return a + b
                }
                return a - b
            }

            func mul(a, b int) int {
                for i := 0; i < b; i++ {
                    a = a * 2
                }
                return a
            }
            """);

        File.WriteAllText(Path.Combine(dir, "web", "index.html"),
            """
            <!doctype html>
            <html>
              <head><title>fixture</title></head>
              <body>
                <!-- a comment -->
                <div id="root"></div>
              </body>
            </html>
            """);

        File.WriteAllText(Path.Combine(dir, "web", "style.css"),
            """
            body {
              margin: 0;
              padding: 0;
            }

            #root {
              color: rebeccapurple;
            }
            """);

        File.WriteAllText(Path.Combine(dir, "README.md"), "# Fixture\n\nA tiny project used by the CodeScope test suite.\n");
        File.WriteAllText(Path.Combine(dir, "package-lock.json"), "{ \"lockfileVersion\": 3 }\n");

        return dir;
    }

    /// <summary>
    /// The same kind of project, wrapped in a Git repository with a handful of
    /// commits so the history sections have something real to report.
    /// </summary>
    public static string CreateGitProject(string root)
    {
        var dir = Path.Combine(root, "gitfixture");
        Directory.CreateDirectory(Path.Combine(dir, "lib"));

        for (var i = 0; i < 6; i++)
        {
            var body = new StringBuilder();
            body.AppendLine($"// revision {i}");
            body.AppendLine("package lib");
            body.AppendLine();
            body.AppendLine($"func Handle{i}(value int) int {{");
            for (var k = 0; k <= i; k++)
            {
                body.AppendLine($"    if value > {k} {{");
                body.AppendLine($"        value = value * {k + 2}");
                body.AppendLine("    }");
            }

            body.AppendLine("    return value");
            body.AppendLine("}");
            File.WriteAllText(Path.Combine(dir, "lib", $"handler{i}.go"), body.ToString());
        }

        File.WriteAllText(Path.Combine(dir, "lib", "shared.go"),
            """
            package lib

            func Shared(x int) int {
                if x < 0 {
                    return 0
                }
                return x
            }
            """);

        Run(dir, "git", "init", "--initial-branch=main");
        Run(dir, "git", "config", "user.name", "Ada Lovelace");
        Run(dir, "git", "config", "user.email", "ada@example.com");
        Run(dir, "git", "config", "commit.gpgsign", "false");

        for (var i = 0; i < 6; i++)
        {
            if (i == 3)
            {
                // A second author, so the author rollup and bus factor have
                // something to distinguish.
                Run(dir, "git", "config", "user.name", "Grace Hopper");
                Run(dir, "git", "config", "user.email", "grace@example.com");
            }

            File.WriteAllText(
                Path.Combine(dir, "lib", $"handler{i}.go"),
                File.ReadAllText(Path.Combine(dir, "lib", $"handler{i}.go")) + $"\n// touched by commit {i}\n");

            // Every commit also touches the shared helper: coupling only
            // appears when two files change in the same commits.
            File.WriteAllText(
                Path.Combine(dir, "lib", "shared.go"),
                File.ReadAllText(Path.Combine(dir, "lib", "shared.go")) + $"// shared revision {i}\n");

            Run(dir, "git", "add", "-A");
            Run(dir, "git", "commit", "-m", $"commit {i}");
        }

        return dir;
    }

    /// <summary>Run a program and fail loudly if it does not succeed.</summary>
    public static string Run(string workingDirectory, string fileName, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{fileName} {string.Join(' ', arguments)} failed ({process.ExitCode}):\n{stderr}\n{stdout}");
        }

        return stdout;
    }

    /// <summary>A fresh temporary workspace that is removed by <see cref="Cleanup"/>.</summary>
    public static string CreateWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "codescope-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// A folder with source files but deliberately no Git repository above it,
    /// used to prove the history sections degrade instead of inventing data.
    /// </summary>
    public static string CreateIsolatedProject(string root)
    {
        var dir = Path.Combine(root, "plain-nogit");
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "one.cs"),
            """
            class One
            {
                int Value(int x)
                {
                    if (x > 0) { return x; }
                    return -x;
                }
            }
            """);

        File.WriteAllText(Path.Combine(dir, "two.cs"),
            """
            class Two
            {
                int Twice(int x)
                {
                    return x * 2;
                }
            }
            """);

        return dir;
    }

    public static void Cleanup(string root)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            // Git marks its object files read-only; clear that before deleting.
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch (IOException)
                {
                    // Best effort.
                }
            }

            Directory.Delete(root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder must never fail the suite.
        }
    }
}