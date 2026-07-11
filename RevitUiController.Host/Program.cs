using RevitUiController.Core;
using RevitUiController.Core.Models;
using RevitUiController.Revit;

namespace RevitUiController.Host;

public static class Program
{
    private static CommandRegistry Registry { get; } = new();
    private static CancellationTokenSource Cts { get; } = new();

    public static async Task<int> Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cts.Cancel();
        };

        RegisterBuiltinAliases();
        LoadPlugins();

        var cleanArgs = ParseGlobalFlags(args);

        if (cleanArgs.Length == 0 || (cleanArgs.Length == 1 && (cleanArgs[0] == "--help" || cleanArgs[0] == "-h" || cleanArgs[0] == "help")))
        {
            PrintHelp();
            return 0;
        }

        if (cleanArgs.Length > 1 && (cleanArgs[0] == "--help" || cleanArgs[0] == "-h" || cleanArgs[0] == "help"))
        {
            PrintCommandHelp(cleanArgs[1]);
            return 0;
        }

        var cmdName = cleanArgs[0].ToLowerInvariant();

        var cmd = Registry.GetCommand(cmdName);
        if (cmd == null)
        {
            Console.Error.WriteLine($"Unknown command: {cmdName}");
            PrintHelp();
            return 1;
        }

        var windowManager = new DesktopWindowManager();

        WindowSession? session = null;

        if (CoreSettings.UseActiveWindow)
        {
            session = await WindowSession.ConnectToActive(ct: Cts.Token);
        }
        else if (CoreSettings.TargetPid.HasValue || CoreSettings.ProcessName != "Revit")
        {
            session = await WindowSession.ConnectToProcess(CoreSettings.TargetPid, CoreSettings.ProcessName, CoreSettings.ConnectTimeoutSec, Cts.Token);
        }
        else if (!string.IsNullOrEmpty(CoreSettings.WindowTitle))
        {
            session = await WindowSession.ConnectByTitle(CoreSettings.WindowTitle, CoreSettings.ConnectTimeoutSec, Cts.Token);
        }
        else
        {
            session = await WindowSession.ConnectToProcess(CoreSettings.TargetPid, CoreSettings.ProcessName, CoreSettings.ConnectTimeoutSec, Cts.Token);
        }

        if (session == null)
            return 1;

        CoreSettings.CurrentSession = session;
        WinAppDriverClient.CurrentAutomation = session.Automation;

        if (!UiMap.IsLoaded)
        {
            UiMap.TryLoadDefault();
        }

        SessionContext.ActiveHwnd = session.Process?.MainWindowHandle.ToInt64();
        SessionContext.ActivePid = session.Process?.Id;
        SessionContext.ActiveProcessName = session.Process?.ProcessName;

        using (session)
        using (windowManager)
        {
            var exitCode = await cmd.ExecuteAsync(session.MainWindow, cleanArgs.Skip(1).ToArray(), Cts.Token);
            return exitCode;
        }
    }

    private static void LoadPlugins()
    {
        var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        if (!Directory.Exists(pluginsDir))
            return;

        foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                var asm = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                foreach (var type in asm.GetExportedTypes())
                {
                    if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                        {
                            plugin.RegisterCommands(Registry);
                            LoggingService.Info("Host", $"Loaded plugin: {plugin.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warn("Host", $"Failed to load plugin assembly {dll}: {ex.Message}");
            }
        }
    }

    private static void RegisterBuiltinAliases()
    {
        Registry.RegisterAlias("lw", "list-windows");
        Registry.RegisterAlias("lc", "list-controls");
        Registry.RegisterAlias("sv", "switch-view");
        Registry.RegisterAlias("rt", "ribbon-tabs");
        Registry.RegisterAlias("rb", "rb");
        Registry.RegisterAlias("sl", "script-list");
        Registry.RegisterAlias("slog", "script-log");
        Registry.RegisterAlias("sdiff", "script-diff");
        Registry.RegisterAlias("cr", "combo-read");
        Registry.RegisterAlias("gr", "grid-read");
        Registry.RegisterAlias("li", "list-items");
        Registry.RegisterAlias("tr", "table-read");
        Registry.RegisterAlias("fa", "find-all");
        Registry.RegisterAlias("cg", "clipboard-get");
        Registry.RegisterAlias("cs", "clipboard-set");
        Registry.RegisterAlias("kc", "key-combo");
        Registry.RegisterAlias("sr", "screenshot-region");
        Registry.RegisterAlias("hr", "highlight-region");
        Registry.RegisterAlias("ri", "revit-instances");
    }

    private static string[] ParseGlobalFlags(string[] args)
    {
        var result = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--pretty")
                CoreSettings.IsPretty = true;
            else if (args[i] == "--screenshot")
                CoreSettings.IsScreenshot = true;
            else if (args[i] == "--verbosity" && i + 1 < args.Length)
                CoreSettings.Verbosity = args[++i];
            else if (args[i] == "--pid" && i + 1 < args.Length && int.TryParse(args[++i], out var pid))
                CoreSettings.TargetPid = pid;
            else if (args[i] == "--process-name" && i + 1 < args.Length)
                CoreSettings.CurrentProfile = new GenericProfile(args[++i]);
            else if (args[i] == "--window-title" && i + 1 < args.Length)
                CoreSettings.WindowTitle = args[++i];
            else if (args[i] == "--active")
                CoreSettings.UseActiveWindow = true;
            else if (args[i] == "--connect-timeout" && i + 1 < args.Length && int.TryParse(args[++i], out var timeout))
                CoreSettings.ConnectTimeoutSec = timeout;
            else if (args[i] == "--non-interactive")
                CoreSettings.IsNonInteractive = true;
            else if (args[i] == "--uia-only")
            {
                CoreSettings.IsUiaOnly = true;
                WinAppDriverClient.Current = new WinAppDriverClient();
            }
            else if (args[i] == "--profile" && i + 1 < args.Length)
            {
                var profileName = args[++i].ToLowerInvariant();
                if (profileName == "revit")
                {
                    CoreSettings.CurrentProfile = new RevitProfile();
                }
                else
                {
                    CoreSettings.CurrentProfile = new GenericProfile(profileName);
                }
            }
            else
                result.Add(args[i]);
        }
        return result.ToArray();
    }

    private static void PrintCommandHelp(string commandName)
    {
        var cmd = Registry.GetCommand(commandName);
        if (cmd != null)
        {
            Console.WriteLine($"Command: {cmd.Name}");
            Console.WriteLine($"Description: {cmd.Description}");
            Console.WriteLine($"Usage: {cmd.Usage}");
        }
        else
        {
            Console.Error.WriteLine($"Unknown command: {commandName}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
RevitUiController — FlaUI-based UI automation tool
Use --help <command> for command-specific help.
""");
    }
}
