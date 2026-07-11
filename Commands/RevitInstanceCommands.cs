using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class RevitInstancesCommand : UiCommandBase
{
    public override string Name => "revit-instances";
    public override string Description => "List all running Revit instances (PID, version, title, project)";
    public override string Usage => "revit-instances (ri)";

    protected override Task<CommandResult> ExecuteInternalAsync(AutomationElement revitWindow, string[] args, CancellationToken ct)
    {
        var manager = Program.InstanceManager;
        var instances = manager.ListInstances();

        var data = instances.Select(i => new
        {
            pid = i.Pid,
            year = i.Year == 0 ? "?" : i.Year.ToString(),
            title = i.Title,
            project = i.ProjectPath,
            connected = i.IsConnected
        }).ToList();

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Data = new { count = data.Count, instances = data }
        });
    }
}

public class RevitLaunchCommand : UiCommandBase
{
    public override string Name => "revit-launch";
    public override string Description => "Launch a specific Revit version, optionally opening a project";
    public override string Usage => "revit-launch --version 2026 [--project <path.rvt>]";

    protected override async Task<CommandResult> ExecuteInternalAsync(AutomationElement revitWindow, string[] args, CancellationToken ct)
    {
        int? year = null;
        string? projectPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--version" && i + 1 < args.Length && int.TryParse(args[++i], out var y))
                year = y;
            else if (args[i] == "--project" && i + 1 < args.Length)
                projectPath = args[++i];
        }

        if (year == null)
            return new CommandResult { Success = false, Error = "Missing --version argument. Usage: revit-launch --version 2026 [--project <path.rvt>]" };

        var manager = Program.InstanceManager;
        var process = await manager.LaunchInstance(year.Value, projectPath);

        return new CommandResult
        {
            Success = process != null,
            Error = process == null ? $"Failed to launch Revit {year}" : null,
            Data = process != null ? new { year = year.Value, pid = process.Id, project = projectPath } : null
        };
    }
}

public class MultiExecCommand : UiCommandBase
{
    public override string Name => "multi-exec";
    public override string Description => "Execute a command on ALL running Revit instances";
    public override string Usage => "multi-exec --all <command> [args...]";

    protected override async Task<CommandResult> ExecuteInternalAsync(AutomationElement revitWindow, string[] args, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] != "--all")
            return new CommandResult { Success = false, Error = "Usage: multi-exec --all <command> [args...]" };

        var cmdArgs = args.Skip(1).ToArray();
        if (cmdArgs.Length == 0)
            return new CommandResult { Success = false, Error = "Missing command name. Usage: multi-exec --all <command> [args...]" };

        var commandName = cmdArgs[0];
        var commandArgs = cmdArgs.Skip(1).ToArray();

        var manager = Program.InstanceManager;
        var exitCode = await manager.ExecuteOnAll(commandName, commandArgs);

        return new CommandResult
        {
            Success = exitCode == 0,
            Error = exitCode != 0 ? "One or more instances returned non-zero exit code" : null,
            Data = new { command = commandName, exitCode }
        };
    }
}

public class SessionSwitchCommand : UiCommandBase
{
    public override string Name => "session-switch";
    public override string Description => "Switch active session to another Revit instance by PID";
    public override string Usage => "session-switch <pid>";

    protected override Task<CommandResult> ExecuteInternalAsync(AutomationElement revitWindow, string[] args, CancellationToken ct)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var pid))
            return Task.FromResult(new CommandResult { Success = false, Error = "Usage: session-switch <pid>" });

        var manager = Program.InstanceManager;
        var session = manager.GetSession(pid);

        if (session == null)
            LoggingService.Error("RevitInstanceCommands", $"No active session for PID {pid}. Connecting...");

        manager.SwitchToInstance(pid);

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Data = new { pid, activePid = SessionContext.ActivePid }
        });
    }
}
