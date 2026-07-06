namespace TrussAnalyzer.Core.Visualization;

using TrussAnalyzer.Core.Models;

public sealed record SectionVisualRectangle(double CenterY, double CenterZ, double WidthY, double DepthZ);

public sealed record SectionVisualProfile(
    SectionType Type,
    IReadOnlyList<SectionVisualRectangle> Rectangles,
    double Diameter,
    bool IsCircular,
    string Notes)
{
    public bool HasGeometry => IsCircular ? Diameter > 0 : Rectangles.Count > 0;
}

public static class SectionVisualProfileService
{
    public static SectionVisualProfile Create(Section section)
    {
        return section.Type switch
        {
            SectionType.Rectangular or SectionType.RC_Rectangular => CreateRectangular(section),
            SectionType.Circular => CreateCircular(section, "Circular section rendered as a round solid."),
            SectionType.Pipe => CreateCircular(section, "Pipe section rendered by outside diameter only; hollow wall is a visual simplification."),
            SectionType.IShape => CreateIShape(section),
            SectionType.Channel => CreateChannel(section),
            SectionType.Box => CreateBox(section),
            _ => CreateGeneric(section)
        };
    }

    private static SectionVisualProfile CreateRectangular(Section section)
    {
        double width = PositiveOrDefault(section.Width, Math.Sqrt(Math.Max(section.Area, 0)));
        double depth = PositiveOrDefault(section.Depth, width);
        return RectProfile(section.Type, width, depth, "Rectangular section rendered from Width x Depth.");
    }

    private static SectionVisualProfile CreateCircular(Section section, string notes)
    {
        double diameter = PositiveOrDefault(section.Diameter, 0);
        if (diameter <= 0 && section.Area > 0)
            diameter = Math.Sqrt(4.0 * section.Area / Math.PI);
        if (diameter <= 0)
            diameter = PositiveOrDefault(Math.Max(section.Width, section.Depth), 0);

        return new SectionVisualProfile(section.Type, Array.Empty<SectionVisualRectangle>(), diameter, diameter > 0, notes);
    }

    private static SectionVisualProfile CreateIShape(Section section)
    {
        var (width, depth, thickness) = GetSteelShapeDimensions(section);
        double webDepth = Math.Max(thickness, depth - 2.0 * thickness);
        var rectangles = new[]
        {
            new SectionVisualRectangle(0, depth / 2.0 - thickness / 2.0, width, thickness),
            new SectionVisualRectangle(0, -depth / 2.0 + thickness / 2.0, width, thickness),
            new SectionVisualRectangle(0, 0, thickness, webDepth)
        };

        return new SectionVisualProfile(section.Type, rectangles, 0, false, "I/H section rendered with uniform web and flange thickness from Thickness.");
    }

    private static SectionVisualProfile CreateChannel(Section section)
    {
        var (width, depth, thickness) = GetSteelShapeDimensions(section);
        double webY = -width / 2.0 + thickness / 2.0;
        var rectangles = new[]
        {
            new SectionVisualRectangle(webY, 0, thickness, depth),
            new SectionVisualRectangle(0, depth / 2.0 - thickness / 2.0, width, thickness),
            new SectionVisualRectangle(0, -depth / 2.0 + thickness / 2.0, width, thickness)
        };

        return new SectionVisualProfile(section.Type, rectangles, 0, false, "Channel/C section rendered with uniform web and flange thickness from Thickness.");
    }

    private static SectionVisualProfile CreateBox(Section section)
    {
        double width = PositiveOrDefault(section.Width, Math.Sqrt(Math.Max(section.Area, 0)));
        double depth = PositiveOrDefault(section.Depth, width);
        double thickness = GetUsableThickness(section.Thickness, width, depth);

        if (thickness <= 0)
            return RectProfile(section.Type, width, depth, "Box section rendered as solid because Thickness is not defined.");

        var rectangles = new[]
        {
            new SectionVisualRectangle(0, depth / 2.0 - thickness / 2.0, width, thickness),
            new SectionVisualRectangle(0, -depth / 2.0 + thickness / 2.0, width, thickness),
            new SectionVisualRectangle(-width / 2.0 + thickness / 2.0, 0, thickness, Math.Max(thickness, depth - 2.0 * thickness)),
            new SectionVisualRectangle(width / 2.0 - thickness / 2.0, 0, thickness, Math.Max(thickness, depth - 2.0 * thickness))
        };

        return new SectionVisualProfile(section.Type, rectangles, 0, false, "Box section rendered as four wall plates from Width, Depth, and Thickness.");
    }

    private static SectionVisualProfile CreateGeneric(Section section)
    {
        if (section.Diameter > 0)
            return CreateCircular(section, "Generic section rendered as circular because Diameter is defined.");
        if (section.Width > 0 || section.Depth > 0)
            return RectProfile(section.Type, PositiveOrDefault(section.Width, section.Depth), PositiveOrDefault(section.Depth, section.Width), "Generic section rendered as rectangular from Width/Depth.");
        if (section.Area > 0)
        {
            double side = Math.Sqrt(section.Area);
            return RectProfile(section.Type, side, side, "Generic section rendered as equivalent square from Area.");
        }

        return new SectionVisualProfile(section.Type, Array.Empty<SectionVisualRectangle>(), 0, false, "No visual section geometry because section dimensions are missing.");
    }

    private static SectionVisualProfile RectProfile(SectionType type, double width, double depth, string notes)
    {
        if (width <= 0 || depth <= 0)
            return new SectionVisualProfile(type, Array.Empty<SectionVisualRectangle>(), 0, false, notes);

        return new SectionVisualProfile(
            type,
            new[] { new SectionVisualRectangle(0, 0, width, depth) },
            0,
            false,
            notes);
    }

    private static (double Width, double Depth, double Thickness) GetSteelShapeDimensions(Section section)
    {
        double width = PositiveOrDefault(section.Width, Math.Sqrt(Math.Max(section.Area, 0)));
        double depth = PositiveOrDefault(section.Depth, width);
        double thickness = GetUsableThickness(section.Thickness, width, depth);
        if (thickness <= 0)
            thickness = Math.Min(width, depth) * 0.08;

        return (width, depth, thickness);
    }

    private static double GetUsableThickness(double thickness, double width, double depth)
    {
        double limit = Math.Min(width, depth) / 2.1;
        return thickness > 0 && thickness < limit ? thickness : 0;
    }

    private static double PositiveOrDefault(double value, double fallback) => value > 0 ? value : fallback;
}
