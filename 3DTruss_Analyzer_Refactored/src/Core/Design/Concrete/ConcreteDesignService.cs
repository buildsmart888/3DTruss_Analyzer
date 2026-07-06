namespace TrussAnalyzer.Core.Design.Concrete;

using TrussAnalyzer.Core.Models;

public sealed class ConcreteDesignService
{
    private readonly StructuralModel _model;
    private readonly Dictionary<int, StructuralElement> _elements;
    private readonly Dictionary<int, Material> _materials;
    private readonly Dictionary<int, Section> _sections;

    public ConcreteDesignService(StructuralModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _elements = model.Elements.ToDictionary(e => e.Id);
        _materials = model.Materials.ToDictionary(m => m.Id);
        _sections = model.Sections.ToDictionary(s => s.Id);
    }

    public IReadOnlyList<DesignCheckResult> DesignFlexure(IEnumerable<ElementForceResult> elementResults)
    {
        ArgumentNullException.ThrowIfNull(elementResults);
        return elementResults.Select(DesignFlexure).ToList();
    }

    public DesignCheckResult DesignFlexure(ElementForceResult forces)
    {
        if (!_elements.TryGetValue(forces.ElementId, out var element))
            throw new InvalidOperationException($"Element {forces.ElementId} was not found in the model.");
        if (!_materials.TryGetValue(element.MaterialId, out var material))
            throw new InvalidOperationException($"Element {element.Id} references missing material {element.MaterialId}.");
        if (!_sections.TryGetValue(element.SectionId, out var section))
            throw new InvalidOperationException($"Element {element.Id} references missing section {element.SectionId}.");

        if (material.Type != MaterialType.Concrete)
        {
            return new DesignCheckResult
            {
                ElementId = element.Id,
                CheckType = "RC flexure",
                Status = DesignCheckStatus.NotApplicable,
                Notes = "RC flexure check applies only to concrete members."
            };
        }

        if (section.Type != SectionType.RC_Rectangular && (section.Width <= 0 || section.Depth <= 0))
        {
            return new DesignCheckResult
            {
                ElementId = element.Id,
                CheckType = "RC flexure",
                Status = DesignCheckStatus.NotApplicable,
                Notes = "Only rectangular RC member flexure is supported by this preliminary service."
            };
        }

        if (section.RebarArea <= 0 || section.EffectiveDepth <= 0)
        {
            return new DesignCheckResult
            {
                ElementId = forces.ElementId,
                CheckType = "RC flexure",
                Status = DesignCheckStatus.MissingData,
                Notes = "Rebar area and effective depth are required for RC flexure."
            };
        }

        double demand = FlexureDemand(forces);
        double capacity = RectangularFlexureCapacity(section, material);
        return MakeCheck(forces.ElementId, "RC flexure", demand, capacity, "Preliminary rectangular RC flexural capacity; not final code design.");
    }

    public double FlexureDemand(ElementForceResult forces)
    {
        return Math.Max(forces.MomentY, forces.MomentZ);
    }

    public double RectangularFlexureCapacity(Section section, Material material)
    {
        double fy = material.YieldStrength > 0 ? material.YieldStrength : _model.DesignSettings.DefaultRebarYieldStrength;
        return _model.DesignSettings.ConcreteFlexureResistanceFactor * section.RebarArea * fy * section.EffectiveDepth;
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
}

