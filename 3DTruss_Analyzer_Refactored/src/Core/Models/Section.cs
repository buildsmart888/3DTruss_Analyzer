namespace TrussAnalyzer.Core.Models;

public enum SectionType
{
    Generic,
    Rectangular,
    Circular,
    Pipe,
    IShape,
    Box,
    RC_Rectangular
}

/// <summary>
/// Cross-section properties in SI units.
/// A is area, Iy/Iz are second moments about local axes, and J is torsional constant.
/// </summary>
public class Section
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public SectionType Type { get; init; } = SectionType.Generic;
    public double Area { get; init; }
    public double Iy { get; init; }
    public double Iz { get; init; }
    public double J { get; init; }
    public double Depth { get; init; }
    public double Width { get; init; }
    public double Thickness { get; init; }
    public double Diameter { get; init; }
    public double RebarArea { get; init; }
    public double EffectiveDepth { get; init; }

    public static Section Generic(int id, string name, double area, double iy, double iz, double j) => new()
    {
        Id = id,
        Name = name,
        Type = SectionType.Generic,
        Area = area,
        Iy = iy,
        Iz = iz,
        J = j
    };

    public static Section Rectangular(int id, string name, double width, double depth) => new()
    {
        Id = id,
        Name = name,
        Type = SectionType.Rectangular,
        Width = width,
        Depth = depth,
        Area = width * depth,
        Iy = depth * Math.Pow(width, 3) / 12.0,
        Iz = width * Math.Pow(depth, 3) / 12.0,
        J = width * depth * (width * width + depth * depth) / 12.0
    };

    public static Section RcRectangular(
        int id,
        string name,
        double width,
        double depth,
        double rebarArea = 0,
        double effectiveDepth = 0) => new()
    {
        Id = id,
        Name = name,
        Type = SectionType.RC_Rectangular,
        Width = width,
        Depth = depth,
        Area = width * depth,
        Iy = depth * Math.Pow(width, 3) / 12.0,
        Iz = width * Math.Pow(depth, 3) / 12.0,
        J = width * depth * (width * width + depth * depth) / 12.0,
        RebarArea = rebarArea,
        EffectiveDepth = effectiveDepth
    };

    public void ValidateForAnalysis(int elementId)
    {
        if (Area <= 0)
            throw new InvalidOperationException($"Element {elementId} has a section with non-positive area.");
        if (Iy <= 0 || Iz <= 0 || J <= 0)
            throw new InvalidOperationException($"Element {elementId} has a section missing Iy, Iz, or J.");
    }
}
