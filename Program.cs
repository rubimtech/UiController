using RevitUiController.Commands;

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
    };

    public static bool IsPretty { get; set; }
    public static bool IsScreenshot { get; set; }
    public static string Verbosity { get; set; } = "normal";
    public static int? TargetPid { get; set; }
    public static string ProcessName { get; set; } = "Revit";
    public static int ConnectTimeoutSec { get; set; } = 30;
    public static RevitSession? CurrentSession { get; set; }

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

        if (UiMap.TryLoadDefault() && Verbosity == "full")
            Console.Error.WriteLine($"[uimap] loaded {UiMap.EntryCount} entries from {UiMap.CurrentPath}");
    }

    private static void Register(ICommand cmd)
    {
        Commands[cmd.Name] = cmd;
    }

    public static async Task<int> Main(string[] args)
    {
        var cleanArgs = ParseGlobalFlags(args);

        if (cleanArgs.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var cmdName = cleanArgs[0].ToLowerInvariant();

        if (Aliases.TryGetValue(cmdName, out var resolved))
            cmdName = resolved;

        if (Commands.TryGetValue(cmdName, out var cmd))
        {
            var session = RevitSession.Connect(TargetPid, ProcessName, ConnectTimeoutSec);
            if (session == null)
                return 1;

            CurrentSession = session;
            using (session)
            {
                var beforeState = SessionContext.IsActive ? OutputFormatter.CaptureState(session.MainWindow) : null;

                var exitCode = await cmd.ExecuteAsync(session.MainWindow, cleanArgs.Skip(1).ToArray());

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
                    var ss = ScreenshotHelper.CaptureWindow(session.MainWindow);
                    if (ss != null)
                        Console.Error.WriteLine($"[AUTO-SCREENSHOT] data:image/png;base64,{ss}");
                }
                return exitCode;
            }
        }

        Console.Error.WriteLine($"Unknown command: {cmdName}");
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
            else if (args[i] == "--connect-timeout" && i + 1 < args.Length && int.TryParse(args[++i], out var timeout))
                ConnectTimeoutSec = timeout;
            else
                result.Add(args[i]);
        }
        return result.ToArray();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
RevitUiController — FlaUI-based Revit automation tool

Global flags:
  --pretty                         Pretty-print JSON output
  --screenshot                     Include base64 screenshot in output
  --verbosity minimal|normal|full  Control output detail (default: normal)
  --pid <number>                   Connect to specific Revit process by PID
  --process-name <name>            Process name to find (default: Revit)
  --connect-timeout <sec>          Wait timeout for process (default: 30)

Commands:
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
  record-save [--path <file.rvs>]       Save recording without stopping
  process-list                          List running Revit processes
  process-info                          Show connected Revit process details
  ai-find <query> [--type <ct>]         Multi-strategy intelligent element search
  cv-match <template.png> [--region]    Find template image via OpenCV MatchTemplate
  cv-click <template.png>               Find template and click on match
  cv-templates [filter]                 List available template images
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
