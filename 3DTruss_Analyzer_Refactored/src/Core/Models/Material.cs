namespace TrussAnalyzer.Core.Models;

/// <summary>
/// Material properties for structural elements. Units are SI.
/// </summary>
public record Material
{
    public int Id { get; init; }
    public MaterialType Type { get; init; } = MaterialType.Custom;
    public string Name { get; init; } = "";
    public double YoungsModulus { get; init; }
    public double ShearModulus { get; init; }
    public double Density { get; init; }
    public double PoissonsRatio { get; init; }
    public double YieldStrength { get; init; }
    public double UltimateStrength { get; init; }
    public double ConcreteCompressiveStrength { get; init; }

    public double EffectiveShearModulus =>
        ShearModulus > 0 ? ShearModulus : YoungsModulus / (2.0 * (1.0 + PoissonsRatio));

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

    public static Material StructuralSteel => new()
    {
        Name = "Structural Steel",
        Type = MaterialType.Steel,
        YoungsModulus = 200e9,
        Density = 7850,
        PoissonsRatio = 0.3,
        YieldStrength = 250e6,
        UltimateStrength = 400e6
    };

    public static Material Aluminum6061 => new()
    {
        Name = "Aluminum 6061-T6",
        Type = MaterialType.Aluminum,
        YoungsModulus = 68.9e9,
        Density = 2700,
        PoissonsRatio = 0.33,
        YieldStrength = 276e6
    };

    public static Material Concrete30MPa => new()
    {
        Name = "Concrete 30 MPa",
        Type = MaterialType.Concrete,
        YoungsModulus = 25e9,
        Density = 2400,
        PoissonsRatio = 0.2,
        ConcreteCompressiveStrength = 30e6
    };
}

public enum MaterialType
{
    Steel,
    Concrete,
    Aluminum,
    Timber,
    Custom
}
