using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class GridReadCommand : ICommand
{
    public string Name => "grid-read";
    public string Description => "Read a DataGrid via GridPattern with rows and columns. Usage: grid-read <name> [--rows N]";
    public string Usage => "grid-read <name> [--rows N] [--columns N]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: grid-read <name> [--rows N] [--columns N]");
            return Task.FromResult(1);
        }

        var rowsLimit = 50;
        var colsLimit = 50;
        var name = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--rows" && i + 1 < args.Length) int.TryParse(args[++i], out rowsLimit);
            else if (args[i] == "--columns" && i + 1 < args.Length) int.TryParse(args[++i], out colsLimit);
            else name = (name == "" ? args[i] : name + " " + args[i]);
        }

        var element = AutomationHelper.FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var rows = new List<object>();
        int totalRows = 0, totalCols = 0;

        try
        {
            var grid = element.Patterns.Grid.Pattern;
            if (grid != null)
            {
                totalRows = grid.RowCount;
                totalCols = grid.ColumnCount;

                var maxRows = Math.Min(totalRows, rowsLimit);
                var maxCols = Math.Min(totalCols, colsLimit);

                for (int r = 0; r < maxRows; r++)
                {
                    var rowCells = new List<object>();
                    for (int c = 0; c < maxCols; c++)
                    {
                        try
                        {
                            var cell = grid.GetItem(r, c);
                            rowCells.Add(new
                            {
                                column = c,
                                value = cell?.Name ?? "",
                                controlType = cell?.ControlType.ToString(),
                                automationId = SafeGet(() => cell?.AutomationId)
                            });
                        }
                        catch
                        {
                            rowCells.Add(new { column = c, value = "[error]" });
                        }
                    }
                    rows.Add(new { rowIndex = r, cells = rowCells });
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GridRead warning: {ex.Message}");
        }

        var result = new CommandResult
        {
            Command = "grid-read",
            Success = true,
            Data = new
            {
                element = new
                {
                    name = SafeGet(() => element.Name),
                    controlType = SafeGet(() => element.ControlType.ToString()),
                    automationId = SafeGet(() => element.AutomationId)
                },
                dimensions = new { rows = totalRows, columns = totalCols },
                shownRows = rows.Count,
                rows,
                truncated = totalRows > rowsLimit || totalCols > colsLimit
                    ? new { rowsTruncated = totalRows > rowsLimit, columnsTruncated = totalCols > colsLimit }
                    : null
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }

    private static string SafeGet(Func<object?> f) { try { return f()?.ToString() ?? ""; } catch { return ""; } }
}
