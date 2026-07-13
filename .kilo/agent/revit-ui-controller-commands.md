# RevitUiController: Command Reference

## Desktop Window Management
| Command | Description |
|---------|-------------|
| `list-all (la) [--filter <txt>]` | List ALL visible top-level windows |
| `active` | Show active/foreground window + monitor info |
| `focus <title> [--pid N\|--hwnd <h>]` | Bring window to foreground |
| `monitors` | List monitors (resolution, DPI, work area) |
| `process-list (ps) [--filter <name>]` | List all running processes |

## UI Exploration & Pattern Tools
| Command | Description |
|---------|-------------|
| `list-windows (lw)` | List child windows/dialogs of target process |
| `list-controls (lc) [window]` | List controls in a window (tree up to 5 levels) |
| `find <name>` | Find element by name, returns bounds, enabled, visible |
| `dump [depth] [-f file] [-t type]` | Full UIA tree dump (filter by ControlType) |
| `inspect [index-path]` | Spy++-style element inspection |
| `state` | Quick UI snapshot (active window, dialogs, tab) |
| `info` | Main window info |
| `find-all (fa) <name> [--max N]` | Find ALL matching controls |
| `patterns <name>` | Show all UIA patterns for element |
| `dump-patterns [depth] [--type ct]` | UIA tree dump with patterns |
| `combo-read (cr) <name>` | Read ComboBox items |
| `grid-read (gr) <name> [--rows N]` | Read DataGrid via GridPattern |
| `list-items (li) <name> [--max N]` | Read ListBox/ListView items |
| `table-read (tr) <name> [--rows N]` | Read Table with column headers |
| `window-screenshot (ws) [--output path.png]` | Screenshot of connected window |

## Navigation & Interaction
| Command | Description |
|---------|-------------|
| `click <name>` | Click button/control by name |
| `safe-click <name>` | Idempotent click (OK if already gone) |
| `ribbon <button> [tab]` | Click ribbon button (optionally switch tab) |
| `type <control> <text>` | Type text into control |
| `switch-view (sv) <view-name>` | Switch view tab |
| `expand` | Expand dialog details |
| `dropdown <btn> <item> [tab]` | SplitButton → select menu item |
| `invoke <name>` | Invoke via InvokePattern |
| `toggle <name> [on\|off]` | Toggle checkbox/switch via TogglePattern |
| `set-value <name> <text>` | Set text via ValuePattern |
| `scroll-to <name> [--parent p]` | ScrollIntoView |
| `tree-expand <name> [--all] [--depth]` | Expand TreeView node |
| `ribbon-tabs (rt) [tab-name]` | List ribbon tabs & buttons |
| `rb [tab-name]` | Deep scan: tabs → panels → buttons |
| `ribbon-find <tab> [panel [btn]]` | Find ribbon tab/panel/button location |
| `ribbon-panel <tab> [panel]` | Buttons on a specific ribbon panel |
| `context-tabs` | List contextual ribbon tabs |
| `qat [click <name>]` | Quick Access Toolbar |
| `menu-click (mc) <path>` | Click MenuItem by path (separated by `/ \ >`) |
| `menu-list (ml)` | List all menu items |
| `tab-select (tbs) <name>` | Select a tab control by name |
| `tree-select (trs) <path>` | Select TreeView item by path |
| `scroll (scr) [--horizontal N] [--vertical N] [--target <name>]` | Scroll a container |

## Dialog Handling (PropertySheet)
| Command | Description |
|---------|-------------|
| `ps <title> fields` | Read all dialog fields |
| `ps <title> tabs` | List dialog tabs |
| `ps <title> type <label> <value>` | Type value into field by label |
| `ps <title> check <label> [t/f]` | Set/read CheckBox |
| `ps <title> select <label> <option>` | Select ComboBox value |
| `ps <title> click <button>` | Click dialog button |
| `ps-batch <title> <json> [--tab t] [--timeout N]` | Batch-fill multiple fields from JSON |
| `taskdialog <title> read\|click\|expand` | TaskDialog operations |

