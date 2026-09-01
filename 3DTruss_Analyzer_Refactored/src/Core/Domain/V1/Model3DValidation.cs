namespace TrussAnalyzer.Core.Domain.V1;

public enum ValidationSeverity
{
    Information,
    Warning,
    Error
}

public enum ValidationCode
{
    DuplicateId,
    DuplicateLabel,
    EmptyLabel,
    MissingReference,
    ZeroLength,
    InvalidMaterial,
    InvalidSection,
    InvalidRelease,
    InvalidLocalAxis,
    InvalidSpring,
    InvalidConstraint,
    CyclicConstraint,
    InvalidLoad,
    UnsupportedAnalysisBehavior
}

public sealed record ModelValidationIssue(
    ValidationSeverity Severity,
    ValidationCode Code,
    Guid? ObjectId,
    string ObjectType,
    string Message);

public sealed class Model3DValidator
{
    public IReadOnlyList<ModelValidationIssue> Validate(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<ModelValidationIssue>();
        var model = document.Model;
        var allObjects = EnumeratePersistentObjects(document).ToList();

        foreach (var duplicate in allObjects.GroupBy(x => x.Object.Id).Where(g => g.Key == Guid.Empty || g.Count() > 1))
        {
            string message = duplicate.Key == Guid.Empty
                ? "Persistent object ID must not be empty."
                : $"Persistent object ID {duplicate.Key} is used more than once.";
            foreach (var item in duplicate)
                Add(issues, ValidationSeverity.Error, ValidationCode.DuplicateId, item.Object, item.Type, message);
        }

        foreach (var typeGroup in allObjects.GroupBy(x => x.Type, StringComparer.Ordinal))
        {
            foreach (var item in typeGroup.Where(x => string.IsNullOrWhiteSpace(x.Object.Label)))
                Add(issues, ValidationSeverity.Warning, ValidationCode.EmptyLabel, item.Object, item.Type, "Label is empty; labels are editable display names, not references.");

            foreach (var duplicate in typeGroup
                         .Where(x => !string.IsNullOrWhiteSpace(x.Object.Label))
                         .GroupBy(x => x.Object.Label.Trim(), StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1))
            {
                foreach (var item in duplicate)
                    Add(issues, ValidationSeverity.Warning, ValidationCode.DuplicateLabel, item.Object, item.Type,
                        $"Label '{duplicate.Key}' is duplicated within {typeGroup.Key}; references remain stable because they use IDs.");
            }
        }

        var nodes = model.Nodes.ToDictionarySafely(n => n.Id);
        var materials = model.Materials.ToDictionarySafely(x => x.Id);
        var sections = model.Sections.ToDictionarySafely(x => x.Id);
        var supports = model.Supports.ToDictionarySafely(x => x.Id);
        var springs = model.Springs.ToDictionarySafely(x => x.Id);
        var movements = model.PrescribedMovements.ToDictionarySafely(x => x.Id);
        var patterns = document.LoadDefinitions.LoadPatterns.ToDictionarySafely(x => x.Id);
        var lines = model.LineObjects.ToDictionarySafely(x => x.Id);

        foreach (var node in model.Nodes)
        {
            if (node.SupportId is { } supportId && !supports.ContainsKey(supportId))
                Missing(issues, node, nameof(Node3D), nameof(node.SupportId), supportId);
            foreach (var springId in node.SpringIds.Where(id => !springs.ContainsKey(id)))
                Missing(issues, node, nameof(Node3D), nameof(node.SpringIds), springId);
            foreach (var movementId in node.PrescribedMovementIds.Where(id => !movements.ContainsKey(id)))
                Missing(issues, node, nameof(Node3D), nameof(node.PrescribedMovementIds), movementId);
            foreach (var movementId in node.PrescribedMovementIds.Where(movements.ContainsKey))
            {
                var movement = movements[movementId];
                if (node.SupportId is not { } referencedSupportId || !supports.TryGetValue(referencedSupportId, out var support) ||
                    HasMovementOnFreeDof(movement.Movement, support.Restrained))
                {
                    Add(issues, ValidationSeverity.Error, ValidationCode.InvalidConstraint, node, nameof(Node3D),
                        $"Prescribed movement {movementId} requires every non-zero component to match a restrained support DOF.");
                }
            }
        }

        foreach (var spring in model.Springs)
        {
            if (Components(spring.Stiffness).Any(value => !double.IsFinite(value) || value < 0))
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidSpring, spring, nameof(SpringDefinition),
                    "Spring stiffness must be finite and non-negative; translation uses N/m and rotation uses N-m/rad.");
        }

