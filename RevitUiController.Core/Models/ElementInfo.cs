namespace RevitUiController.Core.Models;

public class ElementInfo
{
    public string ControlType { get; set; } = "";
    public string Name { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Visible { get; set; }
    public RectInfo? BoundingRect { get; set; }
    public List<ElementInfo>? Children { get; set; }
    public int? Index { get; set; }
}

public class RectInfo
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public RectInfo(double x, double y, double w, double h) => (X, Y, Width, Height) = (x, y, w, h);
}
