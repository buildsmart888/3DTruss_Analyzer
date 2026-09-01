namespace TrussAnalyzer.Core.Design;

using TrussAnalyzer.Core.Design.Concrete;
using TrussAnalyzer.Core.Design.Steel;
using TrussAnalyzer.Core.Models;

public sealed class DesignCheckRunner
{
    private readonly StructuralModel _model;
    private readonly Dictionary<int, StructuralElement> _elements;
    private readonly Dictionary<int, Material> _materials;
    private readonly Dictionary<int, Section> _sections;
    private readonly SteelDesignService _steelDesignService;
    private readonly ConcreteDesignService _concreteDesignService;

    public DesignCheckRunner(StructuralModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _elements = model.Elements.ToDictionary(element => element.Id);
        _materials = model.Materials.ToDictionary(material => material.Id);
        _sections = model.Sections.ToDictionary(section => section.Id);
        _steelDesignService = new SteelDesignService(model);
        _concreteDesignService = new ConcreteDesignService(model);
    }

    public List<DesignCheckResult> Run(IEnumerable<ElementForceResult> elementResults)
    {
        ArgumentNullException.ThrowIfNull(elementResults);

        var checks = new List<DesignCheckResult>();
        foreach (var forces in elementResults)
        {
            if (!_elements.TryGetValue(forces.ElementId, out var element))
                throw new InvalidOperationException($"Element {forces.ElementId} was not found in the model.");
            if (!_materials.TryGetValue(element.MaterialId, out var material))
                throw new InvalidOperationException($"Element {element.Id} references missing material {element.MaterialId}.");
            if (!_sections.TryGetValue(element.SectionId, out var section))
                throw new InvalidOperationException($"Element {element.Id} references missing section {element.SectionId}.");

            if (material.Type == MaterialType.Concrete)
            {
                checks.Add(RunConcreteAxialCheck(forces, section, material));
                checks.Add(_concreteDesignService.DesignFlexure(forces));
                checks.Add(RunConcreteShearCheck(forces, section, material, _model.DesignSettings));
                continue;
            }

            if (material.Type is not (MaterialType.Steel or MaterialType.Aluminum or MaterialType.Custom))
            {
                checks.Add(NotApplicable(element.Id, "Material check", "No MVP check implemented for this material type."));
                continue;
            }

            checks.AddRange(_steelDesignService.DesignElement(forces));
        }

        return checks;
    }

    private static DesignCheckResult RunConcreteAxialCheck(ElementForceResult forces, Section section, Material material)
    {
        if (material.ConcreteCompressiveStrength <= 0)
        {
            return new DesignCheckResult
            {
                ElementId = forces.ElementId,
                CheckType = "RC axial",
                Status = DesignCheckStatus.MissingData,
                Notes = "Concrete f'c is required."
            };
        }

        double demand = Math.Abs(forces.AxialForce) / section.Area;
        double capacity = 0.35 * material.ConcreteCompressiveStrength;
        return MakeCheck(forces.ElementId, "RC axial stress", demand, capacity, "Simplified ACI-inspired axial stress check.");
    }

    private static DesignCheckResult RunConcreteShearCheck(ElementForceResult forces, Section section, Material material, DesignSettings settings)
    {
        if (material.ConcreteCompressiveStrength <= 0 || section.Width <= 0 || section.EffectiveDepth <= 0)
        {
            return new DesignCheckResult
            {
                ElementId = forces.ElementId,
                CheckType = "RC shear",
                Status = DesignCheckStatus.MissingData,
                Notes = "Concrete f'c, section width, and effective depth are required for RC shear."
            };
        }

        double demand = Math.Max(forces.ShearY, forces.ShearZ);
        double capacity = settings.ConcreteShearResistanceFactor *
            0.17 * Math.Sqrt(material.ConcreteCompressiveStrength / 1e6) * 1e6 *
            section.Width * section.EffectiveDepth;
        return MakeCheck(forces.ElementId, "RC shear", demand, capacity, "Preliminary ACI-inspired concrete shear check.");
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

    private static DesignCheckResult NotApplicable(int elementId, string type, string notes) => new()
    {
        ElementId = elementId,
        CheckType = type,
        Status = DesignCheckStatus.NotApplicable,
        Notes = notes
    };
}
