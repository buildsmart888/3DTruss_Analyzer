namespace TrussAnalyzer.Core.Design.Steel;

using TrussAnalyzer.Core.Models;

public sealed class SteelDesignService
{
    private readonly StructuralModel _model;
    private readonly Dictionary<int, Node> _nodes;
    private readonly Dictionary<int, StructuralElement> _elements;
    private readonly Dictionary<int, Material> _materials;
    private readonly Dictionary<int, Section> _sections;

    public SteelDesignService(StructuralModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _nodes = model.Nodes.ToDictionary(n => n.Id);
        _elements = model.Elements.ToDictionary(e => e.Id);
        _materials = model.Materials.ToDictionary(m => m.Id);
        _sections = model.Sections.ToDictionary(s => s.Id);
    }

    public IReadOnlyList<DesignCheckResult> Design(IEnumerable<ElementForceResult> elementResults)
    {
        ArgumentNullException.ThrowIfNull(elementResults);
        return elementResults.SelectMany(DesignElement).ToList();
    }

    public IReadOnlyList<DesignCheckResult> DesignElement(ElementForceResult forces)
    {
        if (!_elements.TryGetValue(forces.ElementId, out var element))
            throw new InvalidOperationException($"Element {forces.ElementId} was not found in the model.");
        if (!_materials.TryGetValue(element.MaterialId, out var material))
            throw new InvalidOperationException($"Element {element.Id} references missing material {element.MaterialId}.");
        if (!_sections.TryGetValue(element.SectionId, out var section))
            throw new InvalidOperationException($"Element {element.Id} references missing section {element.SectionId}.");

        if (material.Type is not (MaterialType.Steel or MaterialType.Aluminum or MaterialType.Custom))
            return Array.Empty<DesignCheckResult>();

        double fy = material.YieldStrength > 0 ? material.YieldStrength : _model.DesignSettings.DefaultSteelYieldStrength;
        if (fy <= 0)
        {
            return new[]
            {
                new DesignCheckResult
                {
                    ElementId = element.Id,
                    CheckType = "Yield stress",
                    Status = DesignCheckStatus.MissingData,
                    Notes = "Yield strength is required."
                }
            };
        }

        double axialDemand = Math.Abs(forces.AxialForce) / section.Area;
        double flexuralDemand = FlexuralStressDemand(forces, section);
        double resistance = fy * _model.DesignSettings.SteelResistanceFactor;
        double compressionCapacity = CompressionBucklingStress(element, material, section, fy) * _model.DesignSettings.SteelResistanceFactor;
        double shearDemand = Math.Max(forces.ShearY, forces.ShearZ) / section.Area;
        double interaction = axialDemand / resistance + flexuralDemand / resistance;

        return new[]
        {
            MakeCheck(element.Id, "Steel tension/yield", axialDemand, resistance, "Preliminary AISC-inspired axial stress check."),
            MakeCheck(element.Id, "Steel flexure", flexuralDemand, resistance, "Preliminary bending stress check."),
            MakeCheck(element.Id, "Steel compression buckling", axialDemand, compressionCapacity, $"Preliminary slenderness check, KL/r={Slenderness(element, section):F1}."),
            MakeCheck(element.Id, "Steel shear", shearDemand, 0.6 * resistance, "Preliminary shear stress check."),
            new DesignCheckResult
            {
                ElementId = element.Id,
                CheckType = "Axial + bending",
                Demand = interaction,
                Capacity = 1,
                Utilization = interaction,
                Status = interaction <= 1 ? DesignCheckStatus.OK : DesignCheckStatus.NG,
                Notes = "Preliminary linear interaction check, not final code design."
            }
        };
    }

    private double CompressionBucklingStress(StructuralElement element, Material material, Section section, double fy)
    {
        double slenderness = Slenderness(element, section);
        double fe = slenderness > 0 ? Math.PI * Math.PI * material.YoungsModulus / (slenderness * slenderness) : fy;
        return Math.Min(fy, 0.877 * fe);
    }

    private double Slenderness(StructuralElement element, Section section)
    {
        double r = Math.Sqrt(Math.Min(section.Iy, section.Iz) / section.Area);
        double length = ElementLength(element);
        return r > 0 ? _model.DesignSettings.CompressionEffectiveLengthFactor * length / r : double.PositiveInfinity;
    }

    private double ElementLength(StructuralElement element)
    {
        var start = _nodes[element.StartNodeId];
        var end = _nodes[element.EndNodeId];
        return start.Position.DistanceTo(end.Position);
    }

    private static DesignCheckResult MakeCheck(int elementId, string type, double demand, double capacity, string notes)
    {
        double utilization = capacity > 0 ? demand / capacity : double.PositiveInfinity;
        return new DesignCheckResult
        {
            ElementId = elementId,
            CheckType = type,
            Demand = demand,
            Capacity = capacity,
            Utilization = utilization,
            Status = utilization <= 1 ? DesignCheckStatus.OK : DesignCheckStatus.NG,
            Notes = notes
        };
    }

    private static double FlexuralStressDemand(ElementForceResult forces, Section section)
    {
        double sy = section.Depth > 0 ? section.Iy / (section.Depth / 2.0) : Math.Sqrt(section.Area * section.Iy);
        double sz = section.Width > 0 ? section.Iz / (section.Width / 2.0) : Math.Sqrt(section.Area * section.Iz);
        double myStress = sy > 0 ? forces.MomentY / sy : 0;
        double mzStress = sz > 0 ? forces.MomentZ / sz : 0;
        return Math.Abs(myStress) + Math.Abs(mzStress);
    }
}

