using System.Diagnostics;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class ScriptListCommand : ICommand
{
    public string Name => "script-list";
    public string Description => "List available .rvs scripts in default search paths";
    public string Usage => "script-list [--path <dir>] [--git]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var customPath = ParseFlagValue(args, "--path");
        var gitOnly = args.Any(a => a == "--git");

        var files = new List<ScriptFileInfo>();

        if (customPath != null)
        {
            AddScriptsFrom(customPath, files);
        }
        else
        {
            AddScriptsFrom(Path.Combine(Directory.GetCurrentDirectory(), "scripts"), files);
            AddScriptsFrom(Path.Combine(Directory.GetCurrentDirectory(), "scenarios"), files);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddScriptsFrom(Path.Combine(localAppData, "ReVibe", "UiController", "scripts"), files);
        }

        HashSet<string>? trackedFiles = null;
        if (gitOnly)
        {
            var gitOutput = RunGit("ls-files '*.rvs'");
            trackedFiles = gitOutput != null
                ? new HashSet<string>(gitOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            files = files.Where(f => trackedFiles!.Contains(f.Path) || trackedFiles.Contains(Path.GetFileName(f.Path))).ToList();
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "script-list",
            Success = true,
            Data = new
            {
                count = files.Count,
                scripts = files.Select(f => new
                {
                    f.Path,
                    f.Name,
                    f.Size,
                    f.Modified,
                    gitTracked = trackedFiles?.Contains(f.Path) == true || trackedFiles?.Contains(Path.GetFileName(f.Path)) == true
                })
            }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }

    private static void AddScriptsFrom(string dir, List<ScriptFileInfo> files)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir, "*.rvs", SearchOption.AllDirectories))
        {
            var fi = new FileInfo(f);
            files.Add(new ScriptFileInfo
            {
                Path = f,
                Name = fi.Name,
                Size = fi.Length,
                Modified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            });
        }
    }

    private static string? ParseFlagValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static string? RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
            using var process = Process.Start(psi);
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output.TrimEnd() : null;
        }
        catch { return null; }
    }
}

public class ScriptLogCommand : ICommand
{
    public string Name => "script-log";
    public string Description => "Show git log for .rvs script files";
    public string Usage => "script-log [--file <path>] [--last N]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var file = ParseFlagValue(args, "--file");
        var last = ParseIntFlag(args, "--last") ?? 10;

        string? gitOutput;

        if (file != null)
        {
            if (!File.Exists(file))
            {
                Console.Write(OutputFormatter.FormatError("FileNotFound", file, new List<string> { "Check the file path" }, Program.GlobalOptions));
                return Task.FromResult(1);
            }
            gitOutput = RunGit($"log --oneline -{last} -- \"{file}\"");
        }
        else
        {
            gitOutput = RunGit($"log --oneline -{last} -- '*.rvs'");
        }

        if (gitOutput == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "script-log",
                Success = false,
                Error = "Git is not available or this is not a git repository",
                Data = new { hint = "Run 'git init' or install git" }
            }, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var entries = gitOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                return new { hash = parts.Length > 0 ? parts[0] : "", message = parts.Length > 1 ? parts[1] : "" };
            }).ToList();

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "script-log",
            Success = true,
            Data = new { count = entries.Count, entries }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }

    private static string? ParseFlagValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static int? ParseIntFlag(string[] args, string flag)
    {
        var val = ParseFlagValue(args, flag);
        return val != null && int.TryParse(val, out var n) ? n : null;
    }

    private static string? RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
            using var process = Process.Start(psi);
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output.TrimEnd() : null;
        }
        catch { return null; }
    }
}

public class ScriptDiffCommand : ICommand
{
    public string Name => "script-diff";
    public string Description => "Show git diff for .rvs script files";
    public string Usage => "script-diff [--file <path>] [--commit <hash>] [--last N]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var file = ParseFlagValue(args, "--file");
        var commit = ParseFlagValue(args, "--commit");
        var last = ParseIntFlag(args, "--last");

        string? gitOutput;
        string? commitMsg = null;

        if (commit != null)
        {
            if (file != null)
            {
                gitOutput = RunGit($"diff {commit}^ {commit} -- \"{file}\"");
                var msg = RunGit($"log --oneline -1 {commit}");
                commitMsg = msg;
            }
            else
            {
                gitOutput = RunGit($"show {commit} --stat -- '*.rvs'");
                var msg = RunGit($"log --oneline -1 {commit}");
                commitMsg = msg;
            }
        }
        else if (last.HasValue)
        {
            if (file != null)
            {
                gitOutput = RunGit($"diff HEAD~{last.Value} HEAD -- \"{file}\"");
            }
            else
            {
                gitOutput = RunGit($"diff HEAD~{last.Value} HEAD -- '*.rvs'");
            }
        }
        else
        {
            if (file != null)
            {
                gitOutput = RunGit($"diff HEAD -- \"{file}\"");
            }
            else
            {
                gitOutput = RunGit($"diff HEAD -- '*.rvs'");
            }
        }

        if (gitOutput == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "script-diff",
                Success = false,
                Error = "Git is not available, not a git repository, or no changes found",
                Data = new { hint = "Make sure git is installed and the repo has .rvs commits" }
            }, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "script-diff",
            Success = true,
            Data = new
            {
                file,
                diff = gitOutput,
                commitHash = commit,
                commitMessage = commitMsg
            }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }

    private static string? ParseFlagValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static int? ParseIntFlag(string[] args, string flag)
    {
        var val = ParseFlagValue(args, flag);
        return val != null && int.TryParse(val, out var n) ? n : null;
    }

    private static string? RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
            using var process = Process.Start(psi);
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output.TrimEnd() : null;
        }
        catch { return null; }
    }
}

public class ScriptFileInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string Modified { get; set; } = "";
}
