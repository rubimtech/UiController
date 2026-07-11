using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using UiController.Core.Models;
using Point = OpenCvSharp.Point;

namespace UiController.Core;

public class CvMatchResult
{
    public bool Found { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int TemplateWidth { get; set; }
    public int TemplateHeight { get; set; }
    public double Confidence { get; set; }
    public string? TemplatePath { get; set; }
}

public static class CvMatchClient
{
    private static readonly string[] SearchPaths =
    [
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cv-templates"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReVibe", "UiController", "templates"),
    ];

    public static CvMatchResult? MatchTemplate(Bitmap screenshot, Bitmap template, double threshold = 0.8)
    {
        using var src = BitmapToMat(screenshot);
        using var tpl = BitmapToMat(template);
        using var result = new Mat();

        if (src.Width < tpl.Width || src.Height < tpl.Height)
            return null;

        Cv2.MatchTemplate(src, tpl, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

        if (maxVal < threshold)
            return null;

        return new CvMatchResult
        {
            Found = true,
            X = maxLoc.X + tpl.Width / 2,
            Y = maxLoc.Y + tpl.Height / 2,
            TemplateWidth = tpl.Width,
            TemplateHeight = tpl.Height,
            Confidence = maxVal,
        };
    }

    public static CvMatchResult? MatchAny(Bitmap screenshot, List<Bitmap> templates, double threshold = 0.7)
    {
        CvMatchResult? best = null;
        foreach (var tpl in templates)
        {
            var result = MatchTemplate(screenshot, tpl, threshold);
            if (result != null && (best == null || result.Confidence > best.Confidence))
                best = result;
        }
        return best;
    }

    public static Bitmap? LoadTemplate(string path)
    {
        if (!File.Exists(path))
            return null;
        return new Bitmap(path);
    }

    public static string? FindTemplateFile(string name)
    {
        if (File.Exists(name))
            return Path.GetFullPath(name);

        foreach (var dir in SearchPaths)
        {
            if (!Directory.Exists(dir))
                continue;
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }
        return null;
    }

    public static string[] GetSearchPaths() => SearchPaths;

    private static readonly JsonSerializerOptions MetaJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static TemplateMetadata? LoadTemplateMetadata(string templatePath)
    {
        var dir = Path.GetDirectoryName(templatePath);
        var name = Path.GetFileNameWithoutExtension(templatePath);
        var metaPath = Path.Combine(dir ?? ".", name + ".meta.json");
        if (!File.Exists(metaPath))
            return null;
        try
        {
            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<TemplateMetadata>(json, MetaJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static List<(string Name, string Path, long SizeKb)> FindTemplates(string? filter = null)
    {
        var results = new List<(string, string, long)>();
        foreach (var dir in SearchPaths)
        {
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.png"))
            {
                var name = Path.GetFileName(file);
                if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                var info = new FileInfo(file);
                results.Add((name, Path.GetFullPath(file), info.Length / 1024));
            }
        }
        return results;
    }

    private static Mat BitmapToMat(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        return Mat.FromStream(ms, ImreadModes.Color);
    }
}
