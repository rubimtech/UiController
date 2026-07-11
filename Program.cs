using FlaUI.UIA3;
using RevitUiController.Commands;
using RevitUiController.Models;

namespace RevitUiController;

public static class Program
{
    private static readonly Dictionary<string, ICommand> Commands = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lw"] = "list-windows",
        ["lc"] = "list-controls",
        ["sv"] = "switch-view",
        ["rt"] = "ribbon-tabs",
        ["rb"] = "rb",
        ["sl"] = "script-list",
        ["slog"] = "script-log",
        ["sdiff"] = "script-diff",
        ["cr"] = "combo-read",
        ["gr"] = "grid-read",
        ["li"] = "list-items",
        ["tr"] = "table-read",
        ["fa"] = "find-all",
        ["cg"] = "clipboard-get",
        ["cs"] = "clipboard-set",
        ["kc"] = "key-combo",
        ["sr"] = "screenshot-region",
        ["hr"] = "highlight-region",
        ["ri"] = "revit-instances",
    };

    public static CancellationTokenSource Cts { get; } = new();
    public static bool IsPretty { get; set; }
    public static bool IsScreenshot { get; set; }
    public static string Verbosity { get; set; } = "normal";
    public static int? TargetPid { get; set; }
    public static string ProcessName { get; set; } = "Revit";
    public static string? WindowTitle { get; set; }
    public static bool UseActiveWindow { get; set; }
    public static int ConnectTimeoutSec { get; set; } = 30;
    public static WindowSession? CurrentSession { get; set; }
    public static WindowSession? CurrentWindowSession { get; set; }
    public static DesktopWindowManager? WindowManager { get; set; }
    public static string? LastOutput { get; set; }
    public static AutomationEventService? EventService { get; set; }
    public static bool IsUiaOnly { get; set; }
    public static WinAppDriverClient? WadClient { get; set; }
    public static UIA3Automation? Automation { get; set; }
    public static RevitInstanceManager InstanceManager { get; } = new();
    public static ProgramOptions GlobalOptions => ProgramOptions.FromGlobalFlags();

    static Program()
    {
        Register(new ListWindowsCommand());
        Register(new ListControlsCommand());
        Register(new FindCommand());
        Register(new InfoCommand());
        Register(new DumpCommand());
        Register(new InspectCommand());
        Register(new ClickCommand());
        Register(new RibbonCommand());
        Register(new SwitchViewCommand());
        Register(new TypeTextCommand());
        Register(new ExpandCommand());
        Register(new RibbonTabsCommand());
        Register(new RibbonButtonsCommand());
        Register(new WaitCommand());
        Register(new ScriptCommand(Commands));
        Register(new StateCommand());
        Register(new WaitForCommand());
        Register(new WaitCloseCommand());
        Register(new WaitForElementCommand());
        Register(new SafeClickCommand());
        Register(new PropertySheetCommand());
        Register(new PropertySheetBatchCommand());
        Register(new TaskDialogCommand());
        Register(new AssertDialogCommand());
        Register(new AssertRibbonCommand());
        Register(new AssertViewCommand());
        Register(new RibbonFindCommand());
        Register(new DropDownCommand());
        Register(new ContextTabsCommand());
        Register(new QatCommand());
        Register(new RibbonPanelCommand());
        Register(new MouseClickCommand());
        Register(new MouseDragCommand());
        Register(new MouseScrollCommand());
        Register(new MousePosCommand());
        Register(new MouseTypeCommand());
        Register(new LogsCommand());
        Register(new DryRunCommand());
        Register(new SafetyCheckCommand());
        Register(new RevitRestartCommand());
        Register(new CacheFindCommand());
        Register(new CacheClearCommand());
        Register(new CacheStatsCommand());
        Register(new Win32ClickCommand());
        Register(new Win32EnumCommand());
        Register(new RecordStartCommand());
        Register(new RecordStopCommand());
        Register(new RecordStatusCommand());
        Register(new RecordSaveCommand());
        Register(new ScriptListCommand());
        Register(new ScriptLogCommand());
        Register(new ScriptDiffCommand());
        Register(new HighlightCommand());
        Register(new HighlightClearCommand());
        Register(new RevitApiCommand());
        Register(new RevitApiSelectCommand());
        Register(new RevitApiGetCommand());
        Register(new StatusBarCommand());
        Register(new WaitProgressCommand());
        Register(new WadConnectCommand());
        Register(new WadFindCommand());
        Register(new WadClickCommand());
        Register(new CanvasClickCommand());
        Register(new CanvasDragCommand());
        Register(new CanvasZoomCommand());
        Register(new CanvasScreenshotCommand());
        Register(new AllureSetupCommand());
        Register(new AllureReportCommand());
        Register(new RetryClickCommand());
        Register(new RetryDialogCommand());
        Register(new AiFindCommand());
        Register(new RecordVideoStartCommand());
        Register(new RecordVideoStopCommand());

        Register(new CvMatchCommand());
        Register(new CvClickCommand());
        Register(new CvListTemplatesCommand());

        Register(new UiMapLoadCommand());
        Register(new UiMapSaveCommand());
        Register(new UiMapResolveCommand());
        Register(new UiMapRegisterCommand());
        Register(new UiMapListCommand());
        Register(new UiMapAutoCommand());
        Register(new ProcessListCommand());
        Register(new ProcessInfoCommand());
        Register(new SessionBeginCommand());
        Register(new SessionEndCommand());
        Register(new SessionStatusCommand());

        Register(new ListAllWindowsCommand());
        Register(new FocusCommand());
        Register(new ActiveCommand());
        Register(new MonitorsCommand());
        Register(new PatternsCommand());
        Register(new TreeExpandCommand());
        Register(new ComboReadCommand());
        Register(new GridReadCommand());
        Register(new ListItemsCommand());
        Register(new TableReadCommand());
        Register(new ScrollToCommand());
        Register(new DumpPatternsCommand());
        Register(new InvokeCommand());
        Register(new ToggleCommand());
        Register(new SetValueCommand());
        Register(new FindAllCommand());
        Register(new WatchCommand());
        Register(new KeyComboCommand());
        Register(new ClipboardGetCommand());
        Register(new ClipboardSetCommand());
        Register(new ScreenshotRegionCommand());
        Register(new HighlightRegionCommand());

        Register(new ListenStartCommand());
        Register(new ListenStopCommand());
        Register(new EventLogCommand());

        Register(new RevitInstancesCommand());
        Register(new RevitLaunchCommand());
        Register(new MultiExecCommand());
        Register(new SessionSwitchCommand());

        if (UiMap.TryLoadDefault() && Verbosity == "full")
            LoggingService.Info("Program", $"[uimap] loaded {UiMap.EntryCount} entries from {UiMap.CurrentPath}");
    }

    private static void Register(ICommand cmd)
    {
        Commands[cmd.Name] = cmd;
    }

    public static ICommand? GetCommand(string name)
    {
        var lower = name.ToLowerInvariant();
        if (Aliases.TryGetValue(lower, out var resolved))
            lower = resolved;
        return Commands.TryGetValue(lower, out var cmd) ? cmd : null;
    }

    public static async Task<int> Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cts.Cancel();
        };

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

        if (Aliases.TryGetValue(cmdName, out var resolved))
            cmdName = resolved;

        if (Commands.TryGetValue(cmdName, out var cmd))
        {
            WindowManager = new DesktopWindowManager();

            WindowSession? session = null;

            if (UseActiveWindow)
            {
                session = await WindowSession.ConnectToActive(ct: Cts.Token);
            }
            else if (TargetPid.HasValue || ProcessName != "Revit")
            {
                session = await WindowSession.ConnectToProcess(TargetPid, ProcessName, ConnectTimeoutSec, Cts.Token);
            }
            else if (!string.IsNullOrEmpty(WindowTitle))
            {
                session = await WindowSession.ConnectByTitle(WindowTitle, ConnectTimeoutSec, Cts.Token);
            }
            else
            {
                session = await WindowSession.ConnectToProcess(TargetPid, ProcessName, ConnectTimeoutSec, Cts.Token);
            }

            if (session == null)
                return 1;

            CurrentSession = session;
            CurrentWindowSession = session;
            Automation = session.Automation;

            if (!UiMap.IsLoaded)
            {
                UiMap.TryLoadDefault();
                if (UiMap.IsLoaded && Verbosity != "minimal")
                    LoggingService.Info("Program", $"[uimap] auto-loaded {UiMap.EntryCount} entries from {UiMap.CurrentPath}");
            }

            SessionContext.ActiveHwnd = session.Process?.MainWindowHandle.ToInt64();
            SessionContext.ActivePid = session.Process?.Id;
            SessionContext.ActiveProcessName = session.Process?.ProcessName;

            using (session)
            using (WindowManager)
            {
                var beforeState = SessionContext.IsActive ? OutputFormatter.CaptureState(session.MainWindow) : null;

                var cmdArgs = cleanArgs.Skip(1).ToArray();
                if (SafetyGuard.IsDestructive(cmdName, cmdArgs)
                    && !SafetyGuard.ConfirmDestructiveAction($"{cmdName} {string.Join(" ", cmdArgs)}"))
                {
                    return 0;
                }

                var originalOut = Console.Out;
                using var outputWriter = new StringWriter();
                Console.SetOut(outputWriter);
                try
                {
                    var exitCode = await cmd.ExecuteAsync(session.MainWindow, cleanArgs.Skip(1).ToArray(), Cts.Token);

                    if (EventService != null) { EventService.Dispose(); EventService = null; }

                    try { LastOutput = outputWriter.ToString(); } catch (Exception ex) { LoggingService.Warn("Safe", $"LastOutput: {ex.Message}"); }

                    if (SessionContext.IsActive && beforeState != null)
                    {
                        var afterState = OutputFormatter.CaptureState(session.MainWindow);
                        var diff = OutputFormatter.ComputeDiff(beforeState, afterState);
                        foreach (var d in diff.NewDialogs)
                            SessionContext.PushDialog(d);
                        foreach (var _ in diff.ClosedDialogs)
                            SessionContext.PopDialog();
                    }
                    if (exitCode == 0 && RecorderService.IsRecording && cmd.Name != "record-start" && cmd.Name != "record-stop")
                    {
                        RecorderService.Record($"{cmdName} {string.Join(" ", cleanArgs.Skip(1))}");
                    }
                    if (exitCode != 0 && !IsScreenshot)
                    {
                        var b64 = ScreenshotHelper.CaptureWindow(session.MainWindow);
                        if (b64 != null)
                        {
                            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            var dir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
                            Directory.CreateDirectory(dir);
                            var filePath = Path.Combine(dir, $"error_{timestamp}.png");
                            var bytes = Convert.FromBase64String(b64);
                            File.WriteAllBytes(filePath, bytes);
                            LoggingService.Info("Program", $"[AUTO-SCREENSHOT] {filePath}");
                        }
                    }
                    return exitCode;
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        LoggingService.Error("Program", $"Unknown command: {cmdName}");
        PrintHelp();
        return 1;
    }

    private static string[] ParseGlobalFlags(string[] args)
    {
        var result = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--pretty")
                IsPretty = true;
            else if (args[i] == "--screenshot")
                IsScreenshot = true;
            else if (args[i] == "--verbosity" && i + 1 < args.Length)
                Verbosity = args[++i];
            else if (args[i] == "--pid" && i + 1 < args.Length && int.TryParse(args[++i], out var pid))
                TargetPid = pid;
            else if (args[i] == "--process-name" && i + 1 < args.Length)
                ProcessName = args[++i];
            else if (args[i] == "--window-title" && i + 1 < args.Length)
                WindowTitle = args[++i];
            else if (args[i] == "--active")
                UseActiveWindow = true;
            else if (args[i] == "--connect-timeout" && i + 1 < args.Length && int.TryParse(args[++i], out var timeout))
                ConnectTimeoutSec = timeout;
            else if (args[i] == "--non-interactive")
                SafetyGuard.IsNonInteractive = true;
            else if (args[i] == "--uia-only")
            {
                IsUiaOnly = true;
                WadClient = new WinAppDriverClient();
            }
            else
                result.Add(args[i]);
        }
        return result.ToArray();
    }

    private static void PrintCommandHelp(string commandName)
    {
        var lower = commandName.ToLowerInvariant();
        if (Aliases.TryGetValue(lower, out var resolved))
            lower = resolved;

        if (Commands.TryGetValue(lower, out var cmd))
        {
            Console.WriteLine($"Command: {cmd.Name}");
            Console.WriteLine($"Description: {cmd.Description}");
            Console.WriteLine($"Usage: {cmd.Usage}");
        }
        else
        {
            LoggingService.Error("Program", $"Unknown command: {commandName}. Use --help to list all commands.");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
RevitUiController — FlaUI-based UI automation tool (Revit + any desktop window)

Global flags:
  --pretty                         Pretty-print JSON output
  --screenshot                     Include base64 screenshot in output
  --verbosity minimal|normal|full  Control output detail (default: normal)
  --pid <number>                   Connect to specific process by PID
  --process-name <name>            Process name to find (default: Revit)
  --window-title <title>           Connect to window by title (contains match)
  --active                         Connect to the currently active/foreground window
  --connect-timeout <sec>          Wait timeout for process (default: 30)
  --non-interactive                Auto-reject destructive actions without prompt
  --uia-only                       UIA-only mode (no GDI/mouse_event for RDP/headless)
 
Desktop Window Management (any process):
  list-all (la) [--filter <txt>]        List ALL visible top-level windows on desktop
  focus <title> [--pid <N>|--hwnd <h>]  Bring a window to foreground
  active                                Show active/foreground window + monitor info
  monitors                              List all monitors (resolution, DPI, work area)

UIA Pattern Tools (read/interact with ANY UI control without screenshots):
  patterns <name>                       Show all UIA patterns for an element (Invoke, Toggle, Grid, Table, Scroll, etc.)
  dump-patterns [depth] [--type <ct>]   Dump UIA tree with supported patterns per element
  tree-expand <name> [--all] [--depth]  Expand TreeView node and dump subtree
  combo-read (cr) <name>                Open ComboBox, read all items, close
  grid-read (gr) <name> [--rows N]      Read DataGrid via GridPattern (rows×columns)
  list-items (li) <name> [--max N]      Read all ListBox/ListView items
  table-read (tr) <name> [--rows N]     Read Table with column headers
  scroll-to <name> [--parent <p>]       Scroll element into view (ScrollItemPattern)

Advanced Pattern Actions:
  invoke <name>                         Invoke control via InvokePattern (not Click)
  toggle <name> [on|off]                Toggle checkbox/switch via TogglePattern
  set-value <name> <text>               Set text via ValuePattern (more reliable than type)

Search & Watch:
  find-all (fa) <name> [--max N]        Find ALL matching controls, not just first
  watch <cmd> [args] --until <cond>     Poll a command until condition met (found/gone/enabled/disabled/text:...)
      [--interval <sec>] [--timeout <sec>]

Keyboard & Clipboard:
  key-combo (kc) <keys>                 Send keyboard shortcut (^c=Ctrl+C, %{F4}=Alt+F4, {TAB})
  clipboard-get (cg)                    Read text from clipboard
  clipboard-set (cs) <text>             Write text to clipboard

Region & Screenshot:
  screenshot-region (sr) <x> <y> <w> <h>  Capture screenshot of a screen region
  highlight-region (hr) <x> <y> <w> <h>   Highlight a screen region with overlay

Commands (Revit-focused, work with --process-name or --pid):
  list-windows (lw)                     List all Revit windows/dialogs
  list-controls (lc) [window-name]      List controls in a window
  click <button-name>                   Click a button/control by name
  ribbon <button-name> [tab-name]       Click a ribbon button (optionally on a tab)
  switch-view (sv) <view-name>          Switch to a view tab
  type <control-name> <text>            Type text into a control
  find <control-name>                   Find info about a control
  dump [depth] [-f <file>] [-t <type>]  Dump UIA tree (console or -f file, -t filter)
  inspect [index-path]                  Inspect element like Spy++ (e.g. inspect 37 0)
  state                                 Quick snapshot of Revit UI state
  wait-for <title> [timeout]            Wait for dialog to appear
  wait-close <title> [timeout]          Wait for dialog to close
  wait-element <name> [timeout]         Wait for element to appear
  safe-click <name>                     Idempotent click (OK if already gone)
  ps <title> [action]                     PropertySheet: fields, type, check, select, click
  ps-batch <title> <json> [--tab <t>]   Batch-fill multiple fields from JSON
  taskdialog <title> [action]           TaskDialog: read, click, expand
  assert-dialog <title> [check]         Assert dialog state (exists, text, button)
  assert-ribbon <tab> [button <name>]   Assert ribbon tab/button exists
  assert-view <name>                    Assert view tab exists
  ribbon-find <tab> [panel [btn]]       Find ribbon tab/panel/button location
  dropdown <btn> <item> [tab]           Open SplitButton, select dropdown item
  context-tabs                          List contextual ribbon tabs
  qat [click <name>]                    List/click Quick Access Toolbar buttons
  ribbon-panel <tab> [panel]            Show buttons in a specific ribbon panel
  mouse-click <x> <y>                   Click at screen coordinates
  mouse-drag <x1> <y1> <x2> <y2>       Drag from one point to another
  mouse-scroll <ticks>                  Scroll mouse wheel
  mouse-pos                             Get cursor position
  mouse-type <text>                     Type text via SendKeys
  logs [--tail N] [--level L]           Read controller/plugin logs
  dry-run <script>                      Simulate script without clicking
  safety-check                          Check/dismiss unexpected warning dialogs
  revit-restart [--path <exe>]          Start Revit if not running
  cached-find <name>                    Find with cache (fast repeated lookup)
  cache-clear                           Clear element cache
  cache-stats                           Show cache statistics
  win32-click <name>                    Click via Win32 SendMessage fallback
  win32-enum                            Enumerate Win32 child windows
  revit-api <cmd> [--payload <json>]    Execute Revit API command via pipe
  revit-select <id> [id ...]            Select elements by ID
  revit-get <query>                     Get Revit data (views/categories/elements)
  record-start <path>                   Start recording actions to .rvs
  record-stop                           Stop recording and save .rvs
  record-status                         Show recording status
  highlight <name> [ms]                 Highlight an element on screen
  highlight-clear                       Clear highlight overlay
  statusbar                             Read Revit status bar text
  wait-progress [timeout]               Wait for progress bar to complete
  wait <seconds>                        Wait N seconds
  expand                                Expand/collapse dialog details
  ribbon-tabs (rt) [tab-name]           List ribbon tabs & buttons, or switch to a tab
  rb [tab-name]                         List all ribbon buttons for a tab (deep scan)
  script <file>                         Run commands from a script file
  script-list (sl) [--path <dir>]       List available .rvs script files
  script-log (slog) [--file <p>]        Git log for .rvs scripts
  script-diff (sdiff) [--file <p>]      Git diff for .rvs scripts
  record-video [--fps 5] [--quality]    Start FFmpeg screen recording (gdigrab)
  record-video-stop                     Stop recording, save .mp4 to screenshots/
  record-save [--path <file.rvs>]       Save recording without stopping
  process-list                          List running Revit processes
  process-info                          Show connected Revit process details
  ai-find <query> [--type <ct>]         Multi-strategy intelligent element search
  llm-find <description> [--provider <p>]  Find UI element via LLM Vision (screenshot + AI)
  llm-click <description> [--provider <p>]  Find and click via LLM Vision
  info                                  Show Revit window info
  uimap-load [path]                     Load UI Map from YAML file
  uimap-save [path]                     Save current UI Map config to YAML
  uimap-resolve <name> [--version Y]    Resolve logical name to selectors
  uimap-register <name> --auto-id <id>  Register new UiMap entry
  uimap-list [filter]                   List registered UiMap entries
  uimap-auto <name> <element-name>      Auto-detect element and register entry
  cv-match <template.png> [--region x,y,w,h] [--threshold 0.8]  Find template image in Revit window via OpenCV
  cv-click <template.png> [--threshold 0.8] [--region x,y,w,h]  Find template and click on match
  cv-templates [filter]                  List available template images
  session-begin [--dialog <title>]      Start a stateful session (auto-tracks context)
  session-end                           End the current session
  session-status                        Show session context (dialog, tab, variables, stack)

Event Listener (event-driven, < 100ms response instead of polling):
  listen-start                          Start event-driven automation listener
  listen-stop                           Stop event-driven automation listener
  event-log [--last N]                  Show recent automation events

Script file: one command per line, # for comments, empty lines skipped
  Directives: wait-for, wait-close, select, window, set, get-output
  Session-aware: commands auto-scope to ActiveDialog when no dialog name given
  Variable expansion: $varName in args replaced from session variables
  Example:
    wait-for "Modify | Walls" 15
    select "Level" "Level 2"
""");
    }
}
