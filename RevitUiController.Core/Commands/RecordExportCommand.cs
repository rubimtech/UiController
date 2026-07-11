using System.IO;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace UiController.Core.Commands;

public class RecordExportCommand : ICommand
{
    public string Name => "record-export";
    public string Description => "Export .rvs recording to test code: --xunit <file> --gherkin <file> --python <file>";
    public string Usage => "record-export --xunit <file.rvs> | --gherkin <file.rvs> | --python <file.rvs>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-export",
                Success = false,
                Error = "Usage: record-export --xunit|--gherkin|--python <file.rvs>"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var format = args[0].ToLowerInvariant();
        var filePath = args[1];

        if (!File.Exists(filePath))
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-export",
                Success = false,
                Error = $"File not found: {filePath}"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var (converted, error) = format switch
        {
            "--xunit" => ExportToXunit(filePath),
            "--gherkin" => ExportToGherkin(filePath),
            "--python" => ExportToPython(filePath),
            _ => (null, $"Unknown format: {format}. Use --xunit, --gherkin, or --python.")
        };

        if (converted == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-export",
                Success = false,
                Error = error
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var outputPath = Path.ChangeExtension(filePath, format switch { "--xunit" => ".xunit.cs", "--gherkin" => ".feature", "--python" => ".py", _ => ".txt" });
        File.WriteAllText(outputPath, converted, Encoding.UTF8);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "record-export",
            Success = true,
            Data = new { format = format.TrimStart('-'), input = filePath, output = outputPath, lines = converted.Split('\n').Length }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }

    private static (string? content, string? error) ExportToXunit(string filePath)
    {
        var lines = ParseScript(filePath);
        var sb = new StringBuilder();
        var testName = Path.GetFileNameWithoutExtension(filePath);
        testName = SanitizeIdentifier(testName);

        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine($"public class {testName}Tests");
        sb.AppendLine("{");
        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public void {testName}_Test()");
        sb.AppendLine("    {");
        sb.AppendLine("        var controller = new RevitUiController();");
        sb.AppendLine();

        foreach (var (cmd, cmdArgs) in lines)
        {
            var escaped = string.Join(", ", cmdArgs.Select(a => EscapeStringLiteral(a)));
            var method = cmd switch
            {
                "ribbon" or "rb" => "Ribbon",
                "wait" => "Wait",
                "wait-for" => "WaitForDialog",
                "wait-close" => "WaitForDialogClose",
                "click" => "Click",
                "safe-click" => "SafeClick",
                "type" => "TypeText",
                "select" => "Select",
                "set" => "SetVariable",
                "ps" => "PropertySheet",
                "taskdialog" => "TaskDialog",
                "switch-view" or "sv" => "SwitchView",
                "ribbon-find" => "RibbonFind",
                "dropdown" => "DropDown",
                "mouse-click" => "MouseClick",
                "mouse-drag" => "MouseDrag",
                "mouse-scroll" => "MouseScroll",
                _ => cmd
            };

            if (method != cmd && cmdArgs.Length > 0)
            {
                sb.AppendLine($"        controller.{method}({escaped});");
            }
            else
            {
                sb.AppendLine($"        controller.Execute(\"{cmd}\", new[] {{ {escaped} }});");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return (sb.ToString(), null);
    }

    private static (string? content, string? error) ExportToGherkin(string filePath)
    {
        var lines = ParseScript(filePath);
        var sb = new StringBuilder();
        var scenarioName = Path.GetFileNameWithoutExtension(filePath);
        scenarioName = SanitizeIdentifier(scenarioName).Replace("_", " ");

        sb.AppendLine("Feature: Automated Revit UI Test");
        sb.AppendLine();
        sb.AppendLine($"  Scenario: {scenarioName}");

        foreach (var (cmd, cmdArgs) in lines)
        {
            var step = cmd switch
            {
                "ribbon" => $"When I click ribbon \"{cmdArgs[0]}\" on tab \"{(cmdArgs.Length > 1 ? cmdArgs[1] : "")}\"",
                "rb" => $"When I click ribbon \"{cmdArgs[0]}\" on tab \"{(cmdArgs.Length > 1 ? cmdArgs[1] : "")}\"",
                "wait" => $"Then I wait {cmdArgs[0]} seconds",
                "wait-for" => $"Then dialog \"{cmdArgs[0]}\" appears within {(cmdArgs.Length > 1 ? cmdArgs[1] : "15")} seconds",
                "wait-close" => $"Then dialog \"{cmdArgs[0]}\" closes within {(cmdArgs.Length > 1 ? cmdArgs[1] : "15")} seconds",
                "click" => $"When I click \"{cmdArgs[0]}\"",
                "safe-click" => $"When I click \"{cmdArgs[0]}\" (safe)",
                "type" => $"When I type \"{string.Join(" ", cmdArgs.Skip(1))}\" into \"{cmdArgs[0]}\"",
                "select" => $"When I select \"{string.Join(" ", cmdArgs.Skip(1))}\" from \"{cmdArgs[0]}\"",
                "switch-view" or "sv" => $"When I switch to view \"{cmdArgs[0]}\"",
                "set" => $"Given variable \"{cmdArgs[0]}\" = \"{string.Join(" ", cmdArgs.Skip(1))}\"",
                "get-output" => $"Then I capture output to \"{cmdArgs[0]}\"",
                "window" => $"Given I scope to dialog \"{string.Join(" ", cmdArgs)}\"",
                "ps" => $"When I edit property sheet \"{cmdArgs[0]}\"",
                "taskdialog" => $"When I interact with task dialog \"{cmdArgs[0]}\"",
                "dropdown" => $"When I select \"{string.Join(" ", cmdArgs.Skip(1))}\" from dropdown \"{cmdArgs[0]}\"{(cmdArgs.Length > 2 ? $" on tab \"{cmdArgs[2]}\"" : "")}",
                "assert-dialog" => $"Then dialog assertion for \"{cmdArgs[0]}\"",
                "assert-ribbon" => $"Then ribbon assertion for \"{cmdArgs[0]}\"",
                "mouse-click" => $"When I click at ({string.Join(", ", cmdArgs)})",
                "mouse-drag" => $"When I drag from ({cmdArgs[0]}, {cmdArgs[1]}) to ({cmdArgs[2]}, {cmdArgs[3]})",
                _ => $"When I run \"{cmd}\" with arguments \"{string.Join(" ", cmdArgs)}\""
            };
            sb.AppendLine($"    {step}");
        }

        return (sb.ToString(), null);
    }

