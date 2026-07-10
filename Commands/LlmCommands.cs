using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class LlmFindCommand : ICommand
{
    public string Name => "llm-find";
    public string Description => "Find UI element by description using LLM Vision on a screenshot";
    public string Usage => "llm-find <description> [--region x,y,w,h] [--provider <p>] [--model <m>] [--timeout <s>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var start = DateTime.UtcNow;

        if (args.Length == 0 || args[0].StartsWith("--"))
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", Usage, null, Program.IsPretty));
            return 1;
        }

        var description = args[0];
        int? regionX = null, regionY = null, regionW = null, regionH = null;
        string? provider = null;
        string? model = null;
        var timeoutSec = 30;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--region" when i + 1 < args.Length:
                    var parts = args[++i].Split(',');
                    if (parts.Length == 4 &&
                        int.TryParse(parts[0], out var rx) &&
                        int.TryParse(parts[1], out var ry) &&
                        int.TryParse(parts[2], out var rw) &&
                        int.TryParse(parts[3], out var rh))
                    {
                        regionX = rx; regionY = ry; regionW = rw; regionH = rh;
                    }
                    break;
                case "--provider" when i + 1 < args.Length:
                    provider = args[++i];
                    break;
                case "--model" when i + 1 < args.Length:
                    model = args[++i];
                    break;
                case "--timeout" when i + 1 < args.Length && int.TryParse(args[++i], out var t):
                    timeoutSec = Math.Max(5, t);
                    break;
            }
        }

        var rect = revitWindow.BoundingRectangle;

        int capX, capY, capW, capH;
        bool useRegion;

        if (regionX.HasValue && regionY.HasValue && regionW.HasValue && regionH.HasValue)
        {
            capX = regionX.Value; capY = regionY.Value;
            capW = regionW.Value; capH = regionH.Value;
            useRegion = true;
        }
        else
        {
            capX = (int)rect.X; capY = (int)rect.Y;
            capW = (int)rect.Width; capH = (int)rect.Height;
            useRegion = false;
        }

        var base64 = ScreenshotHelper.CaptureBase64(capX, capY, capW, capH);
        if (base64 == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "llm-find",
                Success = false,
                Error = "Failed to capture screenshot"
            }, Program.IsPretty));
            return 1;
        }

        var result = await LlmVisionClient.FindElementAsync(description, base64, provider, model, timeoutSec);

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

        if (result == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "llm-find",
                Success = false,
                Error = "LLM Vision request failed (check API key or provider availability)",
                Data = new
                {
                    description,
                    provider = LlmVisionClient.ResolveProvider(provider) ?? "none",
                    availableProviders = LlmVisionClient.GetAvailableProviders()
                        .Where(p => p.IsAvailable)
                        .Select(p => new { p.Name, p.DisplayName, p.DefaultModel })
                },
                Screenshot = Program.IsScreenshot ? base64 : null,
                DurationMs = elapsed
            }, Program.IsPretty));
            return 1;
        }

        if (!result.Found)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "llm-find",
                Success = false,
                Error = $"Element '{description}' not found in screenshot",
                Data = new
                {
                    description,
                    found = false,
                    provider = result.Provider,
                    model = result.Model,
                    confidence = result.Confidence,
                    region = useRegion
                        ? new { x = capX, y = capY, w = capW, h = capH }
                        : new { x = (int)rect.X, y = (int)rect.Y, w = (int)rect.Width, h = (int)rect.Height }
                },
                Screenshot = Program.IsScreenshot ? base64 : null,
                DurationMs = elapsed
            }, Program.IsPretty));
            return 1;
        }

        var elementX = capX + result.X;
        var elementY = capY + result.Y;

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "llm-find",
            Success = true,
            Data = new
            {
                description,
                found = true,
                elementX,
                elementY,
                elementName = result.Name,
                confidence = result.Confidence,
                provider = result.Provider,
                model = result.Model,
                region = useRegion
                    ? new { x = capX, y = capY, w = capW, h = capH }
                    : new { x = (int)rect.X, y = (int)rect.Y, w = (int)rect.Width, h = (int)rect.Height }
            },
            Screenshot = Program.IsScreenshot ? base64 : null,
            DurationMs = elapsed
        }, Program.IsPretty));
        return 0;
    }
}

