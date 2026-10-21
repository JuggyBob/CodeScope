using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace CodeScope;

/// <summary>Application entry point.</summary>
public partial class App : Application
{
    private static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodeScope",
        "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A desktop application must never disappear without a word: catch
        // anything that escapes the dispatcher and leave a readable trace.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        MessageBox.Show(
            $"CodeScope hit an unexpected error:\n\n{e.Exception.Message}\n\n" +
            $"Details were written to:\n{CrashLogPath}",
            "CodeScope",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Handle it: a rendering fault must not take the window down.
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.ExceptionObject as Exception);
    }

    private static void WriteCrashLog(Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(CrashLogPath)!;
            Directory.CreateDirectory(directory);

            var builder = new StringBuilder();
            builder.AppendLine($"---- {DateTimeOffset.Now:u} ----");
            builder.AppendLine(exception?.ToString() ?? "(no exception details)");

            // Keep the last few failures rather than growing without bound.
            var previous = File.Exists(CrashLogPath) ? File.ReadAllText(CrashLogPath) : string.Empty;
            var lines = previous.Split('\n');
            var trimmed = string.Join('\n', lines.Skip(Math.Max(0, lines.Length - 200)));
            File.WriteAllText(CrashLogPath, trimmed + builder);
        }
        catch
        {
            // Logging must never itself throw.
        }
    }
}