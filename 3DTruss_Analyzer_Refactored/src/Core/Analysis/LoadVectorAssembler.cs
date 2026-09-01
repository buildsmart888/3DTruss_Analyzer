namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;

public sealed class LoadVectorAssembler
{
    private const double Gravity = 9.81;
    private readonly StructuralModel _model;
    private readonly DofIndexer _dofIndexer;
    private readonly Dictionary<int, Node> _nodes;
    private readonly Dictionary<int, StructuralElement> _elements;
    private readonly Dictionary<int, Material> _materials;
    private readonly Dictionary<int, Section> _sections;
    private readonly FrameElementStiffnessProvider _stiffnessProvider;

    public LoadVectorAssembler(StructuralModel model, DofIndexer dofIndexer, FrameElementStiffnessProvider? stiffnessProvider = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _dofIndexer = dofIndexer ?? throw new ArgumentNullException(nameof(dofIndexer));
        _stiffnessProvider = stiffnessProvider ?? new FrameElementStiffnessProvider(model.FrameAnalysisOptions);
        _nodes = new Dictionary<int, Node>();
        _elements = new Dictionary<int, StructuralElement>();
        _materials = new Dictionary<int, Material>();
        _sections = new Dictionary<int, Section>();

        foreach (var node in model.Nodes)
            _nodes[node.Id] = node;
        foreach (var element in model.Elements)
            _elements[element.Id] = element;
        foreach (var material in model.Materials)
            _materials[material.Id] = material;
        foreach (var section in model.Sections)
            _sections[section.Id] = section;
    }

    public LoadAssemblyResult CreateResult() => new(_dofIndexer.TotalDof);

    public void AssembleInto(LoadAssemblyResult result, LoadCase? loadCase, double combinationFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.GlobalLoadVector.Length != _dofIndexer.TotalDof)
            throw new ArgumentException("Load assembly result length must match the model DOF count.", nameof(result));

        string? caseId = loadCase?.CaseId;
        double factor = combinationFactor * (loadCase?.LoadFactor ?? 1.0);

        if (loadCase == null)
        {
            foreach (var node in _model.Nodes)
                AddNodeLoad(result.GlobalLoadVector, node.Id, node.AppliedForce, node.AppliedMoment, factor: 1.0);
        }
        else
        {
            foreach (var nodeForce in loadCase.NodeForces)
            {
                AddNodeLoad(
                    result.GlobalLoadVector,
                    nodeForce.Key,
                    new Vector3D(nodeForce.Value.Fx, nodeForce.Value.Fy, nodeForce.Value.Fz),
                    Vector3D.Zero,
                    factor);
            }
        }

        foreach (var item in _model.Loads.Where(load => caseId == null || string.Equals(load.LoadCaseId, caseId, StringComparison.OrdinalIgnoreCase)))
        {
            switch (item)
            {
                case NodalLoad nodal:
                    AddNodeLoad(result.GlobalLoadVector, nodal.NodeId, nodal.Force, nodal.Moment, factor);
                    break;
                case MemberPointLoad point:
                    var pointElement = _elements[point.ElementId];
                    AddEquivalentElementLoad(result, pointElement, PrepareEquivalentLocalLoad(pointElement, BuildPointLoadEquivalentLocal(pointElement, point)), factor);
                    AddMemberPointDiagramLoad(result, pointElement, point, factor);
                    break;
                case MemberDistributedLoad distributed:
                    var distributedElement = _elements[distributed.ElementId];
                    AddEquivalentElementLoad(result, distributedElement, PrepareEquivalentLocalLoad(distributedElement, BuildDistributedLoadEquivalentLocal(distributedElement, distributed)), factor);
                    AddMemberDistributedDiagramLoad(result, distributedElement, distributed, factor);
                    break;
                case MemberTemperatureLoad temperature:
                    var temperatureElement = _elements[temperature.ElementId];
                    AddEquivalentElementLoad(result, temperatureElement, PrepareEquivalentLocalLoad(temperatureElement, BuildTemperatureLoadEquivalentLocal(temperatureElement, temperature)), factor);
                    break;
            }
        }

