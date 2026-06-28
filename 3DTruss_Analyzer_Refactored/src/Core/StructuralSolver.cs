namespace TrussAnalyzer.Core;

using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Analysis.Validation;
using TrussAnalyzer.Core.Utilities;

public class StructuralSolver
{
    private const double Gravity = 9.81;
    private const int DenseSolverWarningDof = 300;
    private readonly StructuralModel _model;
    private readonly ILinearSystemSolver _linearSolver;
    private readonly Dictionary<int, int> _nodeIndex = new();
    private readonly Dictionary<int, Node> _nodes = new();
    private readonly Dictionary<int, StructuralElement> _elements = new();
    private readonly Dictionary<int, Material> _materials = new();
    private readonly Dictionary<int, Section> _sections = new();
    private readonly Dictionary<int, double[]> _equivalentElementLoadsLocal = new();
    private double[,] _originalK = new double[0, 0];
    private double[] _originalF = Array.Empty<double>();
    private double[] _u = Array.Empty<double>();
    private int _lastNonZeroStiffnessEntries;

    public StructuralSolver(StructuralModel model, ILinearSystemSolver? linearSolver = null)
    {
        _model = model;
        _linearSolver = linearSolver ?? new DenseLinearSystemSolver();
        BuildLookupTables();
    }

    public IReadOnlyList<ModelValidationMessage> ValidateModel()
    {
        return new ModelValidator(_model).Validate();
    }

