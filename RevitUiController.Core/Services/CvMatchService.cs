using System.Drawing;
using UiController.Core.Models;

namespace UiController.Core.Services;

public class CvMatchService : ICvMatchService
{
    public CvMatchResult? MatchTemplate(Bitmap screenshot, Bitmap template, double threshold = 0.8)
        => CvMatchClient.MatchTemplate(screenshot, template, threshold);

    public CvMatchResult? MatchAny(Bitmap screenshot, List<Bitmap> templates, double threshold = 0.7)
        => CvMatchClient.MatchAny(screenshot, templates, threshold);

    public Bitmap? LoadTemplate(string path) => CvMatchClient.LoadTemplate(path);
    public string? FindTemplateFile(string name) => CvMatchClient.FindTemplateFile(name);
    public string[] GetSearchPaths() => CvMatchClient.GetSearchPaths();
    public TemplateMetadata? LoadTemplateMetadata(string templatePath) => CvMatchClient.LoadTemplateMetadata(templatePath);
    public List<(string Name, string Path, long SizeKb)> FindTemplates(string? filter = null) => CvMatchClient.FindTemplates(filter);
}
