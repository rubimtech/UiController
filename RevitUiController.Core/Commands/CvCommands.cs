using System.Text.Json;
using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class CvMatchCommand : ICommand
{
    public string Name => "cv-match";
    public string Description => "Find a template image in the Revit window using OpenCV";
    public string Usage => "cv-match <template.png> [--region x,y,w,h] [--threshold 0.8]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("CvCommands", "Usage: cv-match <template.png> [--region x,y,w,h] [--threshold 0.8]");
            return 1;
        }

        var templateName = args[0];
        var knownArgs = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);
        double threshold = 0.8;
        (int x, int y, int w, int h)? region = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--threshold", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                double.TryParse(args[++i], out threshold);
            }
            else if (string.Equals(args[i], "--region", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var parts = args[++i].Split(',');
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out var rx) &&
                    int.TryParse(parts[1], out var ry) &&
                    int.TryParse(parts[2], out var rw) &&
                    int.TryParse(parts[3], out var rh))
                {
                    region = (rx, ry, rw, rh);
                }
            }
        }

        var templatePath = CvMatchClient.FindTemplateFile(templateName);
        if (templatePath == null)
        {
            Console.Write(OutputFormatter.FormatError("TemplateNotFound", templateName,
                ["Check template name or place .png in ./templates/ or ./cv-templates/"], CoreSettings.GlobalOptions));
            return 1;
        }

        using var template = CvMatchClient.LoadTemplate(templatePath);
        if (template == null)
        {
            Console.Write(OutputFormatter.FormatError("TemplateLoadFailed", templatePath, null, CoreSettings.GlobalOptions));
            return 1;
        }

        Bitmap? screenshot;
        int offsetX, offsetY;

        if (region.HasValue)
        {
            screenshot = ScreenshotHelper.CaptureBitmap(region.Value.x, region.Value.y, region.Value.w, region.Value.h);
            offsetX = region.Value.x;
            offsetY = region.Value.y;
        }
        else
        {
            screenshot = ScreenshotHelper.CaptureBitmap(window);
            if (screenshot == null)
            {
                Console.Write(OutputFormatter.FormatError("ScreenshotFailed", "Revit window", null, CoreSettings.GlobalOptions));
                return 1;
            }
            try
            {
                var r = window.BoundingRectangle;
                offsetX = (int)r.X;
                offsetY = (int)r.Y;
            }
            catch
            {
                offsetX = 0;
                offsetY = 0;
            }
        }

        if (screenshot == null)
        {
            Console.Write(OutputFormatter.FormatError("ScreenshotFailed", "region", null, CoreSettings.GlobalOptions));
            return 1;
        }

        using (screenshot)
        {
            var match = CvMatchClient.MatchTemplate(screenshot, template, threshold);
            if (match == null)
            {
                var data = new
                {
                    template = templateName,
                    templatePath,
                    threshold,
                    region = region.HasValue ? $"{region.Value.x},{region.Value.y},{region.Value.w},{region.Value.h}" : null,
                    found = false,
                };

                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "cv-match",
                    Success = false,
                    Error = $"Template '{templateName}' not found (threshold={threshold})",
                    Data = data,
                }, CoreSettings.GlobalOptions));
                return 1;
            }

            var absX = match.X + offsetX;
            var absY = match.Y + offsetY;

            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "cv-match",
                Success = true,
                Data = new
                {
                    template = templateName,
                    templatePath,
                    threshold,
                    confidence = match.Confidence,
                    matchCenter = new { x = match.X, y = match.Y },
                    matchSize = new { w = match.TemplateWidth, h = match.TemplateHeight },
                    absoluteScreen = new { x = absX, y = absY },
                    region = region.HasValue ? $"{region.Value.x},{region.Value.y},{region.Value.w},{region.Value.h}" : null,
                },
            }, CoreSettings.GlobalOptions));
            return 0;
        }
    }
}

