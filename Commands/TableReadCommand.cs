using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class TableReadCommand : ICommand
{
    public string Name => "table-read";
    public string Description => "Read a Table/DataGrid with all rows and columns via GridPattern. Usage: table-read <name> [--rows N]";
    public string Usage => "table-read <name> [--rows N]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: table-read <name> [--rows N]");
            return Task.FromResult(1);
        }

        var maxRows = 100;
        var name = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--rows" && i + 1 < args.Length) int.TryParse(args[++i], out maxRows);
            else name = (name == "" ? args[i] : name + " " + args[i]);
        }

        var element = AutomationHelper.FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var headers = new List<object>();
        var rows = new List<object>();
        int totalRows = 0, totalCols = 0;

        try
        {
            var grid = element.Patterns.Grid.Pattern;
            if (grid != null)
            {
                totalRows = grid.RowCount;
                totalCols = grid.ColumnCount;
            }

            var headerItems = element.FindAllChildren(cf => cf.ByControlType(ControlType.Header));
            foreach (var hi in headerItems)
            {
                var headerItemChildren = hi.FindAllChildren(cf => cf.ByControlType(ControlType.HeaderItem));
                for (int i = 0; i < headerItemChildren.Length; i++)
                {
                    try
                    {
                        headers.Add(new
                        {
                            column = i,
                            name = headerItemChildren[i].Name ?? ""
                        });
                    }
                    catch { }
                }
            }

            var maxR = Math.Min(totalRows, maxRows);
            for (int r = 0; r < maxR && grid != null; r++)
            {
                var cells = new List<object>();
                for (int c = 0; c < totalCols; c++)
                {
                    try
                    {
                        var cell = grid.GetItem(r, c);
                        cells.Add(new
                        {
                            column = c,
                            value = cell?.Name ?? ""
                        });
                    }
                    catch
                    {
                        cells.Add(new { column = c, value = "[error]" });
                    }
                }
                rows.Add(new { rowIndex = r, cells });
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"TableRead warning: {ex.Message}");
        }

        var result = new CommandResult
        {
            Command = "table-read",
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
                headerCount = headers.Count,
                headers,
                rowCount = rows.Count,
                truncated = totalRows > maxRows,
                rows
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }

    private static string SafeGet(Func<object?> f) { try { return f()?.ToString() ?? ""; } catch { return ""; } }
}