## Waiting & Watch
| Command | Description |
|---------|-------------|
| `wait <seconds>` | Wait N seconds |
| `wait-for <title> [timeout]` | Wait for dialog to appear |
| `wait-close <title> [timeout]` | Wait for dialog to close |
| `wait-element <name> [timeout]` | Wait for element to appear |
| `wait-progress [timeout]` | Wait for progress bar to complete |
| `watch <cmd> --until <cond> [--interval] [--timeout]` | Poll command until condition met |

## Keyboard & Clipboard
| Command | Description |
|---------|-------------|
| `key-combo (kc) <keys>` | Send keyboard shortcut (`^c`=Ctrl+C, `%{F4}`=Alt+F4, `{TAB}`) |
| `clipboard-get (cg)` | Read clipboard text |
| `clipboard-set (cs) <text>` | Write text to clipboard |

## Mouse, Canvas & Region
| Command | Description |
|---------|-------------|
| `mouse-click <x> <y>` | Click at coordinates (DPI-aware) |
| `mouse-drag <x1> <y1> <x2> <y2>` | Drag from-to |
| `mouse-scroll <ticks>` | Scroll wheel |
| `mouse-pos` | Get cursor position |
| `mouse-type <text>` | SendKeys |
| `canvas-click/drag/zoom/screenshot` | GraphicsView operations |
| `screenshot-region (sr) <x> <y> <w> <h>` | Screenshot of screen region |
| `highlight-region (hr) <x> <y> <w> <h>` | Highlight region with overlay |

## Computer Vision (OpenCV)
| Command | Description |
|---------|-------------|
| `cv-match <template.png> [--region x,y,w,h] [--threshold 0.8]` | Find template via OpenCV |
| `cv-click <template.png> [--threshold 0.8]` | Find template and click |
| `cv-templates [filter]` | List available template images |

## LLM Vision (AI-powered)
| Command | Description |
|---------|-------------|
| `llm-find <description> [--region] [--provider] [--model]` | Find element by description |
| `llm-click <description> [--provider] [--model]` | Find and click by description |

## Revit API Bridge
| Command | Description |
|---------|-------------|
| `revit-api <cmd> [--payload <json>]` | [DEPRECATED] Use `pipe-api` instead |
| `pipe-api <cmd> [--payload <json>]` | Execute Revit API command via named pipe |
| `pipe-events [--last N]` | Show recent PipeEvents from Revit named pipe |
| `revit-select <id> [id ...]` | Select elements by ID |
| `revit-get views\|elements\|categories` | Get Revit data |

## Multi-instance Revit
| Command | Description |
|---------|-------------|
| `revit-instances (ri)` | [DEPRECATED] Use `app-instances --profile revit` |
| `revit-launch [--version Y] [--path <exe>]` | Launch Revit |
| `multi-exec --all <command>` | Execute command on all instances |
| `session-switch <pid>` | Switch active session |
| `app-instances --profile <name>` | List instances for any profile |

## Scripts & Recording
| Command | Description |
|---------|-------------|
| `script <file.rvs>` | Execute script file |
| `dry-run <file.rvs>` | Simulate script without clicks |
| `record-start <path>` | Start recording actions |
| `record-stop` | Stop and save recording |
| `record-save [--path <p>]` | Save recording without stopping |
| `record-export --xunit\|--gherkin\|--python <file>` | Export recording to code |
| `script-list (sl)` | List .rvs script files |
| `script-log (slog)` | Git log for scripts |
| `script-diff (sdiff)` | Git diff for scripts |
| `record-video [--fps 5]` | Start FFmpeg screen recording |
| `record-video-stop` | Stop recording, save .mp4 |