public class CvClickCommand : ICommand
{
    public string Name => "cv-click";
    public string Description => "Find template image and click on the match";
    public string Usage => "cv-click <template.png> [--region x,y,w,h] [--threshold 0.8]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("CvCommands", "Usage: cv-click <template.png> [--region x,y,w,h] [--threshold 0.8]");
            return 1;
        }

        var templateName = args[0];
        double threshold = 0.8;
        (int x, int y, int w, int h)? region = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--threshold", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                double.TryParse(args[++i], out threshold);
            }
            else if (string.Equals(args[i], "--region", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var parts = args[++i].Split(',');
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out var rx) &&
                    int.TryParse(parts[1], out var ry) &&
                    int.TryParse(parts[2], out var rw) &&
                    int.TryParse(parts[3], out var rh))
                {
                    region = (rx, ry, rw, rh);
                }
            }
        }

        var templatePath = CvMatchClient.FindTemplateFile(templateName);
        if (templatePath == null)
        {
            Console.Write(OutputFormatter.FormatError("TemplateNotFound", templateName,
                ["Check template name or place .png in ./templates/ or ./cv-templates/"], CoreSettings.GlobalOptions));
            return 1;
        }

        using var template = CvMatchClient.LoadTemplate(templatePath);
        if (template == null)
        {
            Console.Write(OutputFormatter.FormatError("TemplateLoadFailed", templatePath, null, CoreSettings.GlobalOptions));
            return 1;
        }

        Bitmap? screenshot;
        int offsetX, offsetY;

        if (region.HasValue)
        {
            screenshot = ScreenshotHelper.CaptureBitmap(region.Value.x, region.Value.y, region.Value.w, region.Value.h);
            offsetX = region.Value.x;
            offsetY = region.Value.y;
        }
        else
        {
            screenshot = ScreenshotHelper.CaptureBitmap(window);
            try
            {
                var r = window.BoundingRectangle;
                offsetX = (int)r.X;
                offsetY = (int)r.Y;
            }
            catch
            {
                offsetX = 0;
                offsetY = 0;
            }
        }

        if (screenshot == null)
        {
            Console.Write(OutputFormatter.FormatError("ScreenshotFailed", "Revit window", null, CoreSettings.GlobalOptions));
            return 1;
        }

        using (screenshot)
        {
            var match = CvMatchClient.MatchTemplate(screenshot, template, threshold);
            if (match == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "cv-click",
                    Success = false,
                    Error = $"Template '{templateName}' not found (threshold={threshold})",
                    Data = new
                    {
                        template = templateName,
                        threshold,
                        found = false,
                    },
                }, CoreSettings.GlobalOptions));
                return 1;
            }

            var absX = match.X + offsetX;
            var absY = match.Y + offsetY;

            var before = OutputFormatter.CaptureState(window);
            await MouseControl.ClickAt(absX, absY, ct);
            await Task.Delay(200, ct);
            var after = OutputFormatter.CaptureState(window);

            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "cv-click",
                Success = true,
                Data = new
                {
                    template = templateName,
                    threshold,
                    confidence = match.Confidence,
                    clickPosition = new { x = absX, y = absY },
                    matchCenter = new { x = match.X, y = match.Y },
                    matchSize = new { w = match.TemplateWidth, h = match.TemplateHeight },
                },
                Diff = OutputFormatter.ComputeDiff(before, after),
            }, CoreSettings.GlobalOptions));
            return 0;
        }
    }
}

public class CvListTemplatesCommand : ICommand
{
    public string Name => "cv-templates";
    public string Description => "List available template images with metadata";
    public string Usage => "cv-templates [filter]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var filter = args.Length > 0 ? args[0] : null;
        var templates = CvMatchClient.FindTemplates(filter);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "cv-templates",
            Success = true,
            Data = new
            {
                count = templates.Count,
                templates = templates.Select(t =>
                {
                    var meta = CvMatchClient.LoadTemplateMetadata(t.Path);
                    return new
                    {
                        name = t.Name,
                        path = t.Path,
                        sizeKb = t.SizeKb,
                        metadata = meta != null ? new
                        {
                            width = meta.Width,
                            height = meta.Height,
                            dpiX = meta.DpiX,
                            dpiY = meta.DpiY,
                            revitVersion = meta.RevitVersion,
                            timestamp = meta.Timestamp,
                            region = meta.Region,
                            elementName = meta.ElementName,
                        } : null,
                    };
                }),
            },
        }, CoreSettings.GlobalOptions));
        return 0;
    }
}
