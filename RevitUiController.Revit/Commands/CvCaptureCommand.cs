using RevitUiController.Core;
using System.IO;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;

namespace RevitUiController.Revit.Commands;

public class CvCaptureCommand : ICommand
{
    public string Name => "cv-capture";
    public string Description => "Capture a screen region as a PNG template for later use with cv-match / cv-click";
    public string Usage => "cv-capture <name> --region x,y,w,h\n  cv-capture <name> --element \"ButtonName\"";

    private static readonly string[] TemplateDirs =
    [
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cv-templates"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReVibe", "UiController", "templates"),
    ];

    private static string? ResolveTemplateDir()
    {
        foreach (var dir in TemplateDirs)
        {
            if (!Directory.Exists(dir))
                continue;
            return dir;
        }
        var first = TemplateDirs[0];
        Directory.CreateDirectory(first);
        return first;
    }

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("CvCaptureCommand", "Usage: cv-capture <name> --region x,y,w,h");
            LoggingService.Error("CvCaptureCommand", "       cv-capture <name> --element \"ButtonName\"");
            return 1;
        }

        var name = args[0];
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Write(OutputFormatter.FormatError("InvalidName", "name is empty", null, CoreSettings.GlobalOptions));
            return 1;
        }

        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            name += ".png";

        (int x, int y, int w, int h)? region = null;
        string? elementName = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--region", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
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
                else
                {
                    Console.Write(OutputFormatter.FormatError("InvalidRegion", args[i],
                        ["Format: --region x,y,w,h"], CoreSettings.GlobalOptions));
                    return 1;
                }
            }
            else if (string.Equals(args[i], "--element", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                elementName = args[++i];
            }
        }

        if (region.HasValue && elementName != null)
        {
            Console.Write(OutputFormatter.FormatError("ConflictingArgs", "--region and --element are mutually exclusive",
                ["Use either --region x,y,w,h or --element \"Name\", not both"], CoreSettings.GlobalOptions));
            return 1;
        }

        if (elementName != null)
        {
            var elements = AutomationHelper.FindControlsByName(window, elementName);
            var found = elements.FirstOrDefault(e =>
            {
                try { return e.IsEnabled && e.IsOffscreen == false; } catch { return false; }
            });

            if (found == null)
            {
                Console.Write(OutputFormatter.FormatError("ElementNotFound", elementName,
                    ["Check the element name, use 'find' to list available elements"], CoreSettings.GlobalOptions));
                return 1;
            }

            try
            {
                var r = found.BoundingRectangle;
                region = ((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
            }
            catch (Exception ex)
            {
                Console.Write(OutputFormatter.FormatError("ElementRectFailed", elementName,
                    [$"Failed to get bounding rect: {ex.Message}"], CoreSettings.GlobalOptions));
                return 1;
            }
        }

        if (!region.HasValue)
        {
            Console.Write(OutputFormatter.FormatError("MissingRegion", "no --region or --element specified",
                ["Provide --region x,y,w,h or --element \"ButtonName\""], CoreSettings.GlobalOptions));
            return 1;
        }

        var (rx2, ry2, rw2, rh2) = region.Value;

        var dir = ResolveTemplateDir();
        if (dir == null)
        {
            Console.Write(OutputFormatter.FormatError("NoTemplateDir", "",
                ["Cannot create template directory. Check permissions."], CoreSettings.GlobalOptions));
            return 1;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var bitmap = ScreenshotHelper.CaptureBitmap(rx2, ry2, rw2, rh2);
        if (bitmap == null)
        {
            Console.Write(OutputFormatter.FormatError("CaptureFailed", $"region {rx2},{ry2},{rw2},{rh2}", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var pngPath = Path.Combine(dir, name);
        bitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);

        var dpiX = bitmap.HorizontalResolution;
        var dpiY = bitmap.VerticalResolution;

        var profile = RevitVersionProfile.Detect(window);
        var revitYear = profile.Year;

        var metadata = new TemplateMetadata
        {
            Name = Path.GetFileNameWithoutExtension(name),
            FileName = name,
            Width = rw2,
            Height = rh2,
            DpiX = dpiX,
            DpiY = dpiY,
            RevitVersion = revitYear,
            Timestamp = DateTime.UtcNow,
            Region = $"{rx2},{ry2},{rw2},{rh2}",
            ElementName = elementName,
        };

        var metaPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(name) + ".meta.json");
        var metaJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        await File.WriteAllTextAsync(metaPath, metaJson, ct);

        sw.Stop();

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "cv-capture",
            Success = true,
            Data = new
            {
                name = metadata.Name,
                fileName = metadata.FileName,
                path = Path.GetFullPath(pngPath),
                metaPath = Path.GetFullPath(metaPath),
                width = metadata.Width,
                height = metadata.Height,
                dpiX = metadata.DpiX,
                dpiY = metadata.DpiY,
                revitVersion = metadata.RevitVersion,
                timestamp = metadata.Timestamp,
                region = metadata.Region,
                elementName = metadata.ElementName,
                sizeKb = new FileInfo(pngPath).Length / 1024,
                durationMs = sw.ElapsedMilliseconds,
            },
        }, CoreSettings.GlobalOptions));
        return 0;
    }
}