## UI Map (Page Object Model)
| Command | Description |
|---------|-------------|
| `uimap-load [path]` | Load UI Map from YAML |
| `uimap-save [path]` | Save UI Map to YAML |
| `uimap-resolve <name> [--version Y]` | Resolve logical name to selectors |
| `uimap-register <name> --auto-id <id>` | Register new entry |
| `uimap-list [filter]` | List entries |
| `uimap-auto <name> <element-name>` | Auto-detect element and register |

## Smart Search
| Command | Description |
|---------|-------------|
| `ai-find <query> [--type ct] [--deep] [--max N]` | Multi-strategy search (6 strategies) |

## Safety & Diagnostics
| Command | Description |
|---------|-------------|
| `safety-check` | Check/dismiss unexpected warning dialogs |
| `app-restart --profile <name>` | Start app if not running |
| `process-list` | List Revit processes |
| `process-info` | Connected process details |
| `logs [--tail N] [--level L]` | Read controller/plugin logs |
| `statusbar` | Read Revit status bar text |
| `highlight <name> [ms]` | Highlight element with overlay |
| `highlight-clear` | Clear highlight |
| `cache-clear / cache-stats / cached-find` | Element cache management |

## Assertions
| Command | Description |
|---------|-------------|
| `assert-dialog <title> exists\|text\|button\|enabled\|field` | Assert dialog state |
| `assert-ribbon <tab> [button <name>]` | Assert ribbon tab/button |
| `assert-view <name>` | Assert view tab active |

## Session Management
| Command | Description |
|---------|-------------|
| `session-begin [--dialog <title>]` | Start stateful session |
| `session-end` | End session |
| `session-status` | Show session context |

## Event Listener
| Command | Description |
|---------|-------------|
| `listen-start` | Start event-driven listener (<100ms) |
| `listen-stop` | Stop listener |
| `event-log [--last N]` | Show recent automation events |

## Daemon Management
| Command | Description |
|---------|-------------|
| `daemon [--start\|--stop\|--status] [--pipe <name>] [--profile <name>]` | Start/interact with persistent daemon |

## Batch Commands
| Command | Description |
|---------|-------------|
| `batch <json-array> [--pretty]` | Execute multiple commands from JSON array, return array of results |

## Allure Reporting
| Command | Description |
|---------|-------------|
| `allure-setup [--output <dir>]` | Initialize Allure reporting for test results |
| `allure-generate [--output <dir>] [--history <dir>]` | Generate Allure report from results |
| `allure-open [--port <port>] [--report <dir>]` | Open Allure report in browser |

## WebView2 Commands
| Command | Description |
|---------|-------------|
| `wv-connect [--port <port>] [--timeout <sec>]` | Connect to WebView2 via CDP |
| `wv-list` | List WebView2 pages/tabs |
| `wv-click <selector>` | Click element by CSS selector |
| `wv-type <selector> <text>` | Type text into element by CSS selector |
| `wv-text <selector>` | Get text content of element |
| `wv-eval <js-code>` | Execute JavaScript in WebView2 |
| `wv-screenshot [--selector <css>]` | Screenshot WebView2 content |
| `wv-url` | Get current URL of WebView2 |
| `wv-wait <selector> [--timeout <sec>]` | Wait for element to appear in WebView2 |

## Fallback Layers
| Command | Description |
|---------|-------------|
| `win32-click <name>` | Win32 SendMessage click |
| `win32-enum` | Enumerate Win32 child windows |
| `wad-connect` | Connect to WinAppDriver |
| `wad-find <method> <value>` | Find via WinAppDriver |
| `wad-click <element-id>` | Click via WinAppDriver |

## .rvs Script Format
```
# Comment
wait-for "Modify | Walls" 15
ribbon Wall Architecture
ps type "Height" 3000
select "Level" "Level 2"
check "Structural Wall" false
set wallResult "created"
ps click OK
wait-close "Modify | Walls" 10
```

Directives: `wait-for`, `wait-close`, `window`, `select`, `set`, `get-output`, `if-dialog`, `on-error`, `wait-any`, `while-dialog`