        if (loadCase?.IncludeSelfWeight == true)
            AddSelfWeight(result.GlobalLoadVector, factor);
    }

    private void AddNodeLoad(double[] loadVector, int nodeId, Vector3D force, Vector3D moment, double factor)
    {
        int dofBase = _dofIndexer.GetNodeDofBase(nodeId);
        loadVector[dofBase] += force.X * factor;
        loadVector[dofBase + 1] += force.Y * factor;
        loadVector[dofBase + 2] += force.Z * factor;
        loadVector[dofBase + 3] += moment.X * factor;
        loadVector[dofBase + 4] += moment.Y * factor;
        loadVector[dofBase + 5] += moment.Z * factor;
    }

    private double[] BuildDistributedLoadEquivalentLocal(StructuralElement element, MemberDistributedLoad load)
    {
        double length = GetGeometry(element).Length;
        var loadPerLength = ResolveLoadVectorToLocal(element, load.ForcePerLength, load.Direction);
        double start = Math.Clamp(load.StartRelativeDistance, 0, 1);
        double end = Math.Clamp(load.EndRelativeDistance, 0, 1);
        if (end < start)
            (start, end) = (end, start);
        if (end - start <= 1e-9)
            return new double[12];

        if (start > 1e-9 || end < 1.0 - 1e-9)
            return BuildPartialDistributedLoadEquivalentLocal(length, loadPerLength, start, end);

        var equivalent = new double[12];
        equivalent[0] = loadPerLength.X * length / 2.0;
        equivalent[6] = loadPerLength.X * length / 2.0;
        equivalent[1] = loadPerLength.Y * length / 2.0;
        equivalent[5] = loadPerLength.Y * length * length / 12.0;
        equivalent[7] = loadPerLength.Y * length / 2.0;
        equivalent[11] = -loadPerLength.Y * length * length / 12.0;
        equivalent[2] = loadPerLength.Z * length / 2.0;
        equivalent[4] = -loadPerLength.Z * length * length / 12.0;
        equivalent[8] = loadPerLength.Z * length / 2.0;
        equivalent[10] = loadPerLength.Z * length * length / 12.0;
        return equivalent;
    }

    private void AddMemberPointDiagramLoad(LoadAssemblyResult result, StructuralElement element, MemberPointLoad load, double factor)
    {
        result.AddMemberDiagramLoad(new MemberPointDiagramLoad(
            element.Id,
            Math.Clamp(load.RelativeDistance, 0, 1),
            ResolveLoadVectorToLocal(element, load.Force, load.Direction).Scale(factor),
            ResolveLoadVectorToLocal(element, load.Moment, load.Direction).Scale(factor)));
    }

    private void AddMemberDistributedDiagramLoad(LoadAssemblyResult result, StructuralElement element, MemberDistributedLoad load, double factor)
    {
        double start = Math.Clamp(load.StartRelativeDistance, 0, 1);
        double end = Math.Clamp(load.EndRelativeDistance, 0, 1);
        if (end < start)
            (start, end) = (end, start);
        if (end - start <= 1e-9)
            return;

        result.AddMemberDiagramLoad(new MemberDistributedDiagramLoad(
            element.Id,
            start,
            end,
            ResolveLoadVectorToLocal(element, load.ForcePerLength, load.Direction).Scale(factor)));
    }

    private static double[] BuildPartialDistributedLoadEquivalentLocal(double length, Vector3D loadPerLength, double start, double end)
    {
        var equivalent = new double[12];
        const int segments = 16;
        double range = end - start;
        double segmentLength = length * range / segments;
        for (int i = 0; i < segments; i++)
        {
            double relativePosition = start + range * (i + 0.5) / segments;
            var pointEquivalent = BuildPointLoadEquivalentLocal(length, relativePosition, loadPerLength.Scale(segmentLength), Vector3D.Zero);
            for (int dof = 0; dof < equivalent.Length; dof++)
                equivalent[dof] += pointEquivalent[dof];
        }

        return equivalent;
    }

    private double[] BuildPointLoadEquivalentLocal(StructuralElement element, MemberPointLoad load)
    {
        double length = GetGeometry(element).Length;
        double relativePosition = Math.Clamp(load.RelativeDistance, 0, 1);
        var force = ResolveLoadVectorToLocal(element, load.Force, load.Direction);
        var moment = ResolveLoadVectorToLocal(element, load.Moment, load.Direction);
        return BuildPointLoadEquivalentLocal(length, relativePosition, force, moment);
    }

    private double[] BuildTemperatureLoadEquivalentLocal(StructuralElement element, MemberTemperatureLoad load)
    {
        if (load.ThermalExpansionCoefficient <= 0)
            throw new InvalidOperationException($"Temperature load on element {element.Id} requires a positive thermal expansion coefficient.");

        var material = _materials[element.MaterialId];
        var section = _sections[element.SectionId];
        double thermalForce = material.YoungsModulus * section.Area * load.ThermalExpansionCoefficient * load.TemperatureChange;
        var equivalent = new double[12];
        equivalent[0] = -thermalForce;
        equivalent[6] = thermalForce;
        return equivalent;
    }

    private double[] PrepareEquivalentLocalLoad(StructuralElement element, double[] equivalentLocalLoad)
    {
        var geometry = GetGeometry(element);
        return _stiffnessProvider.CondenseEquivalentLocalLoad(
            element,
            _materials[element.MaterialId],
            _sections[element.SectionId],
            geometry.Length,
            equivalentLocalLoad);
    }

    private static double[] BuildPointLoadEquivalentLocal(double length, double relativePosition, Vector3D force, Vector3D moment)
    {
        var equivalent = new double[12];
        equivalent[0] = force.X * (1 - relativePosition);
        equivalent[6] = force.X * relativePosition;

        double n1 = 1 - 3 * relativePosition * relativePosition + 2 * relativePosition * relativePosition * relativePosition;
        double n2 = length * (relativePosition - 2 * relativePosition * relativePosition + relativePosition * relativePosition * relativePosition);
        double n3 = 3 * relativePosition * relativePosition - 2 * relativePosition * relativePosition * relativePosition;
        double n4 = length * (-relativePosition * relativePosition + relativePosition * relativePosition * relativePosition);

        equivalent[1] = force.Y * n1;
        equivalent[5] = force.Y * n2;
        equivalent[7] = force.Y * n3;
        equivalent[11] = force.Y * n4;
        equivalent[2] = force.Z * n1;
        equivalent[4] = -force.Z * n2;
        equivalent[8] = force.Z * n3;
        equivalent[10] = -force.Z * n4;
        equivalent[3] = moment.X * (1 - relativePosition);
        equivalent[9] = moment.X * relativePosition;
        equivalent[4] += moment.Y * (1 - relativePosition);
        equivalent[10] += moment.Y * relativePosition;
        equivalent[5] += moment.Z * (1 - relativePosition);
        equivalent[11] += moment.Z * relativePosition;
        return equivalent;
    }

    private void AddEquivalentElementLoad(LoadAssemblyResult result, StructuralElement element, double[] equivalentLocal, double factor)
    {
        var equivalentGlobal = Multiply(Matrix.Transpose(GetGeometry(element).AnalysisTransformation), equivalentLocal);
        int[] dofMap = _dofIndexer.GetElementDofMap(element);
        for (int dof = 0; dof < dofMap.Length; dof++)
            result.GlobalLoadVector[dofMap[dof]] += equivalentGlobal[dof] * factor;

        var accumulatedLocal = result.GetOrCreateEquivalentElementLoad(element.Id);
        for (int dof = 0; dof < accumulatedLocal.Length; dof++)
            accumulatedLocal[dof] += equivalentLocal[dof] * factor;
    }

    private Vector3D ResolveLoadVectorToLocal(StructuralElement element, Vector3D vector, LoadDirection direction)
    {
        if (direction is LoadDirection.LocalX or LoadDirection.LocalY or LoadDirection.LocalZ)
            return vector;

        var axes = GetGeometry(element).LocalAxes;
        return new Vector3D(vector.Dot(axes.XAxis), vector.Dot(axes.YAxis), vector.Dot(axes.ZAxis));
    }

    private void AddSelfWeight(double[] loadVector, double factor)
    {
        foreach (var element in _model.Elements)
        {
            var material = _materials[element.MaterialId];
            var section = _sections[element.SectionId];
            double length = GetGeometry(element).Length;
            double halfWeight = material.Density * section.Area * length * Gravity / 2.0;
            AddNodeLoad(loadVector, element.StartNodeId, new Vector3D(0, 0, -halfWeight), Vector3D.Zero, factor);
            AddNodeLoad(loadVector, element.EndNodeId, new Vector3D(0, 0, -halfWeight), Vector3D.Zero, factor);
        }
    }

    private FrameElementGeometry GetGeometry(StructuralElement element)
    {
        return FrameElementGeometryResolver.Resolve(element, _nodes[element.StartNodeId], _nodes[element.EndNodeId]);
    }

    private static double[] Multiply(double[,] matrix, double[] vector)
    {
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        var result = new double[rows];
        for (int row = 0; row < rows; row++)
        {
            double sum = 0;
            for (int column = 0; column < columns; column++)
                sum += matrix[row, column] * vector[column];
            result[row] = sum;
        }

        return result;
    }
}
