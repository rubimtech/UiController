using System.Diagnostics;
using RevitUiController.Models;
using UiController.Core.Models;

namespace RevitUiController;

public class RevitInstanceManager
{
    private readonly Dictionary<int, WindowSession> _sessions = new();

    public List<RevitInstance> ListInstances()
    {
        var results = new List<RevitInstance>();

        foreach (var p in Process.GetProcessesByName("Revit"))
        {
            try
            {
                var title = p.MainWindowTitle ?? "";
                var hasWindow = p.MainWindowHandle != IntPtr.Zero;
                if (!hasWindow || string.IsNullOrEmpty(title))
                    continue;

                var year = DetectYearFromTitle(title);
                var projectPath = ExtractProjectPath(title);

                results.Add(new RevitInstance(
                    p.Id,
                    year,
                    title,
                    projectPath,
                    _sessions.ContainsKey(p.Id)
                ));
            }
            catch { }
        }

        return results;
    }

    public async Task<WindowSession?> ConnectToInstance(int pid)
    {
        if (_sessions.TryGetValue(pid, out var existing))
        {
            try
            {
                if (!existing.Process.HasExited)
                    return existing;
            }
            catch { }
            _sessions.Remove(pid);
            existing.Dispose();
        }

        var session = await WindowSession.ConnectToProcess(pid, "Revit", 30);
        if (session != null)
            _sessions[pid] = session;

        return session;
    }

    public WindowSession? GetSession(int pid)
    {
        return _sessions.TryGetValue(pid, out var s) ? s : null;
    }

    public void SwitchToInstance(int pid)
    {
        var session = GetSession(pid);
        if (session == null)
        {
            LoggingService.Error("RevitInstanceManager", $"No active session for PID {pid}. Use 'revit-instances' to list and connect.");
            return;
        }

        Program.CurrentSession = session;
        Program.CurrentWindowSession = session;
        SessionContext.ActiveHwnd = session.Process?.MainWindowHandle.ToInt64();
        SessionContext.ActivePid = session.Process?.Id;
        SessionContext.ActiveProcessName = session.Process?.ProcessName;

        var year = DetectYearFromTitle(session.Process?.MainWindowTitle ?? "");
        LoggingService.Info("RevitInstanceManager", $"Switched to Revit {year} (PID={pid})");
    }

    public async Task<Process?> LaunchInstance(int year, string? projectPath = null)
    {
        var possiblePaths = new[]
        {
            $@"C:\Program Files\Autodesk\Revit {year}\Revit.exe",
        };

        var revitPath = possiblePaths.FirstOrDefault(File.Exists);
        if (revitPath == null)
        {
            LoggingService.Warn("RevitInstanceManager", $"Revit {year} executable not found at {possiblePaths[0]}.");
            return null;
        }

        if (!string.IsNullOrEmpty(projectPath) && !File.Exists(projectPath))
        {
            LoggingService.Warn("RevitInstanceManager", $"Project file not found: {projectPath}");
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo(revitPath)
            {
                UseShellExecute = true,
                Arguments = projectPath ?? ""
            };
            var process = Process.Start(psi);
            Console.WriteLine($"Started Revit {year}: {revitPath} (PID={process?.Id})");
            return process;
        }
        catch (Exception ex)
        {
            LoggingService.Error("RevitInstanceManager", $"Failed to start Revit {year}: {ex.Message}");
            return null;
        }
    }

    public async Task<int> ExecuteOnAll(string commandName, string[] args)
    {
        var instances = ListInstances();
        if (instances.Count == 0)
        {
            LoggingService.Error("RevitInstanceManager", "No running Revit instances found.");
            return 1;
        }

        var foundCommand = Program.GetCommand(commandName);
        if (foundCommand == null)
        {
            LoggingService.Error("RevitInstanceManager", $"Unknown command: {commandName}");
            return 1;
        }

        int exitCode = 0;

        foreach (var inst in instances)
        {
            LoggingService.Error("RevitInstanceManager", $"--- [{inst.Pid}] Revit {inst.Year} ---");

            var session = await ConnectToInstance(inst.Pid);
            if (session == null)
            {
                LoggingService.Error("RevitInstanceManager", $"  Failed to connect to PID {inst.Pid}");
                exitCode = 1;
                continue;
            }

            var prevSession = Program.CurrentSession;
            var prevWindowSession = Program.CurrentWindowSession;

            Program.CurrentSession = session;
            Program.CurrentWindowSession = session;
            SessionContext.ActiveHwnd = session.Process?.MainWindowHandle.ToInt64();
            SessionContext.ActivePid = session.Process?.Id;
            SessionContext.ActiveProcessName = session.Process?.ProcessName;

            try
            {
                var code = await foundCommand.ExecuteAsync(session.MainWindow, args, Program.Cts.Token);
                if (code != 0) exitCode = code;
            }
            catch (Exception ex)
            {
                LoggingService.Error("RevitInstanceManager", $"  Error on PID {inst.Pid}: {ex.Message}");
                exitCode = 1;
            }
            finally
            {
                Program.CurrentSession = prevSession;
                Program.CurrentWindowSession = prevWindowSession;
            }
        }

        return exitCode;
    }

    public void DisposeSession(int pid)
    {
        if (_sessions.TryGetValue(pid, out var session))
        {
            _sessions.Remove(pid);
            session.Dispose();
        }
    }

    public void DisposeAll()
    {
        foreach (var (_, s) in _sessions)
            s.Dispose();
        _sessions.Clear();
    }

    public static int DetectYearFromTitle(string title)
    {
        return RevitVersionProfile.DetectYearFromTitle(title);
    }

    private static string ExtractProjectPath(string title)
    {
        var idx = title.IndexOf(" - ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";

        var project = title[..idx].Trim();
        if (project.Contains('\\') || project.Contains('/'))
            return project;

        if (project.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ||
            project.EndsWith(".rte", StringComparison.OrdinalIgnoreCase))
            return project;

        return "";
    }
}