        foreach (var movement in model.PrescribedMovements)
        {
            if (!patterns.ContainsKey(movement.LoadPatternId))
                Missing(issues, movement, nameof(PrescribedMovementDefinition), nameof(movement.LoadPatternId), movement.LoadPatternId);
            if (Components(movement.Movement).Any(value => !double.IsFinite(value)))
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidConstraint, movement, nameof(PrescribedMovementDefinition),
                    "Prescribed movement components must be finite SI values.");
        }

        foreach (var material in model.Materials)
        {
            if (!double.IsFinite(material.YoungsModulus) || material.YoungsModulus <= 0 ||
                !double.IsFinite(material.ShearModulus) || material.ShearModulus <= 0 ||
                !double.IsFinite(material.Density) || material.Density < 0 ||
                !double.IsFinite(material.PoissonsRatio) || material.PoissonsRatio <= -1 || material.PoissonsRatio >= 0.5)
            {
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidMaterial, material, nameof(Material3D),
                    "Material requires positive finite E and G, non-negative density, and -1 < Poisson ratio < 0.5.");
            }
        }

        foreach (var section in model.Sections)
        {
            if (new[] { section.Area, section.Iy, section.Iz, section.TorsionalConstant }.Any(value => !double.IsFinite(value) || value <= 0) ||
                new[] { section.ShearAreaY, section.ShearAreaZ }.Any(value => !double.IsFinite(value) || value < 0) ||
                section.DisplayDimensions.Values.Any(value => !double.IsFinite(value) || value <= 0))
            {
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidSection, section, nameof(Section3D),
                    "Section analysis properties must be positive and finite; shear areas may be zero, and display dimensions must be positive.");
            }
        }

        foreach (var line in model.LineObjects)
        {
            if (!nodes.TryGetValue(line.StartNodeId, out var start))
                Missing(issues, line, line.GetType().Name, nameof(line.StartNodeId), line.StartNodeId);
            if (!nodes.TryGetValue(line.EndNodeId, out var end))
                Missing(issues, line, line.GetType().Name, nameof(line.EndNodeId), line.EndNodeId);
            if (!materials.ContainsKey(line.MaterialId))
                Missing(issues, line, line.GetType().Name, nameof(line.MaterialId), line.MaterialId);
            if (!sections.ContainsKey(line.SectionId))
                Missing(issues, line, line.GetType().Name, nameof(line.SectionId), line.SectionId);

            if (start is not null && end is not null)
            {
                if (start.Position.VectorTo(end.Position).Magnitude <= 1e-10)
                {
                    Add(issues, ValidationSeverity.Error, ValidationCode.ZeroLength, line, line.GetType().Name,
                        "Line object start and end nodes are coincident.");
                }
                else
                {
                    try { _ = LocalAxisBasis.Create(start.Position, end.Position, line.LocalAxis); }
                    catch (InvalidOperationException ex)
                    {
                        Add(issues, ValidationSeverity.Error, ValidationCode.InvalidLocalAxis, line, line.GetType().Name, ex.Message);
                    }
                }
            }

            if (!double.IsFinite(line.StartRigidOffset) || !double.IsFinite(line.EndRigidOffset) ||
                line.StartRigidOffset < 0 || line.EndRigidOffset < 0)
            {
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidSection, line, line.GetType().Name,
                    "Rigid offsets must be finite, non-negative SI lengths.");
            }

            bool invalidRelease = line.StartRelease.Released.All || line.EndRelease.Released.All ||
                                  InvalidPartialFixity(line.StartRelease) || InvalidPartialFixity(line.EndRelease) ||
                                  line is Truss3D && (line.StartRelease.Released.Any || line.EndRelease.Released.Any ||
                                                       line.StartRelease.PartialFixity is not null || line.EndRelease.PartialFixity is not null);
            if (invalidRelease)
            {
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidRelease, line, line.GetType().Name,
                    "A frame end cannot release all six DOFs, partial-fixity values must be finite/non-negative, and Truss3D releases are implicit rather than user-assigned.");
            }
        }

        foreach (var area in model.AreaObjects)
        {
            if (area.BoundaryNodeIds.Count is < 3 or > 4 || area.BoundaryNodeIds.Distinct().Count() != area.BoundaryNodeIds.Count)
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidConstraint, area, nameof(AreaObject3D),
                    "AreaObject3D requires three or four distinct boundary nodes in V1.");
            foreach (var nodeId in area.BoundaryNodeIds.Where(id => !nodes.ContainsKey(id)))
                Missing(issues, area, nameof(AreaObject3D), nameof(area.BoundaryNodeIds), nodeId);
            if (area.MaterialId is { } materialId && !materials.ContainsKey(materialId))
                Missing(issues, area, nameof(AreaObject3D), nameof(area.MaterialId), materialId);
            if (area.SectionId is { } sectionId && !sections.ContainsKey(sectionId))
                Missing(issues, area, nameof(AreaObject3D), nameof(area.SectionId), sectionId);
            Add(issues, ValidationSeverity.Warning, ValidationCode.UnsupportedAnalysisBehavior, area, nameof(AreaObject3D),
                "AreaObject3D is storage/validation only in Model3D V1 and must fail preflight before line-analysis results are presented.");
        }

        var dependencyEdges = new List<(Guid Master, Guid Slave, IPersistentModelObject Owner, string Type)>();
        foreach (var link in model.RigidLinks)
        {
            ValidateNodePair(issues, nodes, link, nameof(RigidLink3D), link.MasterNodeId, link.SlaveNodeId);
            if (!link.CoupledDofs.Any)
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidConstraint, link, nameof(RigidLink3D), "Rigid link must couple at least one DOF.");
            dependencyEdges.Add((link.MasterNodeId, link.SlaveNodeId, link, nameof(RigidLink3D)));
        }

        foreach (var constraint in model.Constraints)
        {
            if (!nodes.ContainsKey(constraint.MasterNodeId))
                Missing(issues, constraint, nameof(MasterSlaveConstraint3D), nameof(constraint.MasterNodeId), constraint.MasterNodeId);
            if (!constraint.CoupledDofs.Any || constraint.SlaveNodeIds.Count == 0 || constraint.SlaveNodeIds.Distinct().Count() != constraint.SlaveNodeIds.Count)
                Add(issues, ValidationSeverity.Error, ValidationCode.InvalidConstraint, constraint, nameof(MasterSlaveConstraint3D),
                    "Constraint requires at least one coupled DOF and one unique slave node.");
            foreach (var slaveId in constraint.SlaveNodeIds)
            {
                if (!nodes.ContainsKey(slaveId))
                    Missing(issues, constraint, nameof(MasterSlaveConstraint3D), nameof(constraint.SlaveNodeIds), slaveId);
                if (slaveId == constraint.MasterNodeId)
                    Add(issues, ValidationSeverity.Error, ValidationCode.InvalidConstraint, constraint, nameof(MasterSlaveConstraint3D), "A node cannot be its own master and slave.");
                dependencyEdges.Add((constraint.MasterNodeId, slaveId, constraint, nameof(MasterSlaveConstraint3D)));
            }
        }

        DetectCycles(issues, dependencyEdges);

        foreach (var assignment in document.LoadDefinitions.Assignments)
        {
            if (!patterns.ContainsKey(assignment.LoadPatternId))
                Missing(issues, assignment, assignment.GetType().Name, nameof(assignment.LoadPatternId), assignment.LoadPatternId);
            switch (assignment)
            {
                case NodalLoadAssignment3D nodal when !nodes.ContainsKey(nodal.NodeId):
                    Missing(issues, nodal, nameof(NodalLoadAssignment3D), nameof(nodal.NodeId), nodal.NodeId);
                    break;
                case LineLoadAssignment3D lineLoad:
                    if (!lines.ContainsKey(lineLoad.LineObjectId))
                        Missing(issues, lineLoad, nameof(LineLoadAssignment3D), nameof(lineLoad.LineObjectId), lineLoad.LineObjectId);
                    if (!double.IsFinite(lineLoad.StartRelativePosition) || !double.IsFinite(lineLoad.EndRelativePosition) ||
                        lineLoad.StartRelativePosition < 0 || lineLoad.EndRelativePosition > 1 || lineLoad.StartRelativePosition >= lineLoad.EndRelativePosition)
                        Add(issues, ValidationSeverity.Error, ValidationCode.InvalidLoad, lineLoad, nameof(LineLoadAssignment3D),
                            "Line-load relative positions must satisfy 0 <= start < end <= 1.");
                    break;
            }
        }

        foreach (var combination in document.LoadDefinitions.LoadCombinations)
            foreach (var patternId in combination.LoadPatternFactors.Keys.Where(id => !patterns.ContainsKey(id)))
                Missing(issues, combination, nameof(LoadCombination3D), nameof(combination.LoadPatternFactors), patternId);
        foreach (var patternId in document.LoadDefinitions.MassSource.LoadPatternFactors.Keys.Where(id => !patterns.ContainsKey(id)))
            issues.Add(new ModelValidationIssue(ValidationSeverity.Error, ValidationCode.MissingReference, null, nameof(MassSource3D),
                $"MassSource3D references missing load pattern {patternId}."));
        foreach (var analysisCase in document.AnalysisDefinitions.Cases.Where(c => !patterns.ContainsKey(c.LoadPatternId)))
            Missing(issues, analysisCase, nameof(AnalysisCase3D), nameof(analysisCase.LoadPatternId), analysisCase.LoadPatternId);

        var combinations = document.LoadDefinitions.LoadCombinations.ToDictionarySafely(x => x.Id);
        foreach (var selection in document.AnalysisDefinitions.ResultSelections)
        {
            foreach (var sourceId in selection.SourceIds)
            {
                bool found = selection.Kind switch
                {
                    ResultSelectionKind.LoadPattern => patterns.ContainsKey(sourceId),
                    ResultSelectionKind.LoadCombination => combinations.ContainsKey(sourceId),
                    ResultSelectionKind.Envelope => patterns.ContainsKey(sourceId) || combinations.ContainsKey(sourceId),
                    _ => false
                };
                if (!found)
                    Missing(issues, selection, nameof(ResultSelection3D), nameof(selection.SourceIds), sourceId);
            }
        }

        return issues;
    }

    private static IEnumerable<(IPersistentModelObject Object, string Type)> EnumeratePersistentObjects(ProjectDocument document)
    {
        var model = document.Model;
        foreach (var item in model.Nodes) yield return (item, nameof(Node3D));
        foreach (var item in model.LineObjects) yield return (item, item.GetType().Name);
        foreach (var item in model.AreaObjects) yield return (item, nameof(AreaObject3D));
        foreach (var item in model.Materials) yield return (item, nameof(Material3D));
        foreach (var item in model.Sections) yield return (item, nameof(Section3D));
        foreach (var item in model.Supports) yield return (item, nameof(SupportDefinition));
        foreach (var item in model.Springs) yield return (item, nameof(SpringDefinition));
        foreach (var item in model.PrescribedMovements) yield return (item, nameof(PrescribedMovementDefinition));
        foreach (var item in model.RigidLinks) yield return (item, nameof(RigidLink3D));
        foreach (var item in model.Constraints) yield return (item, nameof(MasterSlaveConstraint3D));
        foreach (var item in model.Levels) yield return (item, nameof(Level3D));
        foreach (var item in model.Grids) yield return (item, nameof(GridLine3D));
        foreach (var item in model.Groups) yield return (item, nameof(ModelGroup3D));
        foreach (var item in document.LoadDefinitions.LoadPatterns) yield return (item, nameof(LoadPattern3D));
        foreach (var item in document.LoadDefinitions.Assignments) yield return (item, item.GetType().Name);
        foreach (var item in document.LoadDefinitions.LoadCombinations) yield return (item, nameof(LoadCombination3D));
        foreach (var item in document.AnalysisDefinitions.Cases) yield return (item, nameof(AnalysisCase3D));
        foreach (var item in document.AnalysisDefinitions.ResultSelections) yield return (item, nameof(ResultSelection3D));
    }

    private static IEnumerable<double> Components(DofValues value) =>
        new[] { value.UX, value.UY, value.UZ, value.RX, value.RY, value.RZ };

    private static bool InvalidPartialFixity(EndRelease6 release) =>
        release.PartialFixity is { } values && Components(values).Any(value => !double.IsFinite(value) || value < 0);

    private static bool HasMovementOnFreeDof(DofValues movement, DofRestraints restrained) =>
        movement.UX != 0 && !restrained.UX || movement.UY != 0 && !restrained.UY || movement.UZ != 0 && !restrained.UZ ||
        movement.RX != 0 && !restrained.RX || movement.RY != 0 && !restrained.RY || movement.RZ != 0 && !restrained.RZ;

    private static void ValidateNodePair(List<ModelValidationIssue> issues, Dictionary<Guid, Node3D> nodes,
        IPersistentModelObject owner, string type, Guid masterId, Guid slaveId)
    {
        if (!nodes.ContainsKey(masterId)) Missing(issues, owner, type, "MasterNodeId", masterId);
        if (!nodes.ContainsKey(slaveId)) Missing(issues, owner, type, "SlaveNodeId", slaveId);
        if (masterId == slaveId)
            Add(issues, ValidationSeverity.Error, ValidationCode.InvalidConstraint, owner, type, "A node cannot be its own master and slave.");
    }

    private static void DetectCycles(List<ModelValidationIssue> issues,
        List<(Guid Master, Guid Slave, IPersistentModelObject Owner, string Type)> edges)
    {
        var graph = edges.GroupBy(e => e.Master).ToDictionary(g => g.Key, g => g.ToList());
        var visited = new HashSet<Guid>();
        var active = new HashSet<Guid>();
        var reported = new HashSet<Guid>();

        bool Visit(Guid node)
        {
            if (active.Contains(node)) return true;
            if (!visited.Add(node)) return false;
            active.Add(node);
            foreach (var edge in graph.GetValueOrDefault(node) ?? Enumerable.Empty<(Guid Master, Guid Slave, IPersistentModelObject Owner, string Type)>())
            {
                if (Visit(edge.Slave) && reported.Add(edge.Owner.Id))
                    Add(issues, ValidationSeverity.Error, ValidationCode.CyclicConstraint, edge.Owner, edge.Type,
                        "Rigid-link/master-slave dependency contains a cycle.");
            }
            active.Remove(node);
            return false;
        }

        foreach (var node in graph.Keys) Visit(node);
    }

    private static void Missing(List<ModelValidationIssue> issues, IPersistentModelObject owner, string type, string property, Guid missingId) =>
        Add(issues, ValidationSeverity.Error, ValidationCode.MissingReference, owner, type, $"{property} references missing ID {missingId}.");

    private static void Add(List<ModelValidationIssue> issues, ValidationSeverity severity, ValidationCode code,
        IPersistentModelObject owner, string type, string message) =>
        issues.Add(new ModelValidationIssue(severity, code, owner.Id, type, message));
}

internal static class Model3DValidationExtensions
{
    public static Dictionary<Guid, T> ToDictionarySafely<T>(this IEnumerable<T> source, Func<T, Guid> idSelector) =>
        source.GroupBy(idSelector).ToDictionary(group => group.Key, group => group.First());
}
