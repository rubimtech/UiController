using Microsoft.Extensions.DependencyInjection;
using UiController.Core;
using UiController.Core.Models;
using UiController.Core.Services;
using RevitUiController.Revit;

namespace UiController.Host;

public static class Program
{
    private static CommandRegistry Registry { get; } = new();
    private static CancellationTokenSource Cts { get; } = new();
    private static IServiceProvider? ServiceProvider { get; set; }
    private static ConfigModel? _config;

    private static readonly Dictionary<string, string> DeprecatedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["revit-restart"] = "Use  app-restart --profile revit  instead.",
        ["revit-instances"] = "Use  app-instances --profile revit  instead.",
        ["revit-api"] = "Use  pipe-api  instead.",
    };

    public static async Task<int> Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cts.Cancel();
        };

        _config = ConfigLoader.Load();
        CoreSettings.Verbosity = _config.Defaults.Verbosity;
        CoreSettings.ConnectTimeoutSec = _config.Defaults.ConnectTimeout;

        ParseProfileFlag(args);

        var sp = ConfigureServices(args);
        ServiceProvider = sp;

        RegisterBuiltinAliases();
        RegisterBuiltinCommands(sp);
        LoadPlugins(sp);

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

        if (DeprecatedCommands.TryGetValue(cmdName, out var deprecationMsg))
        {
            Console.Error.WriteLine($"[DEPRECATED] '{cmdName}' is deprecated. {deprecationMsg}");
        }

        var cmd = ResolveCommand(cmdName, sp);
        if (cmd == null)
        {
            Console.Error.WriteLine($"Unknown command: {cmdName}");
            PrintHelp();
            return 1;
        }

        var windowManager = new DesktopWindowManager();

        var provider = sp.GetRequiredService<IAutomationProvider>();
        WindowSession? session = null;

        if (CoreSettings.UseActiveWindow)
        {
            session = await WindowSession.ConnectToActive(provider, ct: Cts.Token);
        }
        else if (CoreSettings.TargetPid.HasValue || CoreSettings.ProcessName != "Revit")
        {
            session = await WindowSession.ConnectToProcess(provider, CoreSettings.TargetPid, CoreSettings.ProcessName, CoreSettings.ConnectTimeoutSec, Cts.Token);
        }
        else if (!string.IsNullOrEmpty(CoreSettings.WindowTitle))
        {
            session = await WindowSession.ConnectByTitle(CoreSettings.WindowTitle, provider, CoreSettings.ConnectTimeoutSec, Cts.Token);
        }
        else
        {
            session = await WindowSession.ConnectToProcess(provider, CoreSettings.TargetPid, CoreSettings.ProcessName, CoreSettings.ConnectTimeoutSec, Cts.Token);
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

    private static IServiceProvider ConfigureServices(string[] args)
    {
        var services = new ServiceCollection();

        var provider = CreateAutomationProvider(args);
        services.AddSingleton(provider);
        services.AddSingleton<IAutomationProvider>(provider);

        services.AddSingleton<ILoggingService, LoggingServiceWrapper>();
        services.AddSingleton<IAutomationService, AutomationService>();
        services.AddSingleton<IScreenshotService, ScreenshotService>();
        services.AddSingleton<IOutputFormatterService, OutputFormatterService>();
        services.AddSingleton<IUiMapService, UiMapService>();
        services.AddSingleton<ISafetyGuardService, SafetyGuardService>();
        services.AddSingleton<IEventService, EventServiceWrapper>();
        services.AddSingleton<IRecorderService, RecorderServiceWrapper>();
        services.AddSingleton<ISessionContextService, SessionContextService>();
        services.AddSingleton<ICvMatchService, CvMatchService>();
        services.AddSingleton<ILlmVisionService, LlmVisionService>();

        services.AddSingleton(Registry);

        return services.BuildServiceProvider();
    }

    private static IAutomationProvider CreateAutomationProvider(string[] args)
    {
        var providerName = "uia3";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--provider" && i + 1 < args.Length)
            {
                providerName = args[i + 1].ToLowerInvariant();
                break;
            }
        }

        return providerName switch
        {
            "wad" => new WinAppDriverProvider(new WinAppDriverClient()),
            "composite" => new CompositeAutomationProvider(
                new UIA3AutomationProvider(),
                new WinAppDriverProvider(new WinAppDriverClient())),
            _ => new UIA3AutomationProvider()
        };
    }

    private static void RegisterLauncher(IServiceCollection services)
    {
        if (CoreSettings.CurrentProfile is RevitProfile)
        {
            services.AddSingleton<IApplicationLauncher, RevitLauncher>();
        }
        else
        {
            services.AddSingleton<IApplicationLauncher, GenericLauncher>();
        }
    }

    private static void ParseProfileFlag(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--profile" && i + 1 < args.Length)
            {
                var profileName = args[++i].ToLowerInvariant();
                if (profileName == "revit")
                {
                    CoreSettings.CurrentProfile = new RevitProfile();
                }
                else if (_config?.Profiles.TryGetValue(profileName, out var cfg) == true)
                {
                    CoreSettings.CurrentProfile = ConfigLoader.CreateProfile(profileName, cfg);
                }
                else
                {
                    CoreSettings.CurrentProfile = new GenericProfile(profileName);
                }
                break;
            }
        }
    }

    private static void RegisterBuiltinCommands(IServiceProvider sp)
    {
        foreach (var type in typeof(ICommand).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ICommand).IsAssignableFrom(t)))
        {
            Registry.RegisterType(type);
        }
    }

    private static ICommand? ResolveCommand(string name, IServiceProvider sp)
    {
        var cmdType = Registry.GetCommandType(name);
        if (cmdType != null)
        {
            try
            {
                return ActivatorUtilities.CreateInstance(sp, cmdType) as ICommand;
            }
            catch
            {
            }
        }

        return Registry.GetCommand(name);
    }

    private static void LoadPlugins(IServiceProvider sp)
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
                        if (ActivatorUtilities.CreateInstance(sp, type) is IPlugin plugin)
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
        Registry.RegisterAlias("ws", "window-screenshot");
        Registry.RegisterAlias("ps", "process-list");
        Registry.RegisterAlias("mc", "menu-click");
        Registry.RegisterAlias("ml", "menu-list");
        Registry.RegisterAlias("trs", "tree-select");
        Registry.RegisterAlias("tbs", "tab-select");
        Registry.RegisterAlias("scr", "scroll");
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
            else if (args[i] == "--provider" && i + 1 < args.Length)
            {
                var providerName = args[++i].ToLowerInvariant();
                CoreSettings.AutomationProviderName = providerName;
            }
            else if (args[i] == "--profile" && i + 1 < args.Length)
            {
                var profileName = args[++i].ToLowerInvariant();
                if (profileName == "revit")
                {
                    CoreSettings.CurrentProfile = new RevitProfile();
                }
                else if (_config?.Profiles.TryGetValue(profileName, out var cfg) == true)
                {
                    CoreSettings.CurrentProfile = ConfigLoader.CreateProfile(profileName, cfg);
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
        var isRevitProfile = CoreSettings.CurrentProfile is RevitProfile;
        var coreAssembly = typeof(ICommand).Assembly;

        if (commandName.Equals("generic", StringComparison.OrdinalIgnoreCase))
        {
            var genericCommands = Registry.AllCommandTypes
                .Where(kvp => kvp.Value.Assembly == coreAssembly)
                .Select(kvp => Registry.GetCommand(kvp.Key) ?? Activator.CreateInstance(kvp.Value) as ICommand)
                .Where(c => c != null)
                .OrderBy(c => c!.Name)
                .ToList();

            Console.WriteLine("=== Generic Commands ===");
            foreach (var cmd in genericCommands)
            {
                Console.WriteLine($"  {cmd!.Name,-22} {cmd.Description}");
                Console.WriteLine($"      Usage: {cmd.Usage}");
            }
            return;
        }

        if (commandName.Equals("revit", StringComparison.OrdinalIgnoreCase))
        {
            var revitCommands = Registry.AllCommandTypes
                .Where(kvp => kvp.Value.Assembly != coreAssembly)
                .Select(kvp => Registry.GetCommand(kvp.Key) ?? Activator.CreateInstance(kvp.Value) as ICommand)
                .Where(c => c != null)
                .OrderBy(c => c!.Name)
                .ToList();

            Console.WriteLine("=== Revit Plugin Commands ===");
            foreach (var cmd in revitCommands)
            {
                Console.WriteLine($"  {cmd!.Name,-22} {cmd.Description}");
                Console.WriteLine($"      Usage: {cmd.Usage}");
            }
            return;
        }

        var cmdInstance = Registry.GetCommand(commandName);
        if (cmdInstance != null)
        {
            Console.WriteLine($"Command: {cmdInstance.Name}");
            Console.WriteLine($"Description: {cmdInstance.Description}");
            Console.WriteLine($"Usage: {cmdInstance.Usage}");
        }
        else
        {
            Console.Error.WriteLine($"Unknown command: {commandName}");
        }
    }

    private static void PrintHelp()
    {
        var isRevitProfile = CoreSettings.CurrentProfile is RevitProfile;
        var coreAssembly = typeof(ICommand).Assembly;

        var genericCommands = Registry.AllCommandTypes
            .Where(kvp => kvp.Value.Assembly == coreAssembly)
            .Select(kvp => Registry.GetCommand(kvp.Key) ?? Activator.CreateInstance(kvp.Value) as ICommand)
            .Where(c => c != null)
            .OrderBy(c => c!.Name)
            .ToList();

        var revitCommands = Registry.AllCommandTypes
            .Where(kvp => kvp.Value.Assembly != coreAssembly)
            .Select(kvp => Registry.GetCommand(kvp.Key) ?? Activator.CreateInstance(kvp.Value) as ICommand)
            .Where(c => c != null)
            .OrderBy(c => c!.Name)
            .ToList();

        Console.WriteLine("Ui Controller — Windows UI Automation CLI");
        Console.WriteLine();

        Console.WriteLine("=== Generic Commands ===");
        foreach (var cmd in genericCommands)
        {
            Console.WriteLine($"  {cmd!.Name,-22} {cmd.Description}");
        }

        if (isRevitProfile && revitCommands.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("=== Revit Plugin Commands ===");
            foreach (var cmd in revitCommands)
            {
                Console.WriteLine($"  {cmd!.Name,-22} {cmd.Description}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Use  --help <command>  for command-specific help.");
        if (revitCommands.Count > 0 && !isRevitProfile)
        {
            Console.WriteLine("For Revit-specific help:  --profile revit --help");
        }
    }
}
