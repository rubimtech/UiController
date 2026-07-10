using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class CvMatchCommand : ICommand
{
    public string Name => "cv-match";
    public string Description => "Find a template image in the Revit window using OpenCV";
    public string Usage => "cv-match <template.png> [--region x,y,w,h] [--threshold 0.8]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cv-match <template.png> [--region x,y,w,h] [--threshold 0.8]");
            return Task.FromResult(1);
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
                ["Check template name or place .png in ./templates/ or ./cv-templates/"], Program.IsPretty));
            return Task.FromResult(1);
        }

        using var template = CvMatchClient.LoadTemplate(templatePath);
        if (template == null)
        {
            Console.Write(OutputFormatter.FormatError("TemplateLoadFailed", templatePath, null, Program.IsPretty));
            return Task.FromResult(1);
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
            screenshot = ScreenshotHelper.CaptureBitmap(revitWindow);
            if (screenshot == null)
            {
                Console.Write(OutputFormatter.FormatError("ScreenshotFailed", "Revit window", null, Program.IsPretty));
                return Task.FromResult(1);
            }
            try
            {
                var r = revitWindow.BoundingRectangle;
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
            Console.Write(OutputFormatter.FormatError("ScreenshotFailed", "region", null, Program.IsPretty));
            return Task.FromResult(1);
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
                }, Program.IsPretty));
                return Task.FromResult(1);
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
            }, Program.IsPretty));
            return Task.FromResult(0);
        }
    }
}

public class CvClickCommand : ICommand
{
    public string Name => "cv-click";
    public string Description => "Find template image and click on the match";
    public string Usage => "cv-click <template.png> [--region x,y,w,h] [--threshold 0.8]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cv-click <template.png> [--region x,y,w,h] [--threshold 0.8]");
            return Task.FromResult(1);
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
                ["Check template name or place .png in ./templates/ or ./cv-templates/"], Program.IsPretty));
            return Task.FromResult(1);
        }

        using var template = CvMatchClient.LoadTemplate(templatePath);
        if (template == null)
        {
            Console.Write(OutputFormatter.FormatError("TemplateLoadFailed", templatePath, null, Program.IsPretty));
            return Task.FromResult(1);
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
            screenshot = ScreenshotHelper.CaptureBitmap(revitWindow);
            try
            {
                var r = revitWindow.BoundingRectangle;
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
            Console.Write(OutputFormatter.FormatError("ScreenshotFailed", "Revit window", null, Program.IsPretty));
            return Task.FromResult(1);
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
                }, Program.IsPretty));
                return Task.FromResult(1);
            }

            var absX = match.X + offsetX;
            var absY = match.Y + offsetY;

            var before = OutputFormatter.CaptureState(revitWindow);
            MouseControl.ClickAt(absX, absY);
            Thread.Sleep(200);
            var after = OutputFormatter.CaptureState(revitWindow);

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
            }, Program.IsPretty));
            return Task.FromResult(0);
        }
    }
}

public class CvListTemplatesCommand : ICommand
{
    public string Name => "cv-templates";
    public string Description => "List available template images";
    public string Usage => "cv-templates [filter]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
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
                templates = templates.Select(t => new
                {
                    name = t.Name,
                    path = t.Path,
                    sizeKb = t.SizeKb,
                }),
            },
        }, Program.IsPretty));
        return Task.FromResult(0);
    }
}
