using System.Text.Json;
using System.Text.Json.Serialization;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace UiController.Core;

public static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string? LastOutput { get; set; }

    public static string FormatResult(CommandResult result, bool pretty = false)
    {
        var json = pretty
            ? JsonSerializer.Serialize(result, PrettyJsonOptions)
            : JsonSerializer.Serialize(result, JsonOptions) + Environment.NewLine;
        try { LastOutput = json; } catch { }
        return json;
    }

    public static string FormatResult(CommandResult result, ProgramOptions? options)
    {
        return FormatResult(result, options?.IsPretty ?? CoreSettings.IsPretty);
    }

    public static string FormatError(string code, string query, List<string>? suggestions = null, ProgramOptions? options = null)
    {
        var pretty = options?.IsPretty ?? CoreSettings.IsPretty;
        var result = new CommandResult
        {
            Success = false,
            ErrorInfo = new SelfDescribingError
            {
                Code = code,
                Query = query,
                Suggestions = suggestions ?? new List<string>()
            },
            Error = $"{code}: element '{query}' not found"
        };
        return FormatResult(result, options);
    }

    public static ElementInfo FromAutomationElement(AutomationElement el, int depth = 2, int maxDepth = 2)
    {
        var info = new ElementInfo
        {
            ControlType = el.ControlType.ToString(),
            Name = el.Name ?? "",
            AutomationId = el.AutomationId ?? "",
            Enabled = el.IsEnabled,
            Visible = el.IsOffscreen == false,
        };
        try { var r = el.BoundingRectangle; info.BoundingRect = new RectInfo(r.X, r.Y, r.Width, r.Height); } catch { }
        if (depth < maxDepth)
        {
            try
            {
                var children = new List<ElementInfo>();
                int idx = 0;
                foreach (var c in AutomationHelper.SafeGetChildren(el, 3000))
                {
                    var ci = FromAutomationElement(c, depth + 1, maxDepth);
                    ci.Index = idx++;
                    children.Add(ci);
                }
                if (children.Count > 0)
                    info.Children = children;
            }
            catch { }
        }
        return info;
    }

    public static List<ElementInfo> FromElementList(IEnumerable<AutomationElement> elements)
    {
        return elements.Select((e, i) =>
        {
            var info = FromAutomationElement(e, depth: 0, maxDepth: 0);
            info.Index = i;
            return info;
        }).ToList();
    }

    public static UiState CaptureState(AutomationElement window)
    {
        var state = new UiState
        {
            ActiveWindow = window.Name ?? "",
            OpenDialogs = AutomationHelper.FindActiveDialogs(window)
                .Select(d => d.Name ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList()
        };
        return state;
    }

    public static UiStateDiff ComputeDiff(UiState before, UiState after)
    {
        return new UiStateDiff
        {
            ActiveDialog = after.OpenDialogs.Count > 0 ? after.OpenDialogs[0] : null,
            NewDialogs = after.OpenDialogs.Except(before.OpenDialogs).ToList(),
            ClosedDialogs = before.OpenDialogs.Except(after.OpenDialogs).ToList(),
            ActiveTabChanged = before.ActiveRibbonTab != after.ActiveRibbonTab
        };
    }
}
