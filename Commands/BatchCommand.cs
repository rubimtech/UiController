using System.Text.Json;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class BatchCommand : ICommand
{
    public string Name => "batch";
    public string Description => "Execute multiple commands from JSON array and return array of results";
    public string Usage => "batch <json-array> [--pretty]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError(UiController.Core.Models.ErrorCode.InvalidArgs, "batch <json-array> [--pretty]"));
            return 1;
        }

        var json = string.Join(" ", args.Where(a => a != "--pretty"));
        var pretty = args.Contains("--pretty");

        List<BatchCommandItem>? commands;
        try
        {
            commands = JsonSerializer.Deserialize<List<BatchCommandItem>>(json);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError(UiController.Core.Models.ErrorCode.InvalidArgs, $"Invalid JSON: {ex.Message}"));
            return 1;
        }

        if (commands == null || commands.Count == 0)
        {
            Console.Write(OutputFormatter.FormatError(UiController.Core.Models.ErrorCode.InvalidArgs, "Empty command list"));
            return 1;
        }

        var results = new List<object>();
        var overallSuccess = true;
        var succeededCount = 0;
        var startTime = DateTime.UtcNow;

        foreach (var cmd in commands)
        {
            if (string.IsNullOrEmpty(cmd.Command))
            {
                results.Add(new { command = "", success = false, error = "Missing command name" });
                overallSuccess = false;
                continue;
            }

            var cmdName = cmd.Command.ToLowerInvariant();
            var cmdArgs = cmd.Args?.ToArray() ?? Array.Empty<string>();

            var command = Program.GetCommand(cmdName);
            if (command == null)
            {
                results.Add(new { command = cmdName, success = false, error = $"Unknown command: {cmdName}" });
                overallSuccess = false;
                continue;
            }

            var originalOut = Console.Out;
            using var sw = new StringWriter();
            Console.SetOut(sw);

            try
            {
                var beforeState = SessionContext.IsActive ? OutputFormatter.CaptureState(revitWindow) : null;
                var exitCode = await command.ExecuteAsync(revitWindow, cmdArgs, ct);
                var output = sw.ToString();

                if (beforeState != null && SessionContext.IsActive)
                {
                    var afterState = OutputFormatter.CaptureState(revitWindow);
                    var diff = OutputFormatter.ComputeDiff(beforeState, afterState);
                    foreach (var d in diff.NewDialogs) SessionContext.PushDialog(d);
                    foreach (var _ in diff.ClosedDialogs) SessionContext.PopDialog();
                }

                results.Add(new
                {
                    command = cmdName,
                    success = exitCode == 0,
                    output,
                    exitCode
                });

                if (exitCode == 0) succeededCount++;
                else overallSuccess = false;
            }
            catch (Exception ex)
            {
                results.Add(new { command = cmdName, success = false, error = ex.Message });
                overallSuccess = false;
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "batch",
            Success = overallSuccess,
            Data = new { commands = results, total = results.Count, succeeded = succeededCount, elapsed },
            DurationMs = elapsed
        }, Program.GlobalOptions));

        return overallSuccess ? 0 : 1;
    }
}

public class BatchCommandItem
{
    public string Command { get; set; } = "";
    public List<string>? Args { get; set; }
}
