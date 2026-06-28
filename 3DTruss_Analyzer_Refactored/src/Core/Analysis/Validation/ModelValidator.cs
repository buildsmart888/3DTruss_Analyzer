namespace TrussAnalyzer.Core.Analysis.Validation;

using TrussAnalyzer.Core.Models;

public class ModelValidator
{
    private const int DenseSolverWarningDof = 300;
    private readonly StructuralModel _model;
    private readonly Dictionary<int, Node> _nodes = new();
    private readonly Dictionary<int, Material> _materials = new();
    private readonly Dictionary<int, Section> _sections = new();

    public ModelValidator(StructuralModel model)
    {
        _model = model;
        BuildLookupTables();
    }

    public IReadOnlyList<ModelValidationMessage> Validate()
    {
        var messages = new List<ModelValidationMessage>();

        if (_model.Nodes.Count == 0)
            messages.Add(Error("No nodes have been defined."));
        if (_model.Elements.Count == 0)
            messages.Add(Error("No elements have been defined."));
        if (!_model.Nodes.Any(n => n.IsConstrained))
            messages.Add(Error("No supports are defined."));

        AddDuplicateChecks(messages, _model.Nodes.Select(n => n.Id), "node");
        AddDuplicateChecks(messages, _model.Elements.Select(e => e.Id), "element");
        AddDuplicateChecks(messages, _model.Materials.Select(m => m.Id), "material");
        AddDuplicateChecks(messages, _model.Sections.Select(s => s.Id), "section");

        foreach (var element in _model.Elements)
        {
            if (!_nodes.ContainsKey(element.StartNodeId))
                messages.Add(Error($"Element {element.Id} references missing start node {element.StartNodeId}.", SelectedModelObjectType.Element, element.Id));
            if (!_nodes.ContainsKey(element.EndNodeId))
                messages.Add(Error($"Element {element.Id} references missing end node {element.EndNodeId}.", SelectedModelObjectType.Element, element.Id));
            if (!_materials.ContainsKey(element.MaterialId))
                messages.Add(Error($"Element {element.Id} references missing material {element.MaterialId}.", SelectedModelObjectType.Element, element.Id));
            if (!_sections.ContainsKey(element.SectionId))
                messages.Add(Error($"Element {element.Id} references missing section {element.SectionId}.", SelectedModelObjectType.Element, element.Id));

            if (_nodes.TryGetValue(element.StartNodeId, out var start) &&
                _nodes.TryGetValue(element.EndNodeId, out var end) &&
                start.Position.DistanceTo(end.Position) < 1e-10)
            {
                messages.Add(Error($"Element {element.Id} has zero or near-zero length.", SelectedModelObjectType.Element, element.Id));
            }

            if (element.Type == ElementType.Frame3D &&
                _sections.TryGetValue(element.SectionId, out var section) &&
                (section.Area <= 0 || section.Iy <= 0 || section.Iz <= 0 || section.J <= 0))
            {
                messages.Add(Error($"Frame element {element.Id} requires positive A, Iy, Iz, and J.", SelectedModelObjectType.Element, element.Id));
            }

            if (element.Type == ElementType.Truss)
            {
                messages.Add(new ModelValidationMessage
                {
                    Severity = "Info",
                    Message = $"Element {element.Id} is a truss element: only axial force is recovered; shear, torsion, and bending moments are not applicable.",
                    ObjectType = SelectedModelObjectType.Element,
                    ObjectId = element.Id
                });
            }

            if (_nodes.TryGetValue(element.StartNodeId, out var axisStart) &&
                _nodes.TryGetValue(element.EndNodeId, out var axisEnd))
            {
                var axes = GetLocalAxes(axisStart.Position, axisEnd.Position, element.RollAngleRadians);
                double handedness = axes.XAxis.Cross(axes.YAxis).Dot(axes.ZAxis);
                if (handedness < 0.999)
                    messages.Add(new ModelValidationMessage { Severity = "Warning", Message = $"Element {element.Id} local axes are not strongly right-handed; check roll angle.", ObjectType = SelectedModelObjectType.Element, ObjectId = element.Id });
                if (Math.Abs(element.RollAngleRadians) > Math.PI * 2)
                    messages.Add(new ModelValidationMessage { Severity = "Warning", Message = $"Element {element.Id} roll angle exceeds 360 degrees; confirm units are radians in JSON and degrees in UI.", ObjectType = SelectedModelObjectType.Element, ObjectId = element.Id });
            }
        }

        int dof = _model.Nodes.Count * 6;
        if (dof > DenseSolverWarningDof)
            messages.Add(new ModelValidationMessage { Severity = "Warning", Message = $"Model has {dof} DOF. Dense solver may be slow for large models." });

        foreach (var element in _model.Elements.Where(e => e.Type == ElementType.Frame3D && e.Releases.HasAny))
        {
            if (element.Releases.StartMomentY && element.Releases.EndMomentY)
                messages.Add(new ModelValidationMessage { Severity = "Warning", Message = $"Frame element {element.Id} has both My ends released; check for a mechanism.", ObjectType = SelectedModelObjectType.Element, ObjectId = element.Id });
            if (element.Releases.StartMomentZ && element.Releases.EndMomentZ)
                messages.Add(new ModelValidationMessage { Severity = "Warning", Message = $"Frame element {element.Id} has both Mz ends released; check for a mechanism.", ObjectType = SelectedModelObjectType.Element, ObjectId = element.Id });
        }

        return messages;
    }

    private void BuildLookupTables()
    {
        foreach (var node in _model.Nodes)
            _nodes[node.Id] = node;
        foreach (var material in _model.Materials)
            _materials[material.Id] = material;
        foreach (var section in _model.Sections)
            _sections[section.Id] = section;
    }

    private static void AddDuplicateChecks(List<ModelValidationMessage> messages, IEnumerable<int> ids, string label)
    {
        foreach (var id in ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key))
            messages.Add(Error($"Duplicate {label} id {id}."));
    }

    private static LocalAxes GetLocalAxes(Point3D start, Point3D end, double rollAngleRadians = 0)
    {
        var x = end.Subtract(start).Normalize();
        var reference = Math.Abs(x.Dot(new Vector3D(0, 0, 1))) > 0.95
            ? new Vector3D(0, 1, 0)
            : new Vector3D(0, 0, 1);
        var y = reference.Cross(x).Normalize();
        var z = x.Cross(y).Normalize();

        if (Math.Abs(rollAngleRadians) > 1e-12)
        {
            double c = Math.Cos(rollAngleRadians);
            double s = Math.Sin(rollAngleRadians);
            y = y.Scale(c).Add(z.Scale(s)).Normalize();
            z = x.Cross(y).Normalize();
        }

        return new LocalAxes(x, y, z);
    }

    private static ModelValidationMessage Error(string message) => new() { Severity = "Error", Message = message };
    private static ModelValidationMessage Error(string message, SelectedModelObjectType objectType, int objectId) => new()
    {
        Severity = "Error",
        Message = message,
        ObjectType = objectType,
        ObjectId = objectId
    };
}
