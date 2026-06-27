namespace TrussAnalyzer.Core.Models;

public enum CoordinateConvention
{
    RightHanded_ZUp
}

public enum ResultDiagramMode
{
    Rendered,
    Wireframe,
    Deformed,
    ForceDiagram,
    MomentDiagram,
    Utilization
}

public enum SelectedModelObjectType
{
    None,
    Node,
    Element,
    Material,
    Section,
    LoadCase,
    LoadCombination,
    Load,
    Result
}

public class ViewerLayerVisibility
{
    public bool Nodes { get; set; } = true;
    public bool Elements { get; set; } = true;
    public bool Supports { get; set; } = true;
    public bool Loads { get; set; } = true;
    public bool LoadLabels { get; set; } = true;
    public bool ReactionLabels { get; set; } = true;
    public bool Labels { get; set; } = true;
    public bool LocalAxes { get; set; } = true;
    public bool DeformedShape { get; set; } = true;
    public bool Diagrams { get; set; } = true;
    public bool Grid { get; set; } = true;
}

public class ViewerDisplayOptions
{
    public CoordinateConvention CoordinateConvention { get; set; } = CoordinateConvention.RightHanded_ZUp;
    public ViewerLayerVisibility Layers { get; set; } = new();
    public ResultDiagramMode DiagramMode { get; set; } = ResultDiagramMode.Utilization;
    public double DeformationScale { get; set; } = 1.0;
    public string LabelDensity { get; set; } = "Normal";
}

public class SelectedModelObject
{
    public SelectedModelObjectType Type { get; init; }
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;

    public static SelectedModelObject None { get; } = new();
}

public class ElementEndForceResult
{
    public Vector3D Force { get; init; } = Vector3D.Zero;
    public Vector3D Moment { get; init; } = Vector3D.Zero;
}

public class StructuralResultSummary
{
    public double MaxShearY { get; init; }
    public double MaxShearZ { get; init; }
    public double MaxMomentY { get; init; }
    public double MaxMomentZ { get; init; }
    public double MaxTorsion { get; init; }
    public double MaxAxialForce { get; init; }
    public double MaxStress { get; init; }
}

public class ResultColorScale
{
    public double Minimum { get; init; }
    public double Maximum { get; init; }
    public string Mode { get; init; } = string.Empty;
}
