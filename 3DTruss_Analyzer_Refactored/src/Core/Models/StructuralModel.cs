namespace TrussAnalyzer.Core.Models;

public class StructuralModel
{
    public const int DefaultResultStationCount = 5;
    private int _resultStationCount = DefaultResultStationCount;

    public int SchemaVersion { get; init; } = 2;
    public List<Node> Nodes { get; init; } = new();
    public List<StructuralElement> Elements { get; init; } = new();
    public List<Material> Materials { get; init; } = new();
    public List<Section> Sections { get; init; } = new();
    public List<LoadCase> LoadCases { get; init; } = new();
    public List<LoadCombination> LoadCombinations { get; init; } = new();
    public List<LoadItem> Loads { get; init; } = new();
    public DesignSettings DesignSettings { get; set; } = new();
    public CoordinateConvention CoordinateSystem { get; set; } = CoordinateConvention.RightHanded_ZUp;
    public ViewerDisplayOptions DisplaySettings { get; set; } = new();
    public string ActiveLoadCaseId { get; set; } = string.Empty;
    public int ResultStationCount
    {
        get => _resultStationCount;
        set => _resultStationCount = value >= 2 ? value : DefaultResultStationCount;
    }

    public void EnsureDefaultLoadTemplates()
    {
        AddLoadCaseIfMissing("DL", "Dead Load", includeSelfWeight: true);
        AddLoadCaseIfMissing("LL", "Live Load", includeSelfWeight: false);
        AddLoadCaseIfMissing("WL", "Wind Load Placeholder", includeSelfWeight: false, LoadCaseType.Wind);
        AddLoadCaseIfMissing("EQ", "Seismic Load Placeholder", includeSelfWeight: false, LoadCaseType.Seismic);
    }

    private void AddLoadCaseIfMissing(string id, string name, bool includeSelfWeight, LoadCaseType type = LoadCaseType.Static)
    {
        if (LoadCases.Any(lc => string.Equals(lc.CaseId, id, StringComparison.OrdinalIgnoreCase)))
            return;

        LoadCases.Add(new LoadCase
        {
            CaseId = id,
            Name = name,
            Type = type,
            IncludeSelfWeight = includeSelfWeight,
            Description = name.Contains("Placeholder", StringComparison.OrdinalIgnoreCase)
                ? "Template only; automatic code load generation is not implemented."
                : string.Empty
        });
    }

    public static StructuralModel FromTrussSolver(TrussSolver solver)
    {
        var model = new StructuralModel();
        var materialIds = new Dictionary<Material, int>();
        int nextMaterialId = 1;

        foreach (var node in solver.GetNodes())
        {
            var copy = new Node(node.Id, node.Position)
            {
                ConstraintX = node.ConstraintX,
                ConstraintY = node.ConstraintY,
                ConstraintZ = node.ConstraintZ,
                ConstraintRX = true,
                ConstraintRY = true,
                ConstraintRZ = true
            };
            copy.ApplyForce(node.AppliedForce.X, node.AppliedForce.Y, node.AppliedForce.Z);
            model.Nodes.Add(copy);
        }

        foreach (var element in solver.GetElements())
        {
            if (!materialIds.TryGetValue(element.Material, out int materialId))
            {
                materialId = nextMaterialId++;
                materialIds[element.Material] = materialId;
                model.Materials.Add(element.Material with { Id = materialId });
            }

            int sectionId = element.Id;
            model.Sections.Add(Section.Generic(sectionId, $"Truss A={element.Area:g}", element.Area, element.Area * 1e-4, element.Area * 1e-4, element.Area * 1e-4));
            model.Elements.Add(new TrussElement(element.Id, element.StartNodeId, element.EndNodeId, materialId, sectionId));
        }

        return model;
    }
}
