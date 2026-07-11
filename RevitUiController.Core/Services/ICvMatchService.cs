using System.Drawing;
using UiController.Core.Models;

namespace UiController.Core.Services;

public interface ICvMatchService
{
    CvMatchResult? MatchTemplate(Bitmap screenshot, Bitmap template, double threshold = 0.8);
    CvMatchResult? MatchAny(Bitmap screenshot, List<Bitmap> templates, double threshold = 0.7);
    Bitmap? LoadTemplate(string path);
    string? FindTemplateFile(string name);
    string[] GetSearchPaths();
    TemplateMetadata? LoadTemplateMetadata(string templatePath);
    List<(string Name, string Path, long SizeKb)> FindTemplates(string? filter = null);
}
