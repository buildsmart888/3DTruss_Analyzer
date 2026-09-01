namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public sealed class ElementForceRecoveryService
{
    private readonly StructuralModel _model;
    private readonly DofIndexer _dofIndexer;
    private readonly FrameElementStiffnessProvider _stiffnessProvider;
    private readonly Dictionary<int, Node> _nodes = new();
    private readonly Dictionary<int, Material> _materials = new();
    private readonly Dictionary<int, Section> _sections = new();

    public ElementForceRecoveryService(
        StructuralModel model,
        DofIndexer dofIndexer,
        FrameElementStiffnessProvider stiffnessProvider)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _dofIndexer = dofIndexer ?? throw new ArgumentNullException(nameof(dofIndexer));
        _stiffnessProvider = stiffnessProvider ?? throw new ArgumentNullException(nameof(stiffnessProvider));

        foreach (var node in model.Nodes)
            _nodes[node.Id] = node;
        foreach (var material in model.Materials)
            _materials[material.Id] = material;
        foreach (var section in model.Sections)
            _sections[section.Id] = section;
    }

    public ElementForceResult Recover(
        StructuralElement element,
        double[] globalDisplacements,
        IReadOnlyDictionary<int, double[]> equivalentElementLoadsLocal,
        IReadOnlyDictionary<int, IReadOnlyList<MemberDiagramLoad>>? memberDiagramLoadsLocal = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(globalDisplacements);
        ArgumentNullException.ThrowIfNull(equivalentElementLoadsLocal);

        var start = _nodes[element.StartNodeId];
        var end = _nodes[element.EndNodeId];
        var material = _materials[element.MaterialId];
        var section = _sections[element.SectionId];
        var geometry = FrameElementGeometryResolver.Resolve(element, start, end);
        double length = geometry.Length;
        var localStiffness = _stiffnessProvider.BuildLocalStiffness(element, material, section, length);
        int[] dofMap = _dofIndexer.GetElementDofMap(element);
        var elementGlobalDisplacements = new double[12];
        for (int dof = 0; dof < elementGlobalDisplacements.Length; dof++)
            elementGlobalDisplacements[dof] = globalDisplacements[dofMap[dof]];

        var localDisplacements = Multiply(geometry.AnalysisTransformation, elementGlobalDisplacements);
        var localForces = Multiply(localStiffness, localDisplacements);
        if (equivalentElementLoadsLocal.TryGetValue(element.Id, out var equivalentLoad))
        {
            for (int dof = 0; dof < localForces.Length; dof++)
                localForces[dof] -= equivalentLoad[dof];
        }

        IReadOnlyList<MemberDiagramLoad>? diagramLoads = null;
        if (memberDiagramLoadsLocal != null && memberDiagramLoadsLocal.TryGetValue(element.Id, out var memberLoads))
            diagramLoads = memberLoads;

        double axial = localForces[6];
        double stress = section.Area > 0 ? axial / section.Area : 0;
        return new ElementForceResult
        {
            ElementId = element.Id,
            AxialForce = axial,
            ShearY = Math.Max(Math.Abs(localForces[1]), Math.Abs(localForces[7])),
            ShearZ = Math.Max(Math.Abs(localForces[2]), Math.Abs(localForces[8])),
            Torsion = Math.Max(Math.Abs(localForces[3]), Math.Abs(localForces[9])),
            MomentY = Math.Max(Math.Abs(localForces[4]), Math.Abs(localForces[10])),
            MomentZ = Math.Max(Math.Abs(localForces[5]), Math.Abs(localForces[11])),
            Stress = stress,
            LocalEndForces = localForces,
            StartEndForces = new ElementEndForceResult
            {
                Force = new Vector3D(localForces[0], localForces[1], localForces[2]),
                Moment = new Vector3D(localForces[3], localForces[4], localForces[5])
            },
            EndEndForces = new ElementEndForceResult
            {
                Force = new Vector3D(localForces[6], localForces[7], localForces[8]),
                Moment = new Vector3D(localForces[9], localForces[10], localForces[11])
            },
            StationResults = BuildStationResults(element.Id, length, localForces, _model.ResultStationCount, diagramLoads)
        };
    }

    private static List<ElementStationResult> BuildStationResults(
        int elementId,
        double length,
        double[] localForces,
        int stationCount,
        IReadOnlyList<MemberDiagramLoad>? diagramLoads)
    {
        int count = stationCount >= 2 ? stationCount : StructuralModel.DefaultResultStationCount;
        if (diagramLoads == null || diagramLoads.Count == 0)
            return BuildInterpolatedStationResults(elementId, localForces, count);

        return BuildLoadAwareStationResults(elementId, length, localForces, count, diagramLoads);
    }

    private static List<ElementStationResult> BuildInterpolatedStationResults(int elementId, double[] localForces, int count)
    {
        var stations = new List<ElementStationResult>();
        for (int index = 0; index < count; index++)
        {
            double relativePosition = count == 1 ? 0 : (double)index / (count - 1);
            stations.Add(new ElementStationResult
            {
                ElementId = elementId,
                RelativePosition = relativePosition,
                AxialForce = Lerp(localForces[0], -localForces[6], relativePosition),
                ShearY = Lerp(localForces[1], -localForces[7], relativePosition),
                ShearZ = Lerp(localForces[2], -localForces[8], relativePosition),
                Torsion = Lerp(localForces[3], -localForces[9], relativePosition),
                MomentY = Lerp(localForces[4], -localForces[10], relativePosition),
                MomentZ = Lerp(localForces[5], -localForces[11], relativePosition)
            });
        }

        return stations;
    }

    private static List<ElementStationResult> BuildLoadAwareStationResults(
        int elementId,
        double length,
        double[] localForces,
        int stationCount,
        IReadOnlyList<MemberDiagramLoad> diagramLoads)
    {
        bool hasAxialLoad = HasForceComponent(diagramLoads, component: 0);
        bool hasShearYLoad = HasForceComponent(diagramLoads, component: 1);
        bool hasShearZLoad = HasForceComponent(diagramLoads, component: 2);
        bool hasTorsionalPointMoment = HasPointMomentComponent(diagramLoads, component: 0);
        bool hasMomentYPointLoad = HasPointMomentComponent(diagramLoads, component: 1);
        bool hasMomentZPointLoad = HasPointMomentComponent(diagramLoads, component: 2);
        var stations = new List<ElementStationResult>();

        foreach (var sample in BuildStationSamples(stationCount, diagramLoads))
        {
            double position = sample.RelativePosition;
            if (position >= 1 - 1e-12)
            {
                stations.Add(CreateStation(
                    elementId,
                    position,
                    sample.DiagramSide,
                    -localForces[6],
                    -localForces[7],
                    -localForces[8],
                    -localForces[9],
                    -localForces[10],
                    -localForces[11]));
                continue;
            }

            bool includePointLoadsAtStation = sample.DiagramSide != DiagramStationSide.Left;
            double x = position * length;
            double axial = hasAxialLoad
                ? localForces[0] + CalculateForceIncrement(diagramLoads, length, x, component: 0, includePointLoadsAtStation)
                : Lerp(localForces[0], -localForces[6], position);
            double shearY = hasShearYLoad
                ? localForces[1] + CalculateForceIncrement(diagramLoads, length, x, component: 1, includePointLoadsAtStation)
                : Lerp(localForces[1], -localForces[7], position);
            double torsion = hasTorsionalPointMoment
                ? localForces[3] + CalculatePointMomentIncrement(diagramLoads, position, component: 0, includePointLoadsAtStation)
                : Lerp(localForces[3], -localForces[9], position);
            double shearZ = hasShearZLoad
                ? localForces[2] + CalculateForceIncrement(diagramLoads, length, x, component: 2, includePointLoadsAtStation)
                : Lerp(localForces[2], -localForces[8], position);
            double momentY = hasShearZLoad || hasMomentYPointLoad
                ? localForces[4] + CalculateShearIntegral(diagramLoads, length, x, localForces[2], component: 2) +
                    CalculatePointMomentIncrement(diagramLoads, position, component: 1, includePointLoadsAtStation)
                : Lerp(localForces[4], -localForces[10], position);
            double momentZ = hasShearYLoad || hasMomentZPointLoad
                ? localForces[5] - CalculateShearIntegral(diagramLoads, length, x, localForces[1], component: 1) +
                    CalculatePointMomentIncrement(diagramLoads, position, component: 2, includePointLoadsAtStation)
                : Lerp(localForces[5], -localForces[11], position);

            stations.Add(CreateStation(
                elementId,
                position,
                sample.DiagramSide,
                axial,
                shearY,
                shearZ,
                torsion,
                momentY,
                momentZ));
        }

        return stations;
    }

    private static IEnumerable<StationSample> BuildStationSamples(int stationCount, IReadOnlyList<MemberDiagramLoad> diagramLoads)
    {
        var positions = new List<double>();
        for (int index = 0; index < stationCount; index++)
            positions.Add((double)index / (stationCount - 1));

        foreach (var pointLoad in diagramLoads.OfType<MemberPointDiagramLoad>())
        {
            if (pointLoad.RelativePosition > 1e-12 && pointLoad.RelativePosition < 1 - 1e-12 &&
                !positions.Any(position => Math.Abs(position - pointLoad.RelativePosition) < 1e-12))
            {
                positions.Add(pointLoad.RelativePosition);
            }
        }

        positions.Sort();
        foreach (double position in positions)
        {
            bool hasInternalPointLoad = diagramLoads
                .OfType<MemberPointDiagramLoad>()
                .Any(load =>
                    Math.Abs(load.RelativePosition - position) < 1e-12 &&
                    position > 1e-12 &&
                    position < 1 - 1e-12 &&
                    (load.Force.Magnitude > 1e-12 || load.Moment.Magnitude > 1e-12));

            if (hasInternalPointLoad)
            {
                yield return new StationSample(position, DiagramStationSide.Left);
                yield return new StationSample(position, DiagramStationSide.Right);
            }
            else
            {
                yield return new StationSample(position, DiagramStationSide.Continuous);
            }
        }
    }

    private static bool HasForceComponent(IReadOnlyList<MemberDiagramLoad> loads, int component)
    {
        return loads.OfType<MemberPointDiagramLoad>().Any(load => Math.Abs(GetComponent(load.Force, component)) > 1e-12) ||
            loads.OfType<MemberDistributedDiagramLoad>().Any(load => Math.Abs(GetComponent(load.ForcePerLength, component)) > 1e-12);
    }

    private static bool HasPointMomentComponent(IReadOnlyList<MemberDiagramLoad> loads, int component)
    {
        return loads.OfType<MemberPointDiagramLoad>().Any(load => Math.Abs(GetComponent(load.Moment, component)) > 1e-12);
    }

    private static double CalculateForceIncrement(
        IReadOnlyList<MemberDiagramLoad> loads,
        double length,
        double x,
        int component,
        bool includePointLoadsAtStation)
    {
        double increment = 0;
        foreach (var pointLoad in loads.OfType<MemberPointDiagramLoad>())
        {
            double loadPosition = pointLoad.RelativePosition * length;
            if (x > loadPosition + 1e-12 || (includePointLoadsAtStation && Math.Abs(x - loadPosition) <= 1e-12))
                increment += GetComponent(pointLoad.Force, component);
        }

        foreach (var distributedLoad in loads.OfType<MemberDistributedDiagramLoad>())
        {
            double start = distributedLoad.StartRelativePosition * length;
            double end = distributedLoad.EndRelativePosition * length;
            double coveredLength = Math.Clamp(x - start, 0, end - start);
            increment += GetComponent(distributedLoad.ForcePerLength, component) * coveredLength;
        }

        return increment;
    }

    private static double CalculatePointMomentIncrement(
        IReadOnlyList<MemberDiagramLoad> loads,
        double relativePosition,
        int component,
        bool includePointLoadsAtStation)
    {
        double increment = 0;
        foreach (var pointLoad in loads.OfType<MemberPointDiagramLoad>())
        {
            if (relativePosition > pointLoad.RelativePosition + 1e-12 ||
                (includePointLoadsAtStation && Math.Abs(relativePosition - pointLoad.RelativePosition) <= 1e-12))
            {
                increment += GetComponent(pointLoad.Moment, component);
            }
        }

        return increment;
    }

    private static double CalculateShearIntegral(
        IReadOnlyList<MemberDiagramLoad> loads,
        double length,
        double x,
        double startShear,
        int component)
    {
        double integral = startShear * x;
        foreach (var pointLoad in loads.OfType<MemberPointDiagramLoad>())
        {
            double loadPosition = pointLoad.RelativePosition * length;
            integral += GetComponent(pointLoad.Force, component) * Math.Max(0, x - loadPosition);
        }

        foreach (var distributedLoad in loads.OfType<MemberDistributedDiagramLoad>())
        {
            double start = distributedLoad.StartRelativePosition * length;
            double end = distributedLoad.EndRelativePosition * length;
            if (x <= start)
                continue;

            double loadedLength = end - start;
            double loadSpan = Math.Min(x, end) - start;
            double forcePerLength = GetComponent(distributedLoad.ForcePerLength, component);
            integral += forcePerLength * 0.5 * loadSpan * loadSpan;
            if (x > end)
                integral += forcePerLength * loadedLength * (x - end);
        }

        return integral;
    }

    private static ElementStationResult CreateStation(
        int elementId,
        double relativePosition,
        DiagramStationSide diagramSide,
        double axialForce,
        double shearY,
        double shearZ,
        double torsion,
        double momentY,
        double momentZ) => new()
    {
        ElementId = elementId,
        RelativePosition = relativePosition,
        DiagramSide = diagramSide,
        AxialForce = axialForce,
        ShearY = shearY,
        ShearZ = shearZ,
        Torsion = torsion,
        MomentY = momentY,
        MomentZ = momentZ
    };

    private static double GetComponent(Vector3D vector, int component) => component switch
    {
        0 => vector.X,
        1 => vector.Y,
        2 => vector.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(component))
    };

    private readonly record struct StationSample(double RelativePosition, DiagramStationSide DiagramSide);

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

    private static double Lerp(double start, double end, double position) => start + (end - start) * position;
}
