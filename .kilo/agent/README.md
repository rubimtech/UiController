# RevitUiController Agent Catalog

## Agent Files

| File | Purpose |
|------|---------|
| [Architecture & Patterns](./agent/revit-ui-controller-architecture.md) | Multi-project structure (6 projects: 1 legacy + 5 new), DI, profiles, providers, Daemon/MCP protocol, 28 DaemonRequest fields, 33 MCP tools |
| [Build, Run & Debug](./agent/revit-ui-controller-build-run.md) | Build commands, run flags (Host/Daemon/McpServer + legacy root), config.yaml, debug tips, known issues |
| [Command Reference](./agent/revit-ui-controller-commands.md) | All 150+ commands: 70+ new DI-based, 75 legacy, WebView2, Daemon, MCP, batch, .rvs format |
| [Developer Guide](./agent/revit-ui-controller-dev.md) | Quickstart for development: architecture, build commands, patterns, rules, known problems |
| [Extending the Codebase](./agent/revit-ui-controller-extending.md) | Creating commands, services, plugins (IPlugin), profiles (IApplicationProfile), providers, MCP tools |

## Load for Context
Use these commands to load the relevant agent skill:

```
.kilo/agent/revit-ui-controller-architecture.md    — architecture deep-dive
.kilo/agent/revit-ui-controller-build-run.md       — build & run reference
.kilo/agent/revit-ui-controller-commands.md         — command reference
.kilo/agent/revit-ui-controller-dev.md              — development guide
.kilo/agent/revit-ui-controller-extending.md        — extension guide
```
