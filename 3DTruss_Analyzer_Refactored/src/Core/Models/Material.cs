namespace TrussAnalyzer.Core.Models;

/// <summary>
/// Material properties for truss elements.
/// All units are in SI (Pascal, kg/m³).
/// </summary>
public record Material
{
    /// <summary>Name of the material (e.g., "Steel A36")</summary>
    public string Name { get; init; } = "";

    /// <summary>Young's Modulus / Elastic Modulus (units: Pascal = N/m²)</summary>
    public double YoungsModulus { get; init; }

    /// <summary>Density (units: kg/m³)</summary>
    public double Density { get; init; }

    /// <summary>Poisson's ratio (dimensionless)</summary>
    public double PoissonsRatio { get; init; }

    /// <summary>Yield strength for stress checking (units: Pascal)</summary>
    public double YieldStrength { get; init; }

    public Material()
    {
    }

    public Material(string name, double youngsModulus, double density)
        : this(name, youngsModulus, 0.3, density)
    {
    }

    public Material(string name, double youngsModulus, double poissonsRatio, double density, double yieldStrength = 0)
    {
        Name = name;
        YoungsModulus = youngsModulus;
        PoissonsRatio = poissonsRatio;
        Density = density;
        YieldStrength = yieldStrength;
    }

    /// <summary>Standard structural steel properties</summary>
    public static Material StructuralSteel => new()
    {
        Name = "Structural Steel",
        YoungsModulus = 200e9,      // 200 GPa
        Density = 7850,             // 7850 kg/m³
        PoissonsRatio = 0.3,
        YieldStrength = 250e6       // 250 MPa
    };

    /// <summary>Aluminum 6061-T6 properties</summary>
    public static Material Aluminum6061 => new()
    {
        Name = "Aluminum 6061-T6",
        YoungsModulus = 68.9e9,     // 68.9 GPa
        Density = 2700,             // 2700 kg/m³
        PoissonsRatio = 0.33,
        YieldStrength = 276e6       // 276 MPa
    };
}