public class LlmClickCommand : ICommand
{
    public string Name => "llm-click";
    public string Description => "Find element by description and click on it via LLM Vision";
    public string Usage => "llm-click <description> [--region x,y,w,h] [--provider <p>] [--model <m>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var start = DateTime.UtcNow;

        if (args.Length == 0 || args[0].StartsWith("--"))
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", Usage, null, Program.IsPretty));
            return 1;
        }

        var description = args[0];
        int? regionX = null, regionY = null, regionW = null, regionH = null;
        string? provider = null;
        string? model = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--region" when i + 1 < args.Length:
                    var parts = args[++i].Split(',');
                    if (parts.Length == 4 &&
                        int.TryParse(parts[0], out var rx) &&
                        int.TryParse(parts[1], out var ry) &&
                        int.TryParse(parts[2], out var rw) &&
                        int.TryParse(parts[3], out var rh))
                    {
                        regionX = rx; regionY = ry; regionW = rw; regionH = rh;
                    }
                    break;
                case "--provider" when i + 1 < args.Length:
                    provider = args[++i];
                    break;
                case "--model" when i + 1 < args.Length:
                    model = args[++i];
                    break;
            }
        }

        var rect = revitWindow.BoundingRectangle;

        int capX, capY, capW, capH;
        bool useRegion;

        if (regionX.HasValue && regionY.HasValue && regionW.HasValue && regionH.HasValue)
        {
            capX = regionX.Value; capY = regionY.Value;
            capW = regionW.Value; capH = regionH.Value;
            useRegion = true;
        }
        else
        {
            capX = (int)rect.X; capY = (int)rect.Y;
            capW = (int)rect.Width; capH = (int)rect.Height;
            useRegion = false;
        }

        var base64Before = ScreenshotHelper.CaptureBase64(capX, capY, capW, capH);
        if (base64Before == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "llm-click",
                Success = false,
                Error = "Failed to capture screenshot"
            }, Program.IsPretty));
            return 1;
        }

        var findResult = await LlmVisionClient.FindElementAsync(description, base64Before, provider, model);
        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

        if (findResult == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "llm-click",
                Success = false,
                Error = "LLM Vision request failed (check API key or provider availability)",
                Data = new
                {
                    description,
                    provider = LlmVisionClient.ResolveProvider(provider) ?? "none"
                },
                Screenshot = Program.IsScreenshot ? base64Before : null,
                DurationMs = elapsed
            }, Program.IsPretty));
            return 1;
        }

        if (!findResult.Found)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "llm-click",
                Success = false,
                Error = $"Element '{description}' not found in screenshot",
                Data = new
                {
                    description,
                    found = false,
                    provider = findResult.Provider,
                    model = findResult.Model
                },
                Screenshot = Program.IsScreenshot ? base64Before : null,
                DurationMs = elapsed
            }, Program.IsPretty));
            return 1;
        }

        var elementX = capX + findResult.X;
        var elementY = capY + findResult.Y;

        var beforeState = OutputFormatter.CaptureState(revitWindow);

        MouseControl.ClickAt(elementX, elementY);
        Thread.Sleep(300);

        var afterState = OutputFormatter.CaptureState(revitWindow);
        var diff = OutputFormatter.ComputeDiff(beforeState, afterState);

        var base64After = ScreenshotHelper.CaptureBase64(capX, capY, capW, capH);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "llm-click",
            Success = true,
            Data = new
            {
                description,
                elementX,
                elementY,
                elementName = findResult.Name,
                confidence = findResult.Confidence,
                provider = findResult.Provider,
                model = findResult.Model,
                region = useRegion
                    ? new { x = capX, y = capY, w = capW, h = capH }
                    : new { x = (int)rect.X, y = (int)rect.Y, w = (int)rect.Width, h = (int)rect.Height },
                clickResult = "clicked"
            },
            Diff = diff,
            Screenshot = Program.IsScreenshot ? base64After : null,
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.IsPretty));
        return 0;
    }
}
