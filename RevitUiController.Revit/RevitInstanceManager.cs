using System.Diagnostics;
using System.IO;
using UiController.Core;
using RevitUiController.Revit.Models;

namespace RevitUiController.Revit;

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
                if (!hasWindow || string.IsNullOrEmpty(title)) continue;
                var year = RevitVersionProfile.DetectYearFromTitle(title);
                var projectPath = ExtractProjectPath(title);
                results.Add(new RevitInstance(p.Id, year, title, projectPath, _sessions.ContainsKey(p.Id)));
            }
            catch { }
        }
        return results;
    }

    public async Task<WindowSession?> ConnectToInstance(int pid)
    {
        if (_sessions.TryGetValue(pid, out var existing))
        {
            try { if (!existing.Process.HasExited) return existing; }
            catch { }
            _sessions.Remove(pid);
            existing.Dispose();
        }
        var session = await WindowSession.ConnectToProcess(pid, "Revit", 30);
        if (session != null) _sessions[pid] = session;
        return session;
    }

    public WindowSession? GetSession(int pid) =>
        _sessions.TryGetValue(pid, out var s) ? s : null;

    public async Task<WindowSession?> SwitchToInstance(int pid)
    {
        DisconnectAll();
        return await ConnectToInstance(pid);
    }

    public async Task<Process?> LaunchInstance(int year, string? projectPath = null)
    {
        var revitPath = $@"C:\Program Files\Autodesk\Revit {year}\Revit.exe";
        if (!File.Exists(revitPath))
        {
            LoggingService.Warn("RevitInstanceManager", $"Revit {year} executable not found.");
            return null;
        }
        if (!string.IsNullOrEmpty(projectPath) && !File.Exists(projectPath))
        {
            LoggingService.Warn("RevitInstanceManager", $"Project file not found: {projectPath}");
            return null;
        }
        try
        {
            var psi = new ProcessStartInfo(revitPath) { UseShellExecute = true, Arguments = projectPath ?? "" };
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
        if (instances.Count == 0) { LoggingService.Error("RevitInstanceManager", "No running Revit instances found."); return 1; }
        var foundCommand = CoreSettings.CommandRegistry?.GetCommand(commandName);
        if (foundCommand == null) { LoggingService.Error("RevitInstanceManager", $"Unknown command: {commandName}"); return 1; }
        int exitCode = 0;
        foreach (var inst in instances)
        {
            LoggingService.Error("RevitInstanceManager", $"--- [{inst.Pid}] Revit {inst.Year} ---");
            var session = await ConnectToInstance(inst.Pid);
            if (session == null) { exitCode = 1; continue; }
            var prevSession = CoreSettings.CurrentSession;
            CoreSettings.CurrentSession = session;
            SessionContext.ActiveHwnd = session.Process?.MainWindowHandle.ToInt64();
            SessionContext.ActivePid = session.Process?.Id;
            SessionContext.ActiveProcessName = session.Process?.ProcessName;
            try
            {
                var code = await foundCommand.ExecuteAsync(session.MainWindow, args, CoreSettings.Cts.Token);
                if (code != 0) exitCode = code;
            }
            catch (Exception ex) { LoggingService.Error("RevitInstanceManager", $"Error on PID {inst.Pid}: {ex.Message}"); exitCode = 1; }
            finally { CoreSettings.CurrentSession = prevSession; }
        }
        return exitCode;
    }

    public void Disconnect(int pid)
    {
        if (_sessions.TryGetValue(pid, out var session)) { _sessions.Remove(pid); session.Dispose(); }
    }

    public void DisconnectAll()
    {
        foreach (var s in _sessions.Values) s.Dispose();
        _sessions.Clear();
    }

    public async Task<(WindowSession session, int pid)> GetOrConnectPrimary()
    {
        var instances = ListInstances();
        if (instances.Count == 0) throw new InvalidOperationException("No Revit instances found.");
        foreach (var inst in instances)
            if (_sessions.ContainsKey(inst.Pid)) return (_sessions[inst.Pid], inst.Pid);
        var session = await ConnectToInstance(instances[0].Pid);
        return (session!, instances[0].Pid);
    }

    private static string ExtractProjectPath(string title)
    {
        var idx = title.IndexOf(" - ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        var project = title[..idx].Trim();
        if (project.Contains('\\') || project.Contains('/')) return project;
        if (project.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) || project.EndsWith(".rte", StringComparison.OrdinalIgnoreCase)) return project;
        return "";
    }
}