    public StructuralAnalysisResult Analyze(string? loadCaseId = null)
    {
        var errors = ValidateModel().Where(m => m.Severity == "Error").ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(e => e.Message)));

        int dof = _model.Nodes.Count * 6;
        var k = new double[dof, dof];
        var f = new double[dof];
        _equivalentElementLoadsLocal.Clear();

        foreach (var element in _model.Elements)
            AssembleElement(k, element);

        var loadCase = loadCaseId == null
            ? null
            : _model.LoadCases.FirstOrDefault(lc => string.Equals(lc.CaseId, loadCaseId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Load case '{loadCaseId}' was not found.");

        AssembleLoads(f, loadCase);
        _originalK = (double[,])k.Clone();
        _originalF = (double[])f.Clone();

        ApplyBoundaryConditions(k, f);
        _lastNonZeroStiffnessEntries = CountNonZero(k);
        _u = _linearSolver.Solve(k, f);

        return BuildResult(loadCase?.Name ?? "Default");
    }

    public StructuralAnalysisResult AnalyzeCombination(string combinationId)
    {
        var combination = _model.LoadCombinations.FirstOrDefault(c => string.Equals(c.CombinationId, combinationId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Load combination '{combinationId}' was not found.");

        int dof = _model.Nodes.Count * 6;
        var k = new double[dof, dof];
        var f = new double[dof];
        _equivalentElementLoadsLocal.Clear();

        foreach (var element in _model.Elements)
            AssembleElement(k, element);

        foreach (var entry in combination.LoadCases)
        {
            var loadCase = _model.LoadCases.FirstOrDefault(lc => string.Equals(lc.CaseId, entry.Key, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Load combination '{combination.Name}' references missing load case '{entry.Key}'.");
            AssembleLoads(f, loadCase, entry.Value);
        }

        _originalK = (double[,])k.Clone();
        _originalF = (double[])f.Clone();
        ApplyBoundaryConditions(k, f);
        _lastNonZeroStiffnessEntries = CountNonZero(k);
        _u = _linearSolver.Solve(k, f);
        return BuildResult(combination.Name);
    }

    private void BuildLookupTables()
    {
        for (int i = 0; i < _model.Nodes.Count; i++)
        {
            _nodeIndex[_model.Nodes[i].Id] = i;
            _nodes[_model.Nodes[i].Id] = _model.Nodes[i];
        }

        foreach (var element in _model.Elements)
            _elements[element.Id] = element;
        foreach (var material in _model.Materials)
            _materials[material.Id] = material;
        foreach (var section in _model.Sections)
            _sections[section.Id] = section;
    }

    private void AssembleElement(double[,] k, StructuralElement element)
    {
        var start = _nodes[element.StartNodeId];
        var end = _nodes[element.EndNodeId];
        var material = _materials[element.MaterialId];
        var section = _sections[element.SectionId];
        double length = start.Position.DistanceTo(end.Position);

        if (length < 1e-10)
            throw new InvalidOperationException($"Element {element.Id} has zero or near-zero length.");

        var local = element.Type == ElementType.Truss
            ? BuildTrussLocalStiffness(material, section, length)
            : BuildFrameLocalStiffness(material, section, length, element.Id);
        ApplyFrameReleases(local, element);
        var t = BuildTransformation(start.Position, end.Position, element.RollAngleRadians);
        var global = Matrix.Multiply(Matrix.Multiply(Matrix.Transpose(t), local), t);

        int[] map = GetElementDofMap(element);
        for (int i = 0; i < 12; i++)
        {
            for (int j = 0; j < 12; j++)
                k[map[i], map[j]] += global[i, j];
        }
    }

    private static double[,] BuildTrussLocalStiffness(Material material, Section section, double length)
    {
        if (section.Area <= 0)
            throw new InvalidOperationException("Truss section area must be positive.");

        var k = new double[12, 12];
        double axial = material.YoungsModulus * section.Area / length;
        k[0, 0] = axial;
        k[0, 6] = -axial;
        k[6, 0] = -axial;
        k[6, 6] = axial;
        return k;
    }

    private static double[,] BuildFrameLocalStiffness(Material material, Section section, double length, int elementId)
    {
        section.ValidateForAnalysis(elementId);
        var k = new double[12, 12];
        double l2 = length * length;
        double l3 = l2 * length;
        double e = material.YoungsModulus;
        double g = material.EffectiveShearModulus;

        double ea = e * section.Area / length;
        k[0, 0] = ea; k[0, 6] = -ea; k[6, 0] = -ea; k[6, 6] = ea;

        double gj = g * section.J / length;
        k[3, 3] = gj; k[3, 9] = -gj; k[9, 3] = -gj; k[9, 9] = gj;

        AddBending(k, 1, 5, 7, 11, e * section.Iz, length, l2, l3, positiveCoupling: true);
        AddBending(k, 2, 4, 8, 10, e * section.Iy, length, l2, l3, positiveCoupling: false);
        Symmetrize(k);
        return k;
    }

    private static void AddBending(double[,] k, int v1, int r1, int v2, int r2, double ei, double l, double l2, double l3, bool positiveCoupling)
    {
        double a = 12 * ei / l3;
        double b = 6 * ei / l2;
        double c = 4 * ei / l;
        double d = 2 * ei / l;
        double s = positiveCoupling ? 1.0 : -1.0;

        k[v1, v1] += a;
        k[v1, r1] += s * b;
        k[v1, v2] += -a;
        k[v1, r2] += s * b;
        k[r1, r1] += c;
        k[r1, v2] += -s * b;
        k[r1, r2] += d;
        k[v2, v2] += a;
        k[v2, r2] += -s * b;
        k[r2, r2] += c;
    }

    private static void Symmetrize(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                matrix[j, i] = matrix[i, j];
            }
        }
    }

    private static void ApplyFrameReleases(double[,] localStiffness, StructuralElement element)
    {
        if (element.Type != ElementType.Frame3D || !element.Releases.HasAny)
            return;

        if (element.Releases.StartMomentY)
            ReleaseLocalDof(localStiffness, 4);
        if (element.Releases.StartMomentZ)
            ReleaseLocalDof(localStiffness, 5);
        if (element.Releases.EndMomentY)
            ReleaseLocalDof(localStiffness, 10);
        if (element.Releases.EndMomentZ)
            ReleaseLocalDof(localStiffness, 11);
    }

    private static void ReleaseLocalDof(double[,] matrix, int dof)
    {
        int n = matrix.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            matrix[dof, i] = 0;
            matrix[i, dof] = 0;
        }
    }

    public static LocalAxes GetLocalAxes(Point3D start, Point3D end, double rollAngleRadians = 0)
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

    public static double[,] BuildTransformation(Point3D start, Point3D end, double rollAngleRadians = 0)
    {
        var axes = GetLocalAxes(start, end, rollAngleRadians);

        var r = new[,]
        {
            { axes.XAxis.X, axes.XAxis.Y, axes.XAxis.Z },
            { axes.YAxis.X, axes.YAxis.Y, axes.YAxis.Z },
            { axes.ZAxis.X, axes.ZAxis.Y, axes.ZAxis.Z }
        };
        var t = new double[12, 12];
        for (int block = 0; block < 4; block++)
        {
            int offset = block * 3;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                    t[offset + i, offset + j] = r[i, j];
            }
        }
        return t;
    }

    private void AssembleLoads(double[] f, LoadCase? loadCase, double combinationFactor = 1.0)
    {
        string? caseId = loadCase?.CaseId;
        double factor = combinationFactor * (loadCase?.LoadFactor ?? 1.0);

        if (loadCase == null)
        {
            foreach (var node in _model.Nodes)
            {
                AddNodeLoad(f, node.Id, node.AppliedForce, node.AppliedMoment, 1.0);
            }
        }
        else
        {
            foreach (var nodeForce in loadCase.NodeForces)
                AddNodeLoad(f, nodeForce.Key, new Vector3D(nodeForce.Value.Fx, nodeForce.Value.Fy, nodeForce.Value.Fz), Vector3D.Zero, factor);
        }

        foreach (var item in _model.Loads.Where(l => caseId == null || string.Equals(l.LoadCaseId, caseId, StringComparison.OrdinalIgnoreCase)))
        {
            switch (item)
            {
                case NodalLoad nodal:
                    AddNodeLoad(f, nodal.NodeId, nodal.Force, nodal.Moment, factor);
                    break;
                case MemberPointLoad point:
                    AddMemberPointLoad(f, point, factor);
                    break;
                case MemberDistributedLoad distributed:
                    AddMemberDistributedLoad(f, distributed, factor);
                    break;
            }
        }

        if (loadCase?.IncludeSelfWeight == true)
            AddSelfWeight(f, factor);
    }

    private void AddNodeLoad(double[] f, int nodeId, Vector3D force, Vector3D moment, double factor)
    {
        int idx = DofBase(nodeId);
        f[idx] += force.X * factor;
        f[idx + 1] += force.Y * factor;
        f[idx + 2] += force.Z * factor;
        f[idx + 3] += moment.X * factor;
        f[idx + 4] += moment.Y * factor;
        f[idx + 5] += moment.Z * factor;
    }

    private void AddMemberPointLoad(double[] f, MemberPointLoad load, double factor)
    {
        var element = _elements[load.ElementId];
        var equivalentLocal = BuildPointLoadEquivalentLocal(element, load);
        AddEquivalentElementLoad(f, element, equivalentLocal, factor);
    }

    private void AddMemberDistributedLoad(double[] f, MemberDistributedLoad load, double factor)
    {
        var element = _elements[load.ElementId];
        var equivalentLocal = BuildDistributedLoadEquivalentLocal(element, load);
        AddEquivalentElementLoad(f, element, equivalentLocal, factor);
    }

    private double[] BuildDistributedLoadEquivalentLocal(StructuralElement element, MemberDistributedLoad load)
    {
        double length = _nodes[element.StartNodeId].Position.DistanceTo(_nodes[element.EndNodeId].Position);
        var q = ResolveLoadVectorToLocal(element, load.ForcePerLength, load.Direction);
        double a = Math.Clamp(load.StartRelativeDistance, 0, 1);
        double b = Math.Clamp(load.EndRelativeDistance, 0, 1);
        if (b < a)
            (a, b) = (b, a);
        if (b - a <= 1e-9)
            return new double[12];

        if (a > 1e-9 || b < 1.0 - 1e-9)
            return BuildPartialDistributedLoadEquivalentLocal(length, q, a, b);

        var equivalent = new double[12];

        equivalent[0] = q.X * length / 2.0;
        equivalent[6] = q.X * length / 2.0;

        equivalent[1] = q.Y * length / 2.0;
        equivalent[5] = q.Y * length * length / 12.0;
        equivalent[7] = q.Y * length / 2.0;
        equivalent[11] = -q.Y * length * length / 12.0;

        equivalent[2] = q.Z * length / 2.0;
        equivalent[4] = -q.Z * length * length / 12.0;
        equivalent[8] = q.Z * length / 2.0;
        equivalent[10] = q.Z * length * length / 12.0;

        return equivalent;
    }

    private static double[] BuildPartialDistributedLoadEquivalentLocal(double length, Vector3D q, double start, double end)
    {
        var equivalent = new double[12];
        const int segments = 16;
        double range = end - start;
        double segmentLength = length * range / segments;
        for (int i = 0; i < segments; i++)
        {
            double xi = start + range * (i + 0.5) / segments;
            var pointEquivalent = BuildPointLoadEquivalentLocal(length, xi, q.Scale(segmentLength), Vector3D.Zero);
            for (int j = 0; j < equivalent.Length; j++)
                equivalent[j] += pointEquivalent[j];
        }

        return equivalent;
    }

    private double[] BuildPointLoadEquivalentLocal(StructuralElement element, MemberPointLoad load)
    {
        double length = _nodes[element.StartNodeId].Position.DistanceTo(_nodes[element.EndNodeId].Position);
        double xi = Math.Clamp(load.RelativeDistance, 0, 1);
        var p = ResolveLoadVectorToLocal(element, load.Force, load.Direction);
        var m = ResolveLoadVectorToLocal(element, load.Moment, load.Direction);
        return BuildPointLoadEquivalentLocal(length, xi, p, m);
    }

    private static double[] BuildPointLoadEquivalentLocal(double length, double xi, Vector3D p, Vector3D m)
    {
        var equivalent = new double[12];

        equivalent[0] = p.X * (1 - xi);
        equivalent[6] = p.X * xi;

        double n1 = 1 - 3 * xi * xi + 2 * xi * xi * xi;
        double n2 = length * (xi - 2 * xi * xi + xi * xi * xi);
        double n3 = 3 * xi * xi - 2 * xi * xi * xi;
        double n4 = length * (-xi * xi + xi * xi * xi);

        equivalent[1] = p.Y * n1;
        equivalent[5] = p.Y * n2;
        equivalent[7] = p.Y * n3;
        equivalent[11] = p.Y * n4;

        equivalent[2] = p.Z * n1;
        equivalent[4] = -p.Z * n2;
        equivalent[8] = p.Z * n3;
        equivalent[10] = -p.Z * n4;

        equivalent[3] = m.X * (1 - xi);
        equivalent[9] = m.X * xi;
        equivalent[4] += m.Y * (1 - xi);
        equivalent[10] += m.Y * xi;
        equivalent[5] += m.Z * (1 - xi);
        equivalent[11] += m.Z * xi;

        return equivalent;
    }

    private void AddEquivalentElementLoad(double[] f, StructuralElement element, double[] equivalentLocal, double factor)
    {
        var start = _nodes[element.StartNodeId];
        var end = _nodes[element.EndNodeId];
        var t = BuildTransformation(start.Position, end.Position, element.RollAngleRadians);
        var equivalentGlobal = Multiply(Matrix.Transpose(t), equivalentLocal);
        int[] map = GetElementDofMap(element);
        for (int i = 0; i < map.Length; i++)
            f[map[i]] += equivalentGlobal[i] * factor;

        if (!_equivalentElementLoadsLocal.TryGetValue(element.Id, out var current))
        {
            current = new double[12];
            _equivalentElementLoadsLocal[element.Id] = current;
        }

        for (int i = 0; i < current.Length; i++)
            current[i] += equivalentLocal[i] * factor;
    }

    private Vector3D ResolveLoadVectorToLocal(StructuralElement element, Vector3D vector, LoadDirection direction)
    {
        if (direction is LoadDirection.LocalX or LoadDirection.LocalY or LoadDirection.LocalZ)
            return vector;

        var start = _nodes[element.StartNodeId];
        var end = _nodes[element.EndNodeId];
        var axes = GetLocalAxes(start.Position, end.Position, element.RollAngleRadians);
        return new Vector3D(
            vector.Dot(axes.XAxis),
            vector.Dot(axes.YAxis),
            vector.Dot(axes.ZAxis));
    }

    private void AddSelfWeight(double[] f, double factor)
    {
        foreach (var element in _model.Elements)
        {
            var material = _materials[element.MaterialId];
            var section = _sections[element.SectionId];
            double length = _nodes[element.StartNodeId].Position.DistanceTo(_nodes[element.EndNodeId].Position);
            double halfWeight = material.Density * section.Area * length * Gravity / 2.0;
            AddNodeLoad(f, element.StartNodeId, new Vector3D(0, 0, -halfWeight), Vector3D.Zero, factor);
            AddNodeLoad(f, element.EndNodeId, new Vector3D(0, 0, -halfWeight), Vector3D.Zero, factor);
        }
    }

    private void ApplyBoundaryConditions(double[,] k, double[] f)
    {
        foreach (var node in _model.Nodes)
        {
            int idx = DofBase(node.Id);
            ApplyConstraint(k, f, idx, node.ConstraintX);
            ApplyConstraint(k, f, idx + 1, node.ConstraintY);
            ApplyConstraint(k, f, idx + 2, node.ConstraintZ);
            ApplyConstraint(k, f, idx + 3, node.ConstraintRX);
            ApplyConstraint(k, f, idx + 4, node.ConstraintRY);
            ApplyConstraint(k, f, idx + 5, node.ConstraintRZ);
        }
    }

    private static void ApplyConstraint(double[,] k, double[] f, int dof, bool constrained)
    {
        if (!constrained)
            return;

        int n = f.Length;
        for (int i = 0; i < n; i++)
        {
            k[dof, i] = 0;
            k[i, dof] = 0;
        }
        k[dof, dof] = 1;
        f[dof] = 0;
    }

    private StructuralAnalysisResult BuildResult(string loadCaseName)
    {
        var nodeResults = new List<StructuralNodeResult>();
        foreach (var node in _model.Nodes)
        {
            int idx = DofBase(node.Id);
            var reaction = CalculateReaction(idx);
            var result = new StructuralNodeResult
            {
                NodeId = node.Id,
                Displacement = new Vector3D(_u[idx], _u[idx + 1], _u[idx + 2]),
                Rotation = new Vector3D(_u[idx + 3], _u[idx + 4], _u[idx + 5]),
                ReactionForce = new Vector3D(node.ConstraintX ? reaction[0] : 0, node.ConstraintY ? reaction[1] : 0, node.ConstraintZ ? reaction[2] : 0),
                ReactionMoment = new Vector3D(node.ConstraintRX ? reaction[3] : 0, node.ConstraintRY ? reaction[4] : 0, node.ConstraintRZ ? reaction[5] : 0)
            };
            node.SetDisplacement(result.Displacement.X, result.Displacement.Y, result.Displacement.Z);
            node.SetRotation(result.Rotation.X, result.Rotation.Y, result.Rotation.Z);
            node.SetReactionForce(result.ReactionForce.X, result.ReactionForce.Y, result.ReactionForce.Z);
            node.SetReactionMoment(result.ReactionMoment.X, result.ReactionMoment.Y, result.ReactionMoment.Z);
            nodeResults.Add(result);
        }

        var elementResults = _model.Elements.Select(RecoverElementForces).ToList();
        var checks = elementResults.SelectMany(r => RunDesignChecks(r, _elements[r.ElementId])).ToList();
        var equilibrium = CalculateEquilibrium(nodeResults);

        return new StructuralAnalysisResult
        {
            LoadCaseName = loadCaseName,
            NodeResults = nodeResults,
            ElementResults = elementResults,
            DesignChecks = checks,
            Equilibrium = equilibrium,
            MaxDisplacement = nodeResults.Count == 0 ? 0 : nodeResults.Max(n => n.Displacement.Magnitude),
            Diagnostics = BuildDiagnostics(equilibrium, nodeResults)
        };
    }

    private double[] CalculateReaction(int dofBase)
    {
        var reaction = new double[6];
        for (int local = 0; local < 6; local++)
        {
            int row = dofBase + local;
            double value = 0;
            for (int j = 0; j < _u.Length; j++)
                value += _originalK[row, j] * _u[j];
            reaction[local] = value - _originalF[row];
        }
        return reaction;
    }

    private ElementForceResult RecoverElementForces(StructuralElement element)
    {
        var start = _nodes[element.StartNodeId];
        var end = _nodes[element.EndNodeId];
        var material = _materials[element.MaterialId];
        var section = _sections[element.SectionId];
        double length = start.Position.DistanceTo(end.Position);
        var localK = element.Type == ElementType.Truss
            ? BuildTrussLocalStiffness(material, section, length)
            : BuildFrameLocalStiffness(material, section, length, element.Id);
        ApplyFrameReleases(localK, element);
        var t = BuildTransformation(start.Position, end.Position, element.RollAngleRadians);
        int[] map = GetElementDofMap(element);
        var globalU = new double[12];
        for (int i = 0; i < 12; i++)
            globalU[i] = _u[map[i]];

        var localU = Multiply(t, globalU);
        var localF = Multiply(localK, localU);
        if (_equivalentElementLoadsLocal.TryGetValue(element.Id, out var equivalentLoad))
        {
            for (int i = 0; i < localF.Length; i++)
                localF[i] -= equivalentLoad[i];
        }
        double axial = localF[6];
        double stress = section.Area > 0 ? axial / section.Area : 0;

        return new ElementForceResult
        {
            ElementId = element.Id,
            AxialForce = axial,
            ShearY = Math.Max(Math.Abs(localF[1]), Math.Abs(localF[7])),
            ShearZ = Math.Max(Math.Abs(localF[2]), Math.Abs(localF[8])),
            Torsion = Math.Max(Math.Abs(localF[3]), Math.Abs(localF[9])),
            MomentY = Math.Max(Math.Abs(localF[4]), Math.Abs(localF[10])),
            MomentZ = Math.Max(Math.Abs(localF[5]), Math.Abs(localF[11])),
            Stress = stress,
            LocalEndForces = localF,
            StartEndForces = new ElementEndForceResult
            {
                Force = new Vector3D(localF[0], localF[1], localF[2]),
                Moment = new Vector3D(localF[3], localF[4], localF[5])
            },
            EndEndForces = new ElementEndForceResult
            {
                Force = new Vector3D(localF[6], localF[7], localF[8]),
                Moment = new Vector3D(localF[9], localF[10], localF[11])
            },
            StationResults = BuildStationResults(element.Id, localF, _model.ResultStationCount)
        };
    }

    private static List<ElementStationResult> BuildStationResults(int elementId, double[] localF, int stationCount)
    {
        int count = stationCount >= 2 ? stationCount : StructuralModel.DefaultResultStationCount;
        var stations = new List<ElementStationResult>();
        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0 : (double)i / (count - 1);
            stations.Add(new ElementStationResult
            {
                ElementId = elementId,
                RelativePosition = t,
                AxialForce = Lerp(localF[0], -localF[6], t),
                ShearY = Lerp(localF[1], -localF[7], t),
                ShearZ = Lerp(localF[2], -localF[8], t),
                Torsion = Lerp(localF[3], -localF[9], t),
                MomentY = Lerp(localF[4], -localF[10], t),
                MomentZ = Lerp(localF[5], -localF[11], t)
            });
        }

        return stations;
    }

    private IEnumerable<DesignCheckResult> RunDesignChecks(ElementForceResult forces, StructuralElement element)
    {
        var material = _materials[element.MaterialId];
        var section = _sections[element.SectionId];

        if (material.Type == MaterialType.Concrete)
        {
            yield return RunConcreteAxialCheck(forces, section, material);
            yield return RunConcreteFlexureCheck(forces, section, material);
            yield return RunConcreteShearCheck(forces, section, material, _model.DesignSettings);
            yield break;
        }

        if (material.Type is not (MaterialType.Steel or MaterialType.Aluminum or MaterialType.Custom))
        {
            yield return NotApplicable(element.Id, "Material check", "No MVP check implemented for this material type.");
            yield break;
        }

        double fy = material.YieldStrength > 0 ? material.YieldStrength : _model.DesignSettings.DefaultSteelYieldStrength;
        if (fy <= 0)
        {
            yield return new DesignCheckResult { ElementId = element.Id, CheckType = "Yield stress", Status = DesignCheckStatus.MissingData, Notes = "Yield strength is required." };
            yield break;
        }

        double axialDemand = Math.Abs(forces.AxialForce) / section.Area;
        yield return MakeCheck(element.Id, "Steel tension/yield", axialDemand, fy * _model.DesignSettings.SteelResistanceFactor, "Preliminary AISC-inspired axial stress check.");

        double flexuralDemand = FlexuralStressDemand(forces, section);
        yield return MakeCheck(element.Id, "Steel flexure", flexuralDemand, fy * _model.DesignSettings.SteelResistanceFactor, "Preliminary bending stress check.");

        double r = Math.Sqrt(Math.Min(section.Iy, section.Iz) / section.Area);
        double length = _nodes[element.StartNodeId].Position.DistanceTo(_nodes[element.EndNodeId].Position);
        double slenderness = r > 0 ? _model.DesignSettings.CompressionEffectiveLengthFactor * length / r : double.PositiveInfinity;
        double fe = slenderness > 0 ? Math.PI * Math.PI * material.YoungsModulus / (slenderness * slenderness) : fy;
        double compressionCapacity = Math.Min(fy, 0.877 * fe);
        yield return MakeCheck(element.Id, "Steel compression buckling", axialDemand, compressionCapacity * _model.DesignSettings.SteelResistanceFactor, $"Preliminary slenderness check, KL/r={slenderness:F1}.");

        double shearDemand = Math.Max(forces.ShearY, forces.ShearZ) / section.Area;
        yield return MakeCheck(element.Id, "Steel shear", shearDemand, 0.6 * fy * _model.DesignSettings.SteelResistanceFactor, "Preliminary shear stress check.");

        double factoredFy = fy * _model.DesignSettings.SteelResistanceFactor;
        double interaction = axialDemand / factoredFy + flexuralDemand / factoredFy;
        yield return new DesignCheckResult
        {
            ElementId = element.Id,
            CheckType = "Axial + bending",
            Demand = interaction,
            Capacity = 1,
            Utilization = interaction,
            Status = interaction <= 1 ? DesignCheckStatus.OK : DesignCheckStatus.NG,
            Notes = "Preliminary linear interaction check, not final code design."
        };
    }

    private static DesignCheckResult RunConcreteAxialCheck(ElementForceResult forces, Section section, Material material)
    {
        if (material.ConcreteCompressiveStrength <= 0)
            return new DesignCheckResult { ElementId = forces.ElementId, CheckType = "RC axial", Status = DesignCheckStatus.MissingData, Notes = "Concrete f'c is required." };

        double demand = Math.Abs(forces.AxialForce) / section.Area;
        double capacity = 0.35 * material.ConcreteCompressiveStrength;
        return MakeCheck(forces.ElementId, "RC axial stress", demand, capacity, "Simplified ACI-inspired axial stress check.");
    }

    private static DesignCheckResult RunConcreteFlexureCheck(ElementForceResult forces, Section section, Material material)
    {
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

        double fy = material.YieldStrength > 0 ? material.YieldStrength : 420e6;
        double capacity = 0.9 * section.RebarArea * fy * section.EffectiveDepth;
        double demand = Math.Max(forces.MomentY, forces.MomentZ);
        return MakeCheck(forces.ElementId, "RC flexure", demand, capacity, "Simplified rectangular RC flexural capacity.");
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

    private static double FlexuralStressDemand(ElementForceResult forces, Section section)
    {
        double sy = section.Depth > 0 ? section.Iy / (section.Depth / 2.0) : Math.Sqrt(section.Area * section.Iy);
        double sz = section.Width > 0 ? section.Iz / (section.Width / 2.0) : Math.Sqrt(section.Area * section.Iz);
        double myStress = sy > 0 ? forces.MomentY / sy : 0;
        double mzStress = sz > 0 ? forces.MomentZ / sz : 0;
        return Math.Abs(myStress) + Math.Abs(mzStress);
    }

    private EquilibriumCheck CalculateEquilibrium(List<StructuralNodeResult> nodeResults)
    {
        double sumFx = 0, sumFy = 0, sumFz = 0, scale = 0;
        for (int i = 0; i < _model.Nodes.Count; i++)
        {
            int idx = i * 6;
            var result = nodeResults[i];
            sumFx += _originalF[idx] + result.ReactionForce.X;
            sumFy += _originalF[idx + 1] + result.ReactionForce.Y;
            sumFz += _originalF[idx + 2] + result.ReactionForce.Z;
            scale += Math.Abs(_originalF[idx]) + Math.Abs(_originalF[idx + 1]) + Math.Abs(_originalF[idx + 2]) +
                result.ReactionForce.Magnitude;
        }

        return new EquilibriumCheck(sumFx, sumFy, sumFz, Math.Max(1e-6, scale * 1e-9));
    }

    private SolverDiagnostics BuildDiagnostics(EquilibriumCheck equilibrium, IReadOnlyList<StructuralNodeResult> nodeResults)
    {
        int totalDof = _model.Nodes.Count * 6;
        int constrainedDof = _model.Nodes.Sum(n =>
            (n.ConstraintX ? 1 : 0) +
            (n.ConstraintY ? 1 : 0) +
            (n.ConstraintZ ? 1 : 0) +
            (n.ConstraintRX ? 1 : 0) +
            (n.ConstraintRY ? 1 : 0) +
            (n.ConstraintRZ ? 1 : 0));
        double density = totalDof == 0 ? 0 : (double)_lastNonZeroStiffnessEntries / (totalDof * totalDof);
        double applied = _originalF.Sum(Math.Abs);
        double reactions = nodeResults.Sum(n => n.ReactionForce.Magnitude + n.ReactionMoment.Magnitude);

        return new SolverDiagnostics
        {
            TotalDof = totalDof,
            ConstrainedDof = constrainedDof,
            ElementCount = _model.Elements.Count,
            SolverName = _linearSolver.Name,
            DenseSolverWarning = totalDof > DenseSolverWarningDof,
            MatrixDensity = density,
            AppliedLoadMagnitude = applied,
            ReactionMagnitude = reactions,
            EquilibriumResidualMagnitude = equilibrium.ResidualMagnitude,
            Notes = totalDof > DenseSolverWarningDof
                ? "Dense solver path is active; use sparse solver for larger production models."
                : "Dense solver path is active."
        };
    }

    private int[] GetElementDofMap(StructuralElement element)
    {
        int s = DofBase(element.StartNodeId);
        int e = DofBase(element.EndNodeId);
        return new[] { s, s + 1, s + 2, s + 3, s + 4, s + 5, e, e + 1, e + 2, e + 3, e + 4, e + 5 };
    }

    private int DofBase(int nodeId) => _nodeIndex[nodeId] * 6;

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double[] Multiply(double[,] matrix, double[] vector)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var result = new double[rows];
        for (int i = 0; i < rows; i++)
        {
            double sum = 0;
            for (int j = 0; j < cols; j++)
                sum += matrix[i, j] * vector[j];
            result[i] = sum;
        }
        return result;
    }

    private static int CountNonZero(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        int count = 0;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (Math.Abs(matrix[i, j]) > 1e-18)
                    count++;
            }
        }
        return count;
    }

}
