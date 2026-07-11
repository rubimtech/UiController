using System.IO;
using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class AllureSetupCommand : ICommand
{
    public string Name => "allure-setup";
    public string Description => "Initialize Allure reporting for test results";
    public string Usage => "allure-setup [--output <dir>]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var outputDir = "allure-results";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length)
                outputDir = args[++i];
        }

        try
        {
            Directory.CreateDirectory(outputDir);
            var historyDir = Path.Combine(outputDir, "history");
            Directory.CreateDirectory(historyDir);

            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "allure-setup",
                Success = true,
                Data = new { outputDir, absolutePath = Path.GetFullPath(outputDir) }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("AllureSetupError", outputDir, [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}

public class AllureReportCommand : ICommand
{
    public string Name => "allure-report";
    public string Description => "Generate Allure report from test results: allure-report [--input <dir>] [--output <dir>]";
    public string Usage => "allure-report [--input <dir>] [--output <dir>]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var resultsDir = "allure-results";
        var reportDir = "allure-report";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length)
                resultsDir = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length)
                reportDir = args[++i];
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "allure",
                Arguments = $"generate \"{resultsDir}\" --output \"{reportDir}\" --clean",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(psi);
            if (process == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "allure-report",
                    Success = false,
                    Error = "Allure CLI not found. Install: scoop install allure"
                }, CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            var ok = process.ExitCode == 0;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "allure-report",
                Success = ok,
                Error = ok ? null : error,
                Data = new { resultsDir, reportDir, exitCode = process.ExitCode }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("AllureReportError", reportDir, [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