    private static (string? content, string? error) ExportToPython(string filePath)
    {
        var lines = ParseScript(filePath);
        var sb = new StringBuilder();
        var testName = SanitizeIdentifier(Path.GetFileNameWithoutExtension(filePath));

        sb.AppendLine("from revit_controller import Controller");
        sb.AppendLine();
        sb.AppendLine($"def test_{testName}():");
        sb.AppendLine("    ctrl = Controller()");
        sb.AppendLine();

        foreach (var (cmd, cmdArgs) in lines)
        {
            var escaped = string.Join(", ", cmdArgs.Select(a => EscapeStringLiteral(a)));
            var method = cmd switch
            {
                "ribbon" or "rb" => "ribbon",
                "wait" => "wait",
                "wait-for" => "wait_for_dialog",
                "wait-close" => "wait_for_dialog_close",
                "click" => "click",
                "safe-click" => "safe_click",
                "type" => "type_text",
                "select" => "select",
                "set" => "set_variable",
                "get-output" => "get_output",
                "switch-view" or "sv" => "switch_view",
                "ribbon-find" => "ribbon_find",
                "dropdown" => "dropdown",
                "ps" => "property_sheet",
                "taskdialog" => "task_dialog",
                "mouse-click" => "mouse_click",
                "mouse-drag" => "mouse_drag",
                "mouse-scroll" => "mouse_scroll",
                _ => null
            };

            if (method != null)
                sb.AppendLine($"    ctrl.{method}({escaped})");
            else
                sb.AppendLine($"    ctrl.execute(\"{cmd}\", [{escaped}])");
        }

        return (sb.ToString(), null);
    }

    private static List<(string cmd, string[] args)> ParseScript(string filePath)
    {
        var result = new List<(string, string[])>();
        var lines = File.ReadAllLines(filePath);

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var tokens = Tokenize(trimmed);
            if (tokens.Length == 0) continue;

            var cmd = tokens[0].ToLowerInvariant();
            var cmdArgs = tokens.Skip(1).ToArray();
            result.Add((cmd, cmdArgs));
        }

        return result;
    }

    private static string[] Tokenize(string line)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            tokens.Add(sb.ToString());

        return tokens.ToArray();
    }

    private static string EscapeStringLiteral(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    private static string SanitizeIdentifier(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
        if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]) && sanitized[0] != '_')
            sanitized = "_" + sanitized;
        return sanitized;
    }
}
