namespace TrussAnalyzer.Core.Domain.V1.Adapters;

using System.Globalization;
using TrussAnalyzer.Core.Models;

public enum AdapterDiagnosticSeverity { Information, Warning, Error }

public sealed record AdapterDiagnostic(AdapterDiagnosticSeverity Severity, string Code, string Message);

public sealed record StructuralModelToProjectDocumentResult(
    ProjectDocument Document,
    IReadOnlyDictionary<int, Guid> NodeIds,
    IReadOnlyDictionary<int, Guid> ElementIds,
    IReadOnlyList<AdapterDiagnostic> Diagnostics);

public sealed record ProjectDocumentToStructuralModelResult(
    StructuralModel Model,
    IReadOnlyDictionary<Guid, int> NodeIds,
    IReadOnlyDictionary<Guid, int> LineObjectIds,
    IReadOnlyList<AdapterDiagnostic> Diagnostics);

/// <summary>
/// Explicit compatibility boundary between the current integer-ID solver model and Model3D V1.
/// It never silently drops data: values without an equivalent target semantic generate diagnostics.
/// </summary>
public sealed class StructuralModelModel3DAdapter
{
    private const string SourceSystem = "StructuralModel";
    private const string DefaultLoadCaseId = "LEGACY-DEFAULT";
    private const string SettlementLoadCaseId = "LEGACY-SETTLEMENT";

    public StructuralModelToProjectDocumentResult ToProjectDocument(StructuralModel source, ProjectInfo? projectInfo = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<AdapterDiagnostic>();
        var document = new ProjectDocument
        {
            ProjectInfo = projectInfo ?? new ProjectInfo { Name = "Imported StructuralModel" },
            AuditMetadata = new AuditMetadata
            {
                DocumentId = GuidUtility.Create(BuildDocumentIdentity(source)),
                CreatedUtc = DateTimeOffset.UnixEpoch,
                ModifiedUtc = DateTimeOffset.UnixEpoch,
                CreatedByVersion = "StructuralModel adapter"
            }
        };
        var nodeIds = source.Nodes.ToDictionary(node => node.Id, node => StableId("node", node.Id));
        var materialIds = source.Materials.ToDictionary(material => material.Id, material => StableId("material", material.Id));
        var sectionIds = source.Sections.ToDictionary(section => section.Id, section => StableId("section", section.Id));
        var elementIds = source.Elements.ToDictionary(element => element.Id, element => StableId("line", element.Id));

        foreach (var material in source.Materials)
        {
            document.Model.Materials.Add(new Material3D
            {
                Id = materialIds[material.Id],
                Label = material.Name,
                Kind = MapMaterialKind(material.Type),
                YoungsModulus = material.YoungsModulus,
                ShearModulus = material.EffectiveShearModulus,
                PoissonsRatio = material.PoissonsRatio,
                Density = material.Density,
                Source = LegacySource("material", material.Id)
            });
            if (material.YieldStrength != 0 || material.UltimateStrength != 0 || material.ConcreteCompressiveStrength != 0)
                Warn(diagnostics, "SM2M3-MATERIAL-DESIGN", $"Material {material.Id} design strengths are not part of Model3D V1 material semantics and were not transferred.");
        }

        foreach (var section in source.Sections)
        {
            var dimensions = new Dictionary<string, double>(StringComparer.Ordinal);
            AddPositive(dimensions, "depth", section.Depth);
            AddPositive(dimensions, "width", section.Width);
            AddPositive(dimensions, "thickness", section.Thickness);
            AddPositive(dimensions, "diameter", section.Diameter);
            document.Model.Sections.Add(new Section3D
            {
                Id = sectionIds[section.Id],
                Label = section.Name,
                Shape = MapSectionShape(section.Type),
                Area = section.Area,
                Iy = section.Iy,
                Iz = section.Iz,
                TorsionalConstant = section.J,
                DisplayDimensions = dimensions,
                Source = LegacySource("section", section.Id)
            });
            if (section.RebarArea != 0 || section.EffectiveDepth != 0)
                Warn(diagnostics, "SM2M3-SECTION-DESIGN", $"Section {section.Id} reinforcement design properties are not part of Model3D V1 section semantics and were not transferred.");
        }

        var settlementPattern = AddLoadPatterns(source, document, diagnostics);
        foreach (var node in source.Nodes)
        {
            Guid? supportId = null;
            var restraints = ToDofRestraints(node);
            if (restraints.Any)
            {
                supportId = StableId("support", node.Id);
                document.Model.Supports.Add(new SupportDefinition { Id = supportId.Value, Label = $"Support N{node.Id}", Restrained = restraints });
            }

            var movements = new List<Guid>();
            var movement = new DofValues(
                node.PrescribedDisplacement.X, node.PrescribedDisplacement.Y, node.PrescribedDisplacement.Z,
                node.PrescribedRotation.X, node.PrescribedRotation.Y, node.PrescribedRotation.Z);
            if (HasValues(movement))
            {
                Guid movementId = StableId("movement", node.Id);
                document.Model.PrescribedMovements.Add(new PrescribedMovementDefinition
                {
                    Id = movementId,
                    Label = $"Prescribed movement N{node.Id}",
                    LoadPatternId = settlementPattern,
                    Movement = movement
                });
                movements.Add(movementId);
                Warn(diagnostics, "SM2M3-SETTLEMENT-SCOPE", $"Node {node.Id} prescribed movement is model-wide in StructuralModel; it is exported under {SettlementLoadCaseId} and requires review before case-specific use.");
            }

            document.Model.Nodes.Add(new Node3D
            {
                Id = nodeIds[node.Id],
                Label = $"N{node.Id}",
                Position = ToPoint(node.Position),
                SupportId = supportId,
                PrescribedMovementIds = movements,
                Source = LegacySource("node", node.Id)
            });
        }

        foreach (var element in source.Elements)
        {
            var localAxis = new LocalAxisReference
            {
                ReferenceVector = SelectReferenceAxis(source, element),
                RollRadians = element.RollAngleRadians
            };
            LineObject3D line = element.Type == ElementType.Truss
                ? new Truss3D
                {
                    Id = elementIds[element.Id], Label = $"E{element.Id}", StartNodeId = nodeIds[element.StartNodeId], EndNodeId = nodeIds[element.EndNodeId],
                    MaterialId = materialIds[element.MaterialId], SectionId = sectionIds[element.SectionId], LocalAxis = localAxis, Source = LegacySource("element", element.Id)
                }
                : new Frame3D
                {
                    Id = elementIds[element.Id], Label = $"E{element.Id}", StartNodeId = nodeIds[element.StartNodeId], EndNodeId = nodeIds[element.EndNodeId],
                    MaterialId = materialIds[element.MaterialId], SectionId = sectionIds[element.SectionId], LocalAxis = localAxis,
                    StartRigidOffset = element.StartRigidEndOffset, EndRigidOffset = element.EndRigidEndOffset,
                    StartInsertionOffsetLocal = ToVector(element.StartInsertionPointLocal), EndInsertionOffsetLocal = ToVector(element.EndInsertionPointLocal),
                    StartRelease = new EndRelease6 { Released = new DofRestraints(RY: element.Releases.StartMomentY, RZ: element.Releases.StartMomentZ) },
                    EndRelease = new EndRelease6 { Released = new DofRestraints(RY: element.Releases.EndMomentY, RZ: element.Releases.EndMomentZ) },
                    Source = LegacySource("element", element.Id)
                };
            document.Model.LineObjects.Add(line);
        }

        foreach (var area in source.AreaObjects)
            Warn(diagnostics, "SM2M3-AREA", $"Area object {area.Id} was not transferred: StructuralModel thickness/type/diaphragm semantics have no lossless Model3D V1 equivalent.");

        AddLoads(source, document, nodeIds, elementIds, diagnostics);
        AddCombinations(source, document, diagnostics);
        AddDefaultAppliedForces(source, document, nodeIds, diagnostics);

        return new StructuralModelToProjectDocumentResult(document, nodeIds, elementIds, diagnostics);
    }

    public ProjectDocumentToStructuralModelResult ToStructuralModel(ProjectDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<AdapterDiagnostic>();
        var errors = new Model3DValidator().Validate(source).Where(issue => issue.Severity == ValidationSeverity.Error).ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Model3D V1 validation failed before conversion: {string.Join("; ", errors.Select(error => error.Message))}");

        var nodeIds = LegacyIdMap.Create(source.Model.Nodes, node => node.Source, "node", diagnostics);
        var materialIds = LegacyIdMap.Create(source.Model.Materials, material => material.Source, "material", diagnostics);
        var sectionIds = LegacyIdMap.Create(source.Model.Sections, section => section.Source, "section", diagnostics);
        var lineIds = LegacyIdMap.Create(source.Model.LineObjects, line => line.Source, "element", diagnostics);
        var patternIds = LegacyIdMap.Create(source.LoadDefinitions.LoadPatterns, pattern => pattern.Source, "load pattern", diagnostics);
        var model = new StructuralModel();

        foreach (var material in source.Model.Materials)
        {
            model.Materials.Add(new Material
            {
                Id = materialIds[material.Id], Name = material.Label, Type = MapMaterialType(material.Kind),
                YoungsModulus = material.YoungsModulus, ShearModulus = material.ShearModulus,
                PoissonsRatio = material.PoissonsRatio, Density = material.Density
            });
        }
        foreach (var section in source.Model.Sections)
        {
            model.Sections.Add(new Section
            {
                Id = sectionIds[section.Id], Name = section.Label, Type = MapSectionType(section.Shape),
                Area = section.Area, Iy = section.Iy, Iz = section.Iz, J = section.TorsionalConstant,
                Depth = Dimension(section, "depth"), Width = Dimension(section, "width"), Thickness = Dimension(section, "thickness"), Diameter = Dimension(section, "diameter")
            });
            if (section.ShearAreaY != 0 || section.ShearAreaZ != 0)
                Warn(diagnostics, "M32SM-SHEAR-AREA", $"Section '{section.Label}' shear-area properties are not represented by StructuralModel and were not transferred.");
        }
        foreach (var node in source.Model.Nodes)
        {
            var legacy = new Node(nodeIds[node.Id], new Point3D(node.Position.X, node.Position.Y, node.Position.Z));
            if (node.SupportId is { } supportId && source.Model.Supports.SingleOrDefault(support => support.Id == supportId) is { } support)
                ApplyRestraints(legacy, support.Restrained);
            foreach (var movementId in node.PrescribedMovementIds)
            {
                var movement = source.Model.PrescribedMovements.Single(m => m.Id == movementId);
                legacy.SetPrescribedDisplacement(movement.Movement.UX, movement.Movement.UY, movement.Movement.UZ);
                legacy.SetPrescribedRotation(movement.Movement.RX, movement.Movement.RY, movement.Movement.RZ);
                Warn(diagnostics, "M32SM-SETTLEMENT-SCOPE", $"Prescribed movement '{movement.Label}' is flattened to model-wide StructuralModel support values.");
            }
            model.Nodes.Add(legacy);
        }
        foreach (var line in source.Model.LineObjects)
        {
            StructuralElement legacy = line switch
            {
                Frame3D => new FrameElement3D(lineIds[line.Id], nodeIds[line.StartNodeId], nodeIds[line.EndNodeId], materialIds[line.MaterialId], sectionIds[line.SectionId])
                {
                    RollAngleRadians = line.LocalAxis.RollRadians, StartRigidEndOffset = line.StartRigidOffset, EndRigidEndOffset = line.EndRigidOffset,
                    StartInsertionPointLocal = ToVector(line.StartInsertionOffsetLocal), EndInsertionPointLocal = ToVector(line.EndInsertionOffsetLocal),
                    Releases = new FrameMemberRelease
                    {
                        StartMomentY = line.StartRelease.Released.RY, StartMomentZ = line.StartRelease.Released.RZ,
                        EndMomentY = line.EndRelease.Released.RY, EndMomentZ = line.EndRelease.Released.RZ
                    }
                },
                Truss3D => new TrussElement(lineIds[line.Id], nodeIds[line.StartNodeId], nodeIds[line.EndNodeId], materialIds[line.MaterialId], sectionIds[line.SectionId]),
                _ => throw new NotSupportedException($"Unsupported Model3D line type {line.GetType().Name}.")
            };
            if (line is Frame3D && (line.StartRelease.Released.UX || line.StartRelease.Released.UY || line.StartRelease.Released.UZ || line.StartRelease.Released.RX ||
                                    line.EndRelease.Released.UX || line.EndRelease.Released.UY || line.EndRelease.Released.UZ || line.EndRelease.Released.RX ||
                                    line.StartRelease.PartialFixity is not null || line.EndRelease.PartialFixity is not null))
                Warn(diagnostics, "M32SM-RELEASE", $"Frame '{line.Label}' has release/partial-fixity DOFs not supported by StructuralModel; only local RY/RZ moment releases were transferred.");
            model.Elements.Add(legacy);
        }
        foreach (var pattern in source.LoadDefinitions.LoadPatterns)
        {
            model.LoadCases.Add(new LoadCase
            {
                CaseId = PatternCaseId(pattern, patternIds[pattern.Id]), Name = pattern.Label, Type = MapLoadCaseType(pattern.Kind),
                IncludeSelfWeight = pattern.SelfWeightMultiplier != 0
            });
            if (pattern.SelfWeightMultiplier is not 0 and not 1)
                Warn(diagnostics, "M32SM-SELF-WEIGHT", $"Load pattern '{pattern.Label}' self-weight multiplier {pattern.SelfWeightMultiplier:g} is reduced to a boolean in StructuralModel.");
        }
        AddModel3DLoads(source, model, nodeIds, lineIds, patternIds, diagnostics);
        AddModel3DCombinations(source, model, patternIds, diagnostics);

        foreach (var area in source.Model.AreaObjects)
            Warn(diagnostics, "M32SM-AREA", $"AreaObject3D '{area.Label}' is storage-only and was not transferred into StructuralModel analysis input.");
        foreach (var spring in source.Model.Springs)
            Warn(diagnostics, "M32SM-SPRING", $"Spring '{spring.Label}' is not supported by StructuralModel and was not transferred.");
        foreach (var rigid in source.Model.RigidLinks)
            Warn(diagnostics, "M32SM-RIGID-LINK", $"Rigid link '{rigid.Label}' is not supported by StructuralModel and was not transferred.");
        foreach (var constraint in source.Model.Constraints)
            Warn(diagnostics, "M32SM-CONSTRAINT", $"Master-slave constraint '{constraint.Label}' is not supported by StructuralModel and was not transferred.");

        return new ProjectDocumentToStructuralModelResult(model, nodeIds, lineIds, diagnostics);
    }

    private static Guid AddLoadPatterns(StructuralModel source, ProjectDocument document, List<AdapterDiagnostic> diagnostics)
    {
        foreach (var loadCase in source.LoadCases)
        {
            document.LoadDefinitions.LoadPatterns.Add(new LoadPattern3D
            {
                Id = StableId("load-pattern", loadCase.CaseId), Label = string.IsNullOrWhiteSpace(loadCase.Name) ? loadCase.CaseId : loadCase.Name,
                Kind = MapLoadPatternKind(loadCase.Type), SelfWeightMultiplier = loadCase.IncludeSelfWeight ? 1 : 0,
                Source = new SourceMetadata { SourceSystem = SourceSystem, SourceObjectId = loadCase.CaseId }
            });
            if (loadCase.LoadFactor != 1)
                Warn(diagnostics, "SM2M3-LOAD-FACTOR", $"Load case '{loadCase.CaseId}' LoadFactor {loadCase.LoadFactor:g} is not a Model3D load-pattern property and was not transferred.");
        }
        if (!source.LoadCases.Any(loadCase => string.Equals(loadCase.CaseId, SettlementLoadCaseId, StringComparison.OrdinalIgnoreCase)))
        {
            document.LoadDefinitions.LoadPatterns.Add(new LoadPattern3D { Id = StableId("load-pattern", SettlementLoadCaseId), Label = SettlementLoadCaseId, Kind = LoadPatternKind.Settlement });
        }
        return StableId("load-pattern", SettlementLoadCaseId);
    }

    private static void AddLoads(StructuralModel source, ProjectDocument document, IReadOnlyDictionary<int, Guid> nodes, IReadOnlyDictionary<int, Guid> lines, List<AdapterDiagnostic> diagnostics)
    {
        foreach (var loadCase in source.LoadCases)
        {
            Guid patternId = StableId("load-pattern", loadCase.CaseId);
            foreach (var force in loadCase.NodeForces)
            {
                document.LoadDefinitions.Assignments.Add(new NodalLoadAssignment3D
                {
                    Id = StableId("load-case-node-force", $"{loadCase.CaseId}:{force.Key}"), Label = $"{loadCase.CaseId} N{force.Key}", LoadPatternId = patternId,
                    NodeId = nodes[force.Key], Force = new Vector3DValue(force.Value.Fx, force.Value.Fy, force.Value.Fz)
                });
            }
        }
        foreach (var indexedLoad in source.Loads.Select((load, index) => (load, index)))
        {
            var load = indexedLoad.load;
            Guid assignmentId = StableId("load-assignment", indexedLoad.index);
            Guid patternId = StableId("load-pattern", load.LoadCaseId);
            if (!document.LoadDefinitions.LoadPatterns.Any(pattern => pattern.Id == patternId))
            {
                document.LoadDefinitions.LoadPatterns.Add(new LoadPattern3D { Id = patternId, Label = load.LoadCaseId, Kind = LoadPatternKind.Other, Source = new SourceMetadata { SourceSystem = SourceSystem, SourceObjectId = load.LoadCaseId } });
                Warn(diagnostics, "SM2M3-IMPLICIT-PATTERN", $"Created Model3D load pattern '{load.LoadCaseId}' for a StructuralModel load without a declared LoadCase.");
            }
            switch (load)
            {
                case NodalLoad nodal:
                    document.LoadDefinitions.Assignments.Add(new NodalLoadAssignment3D { Id = assignmentId, Label = $"Nodal {nodal.NodeId}", LoadPatternId = patternId, NodeId = nodes[nodal.NodeId], Force = ToVector(nodal.Force), Moment = ToVector(nodal.Moment) });
                    break;
                case MemberDistributedLoad distributed:
                    document.LoadDefinitions.Assignments.Add(new LineLoadAssignment3D { Id = assignmentId, Label = $"Distributed {distributed.ElementId}", LoadPatternId = patternId, LineObjectId = lines[distributed.ElementId], Basis = IsLocal(distributed.Direction) ? LoadCoordinateBasis.Local : LoadCoordinateBasis.Global, ForcePerLength = ToVector(distributed.ForcePerLength), StartRelativePosition = distributed.StartRelativeDistance, EndRelativePosition = distributed.EndRelativeDistance });
                    break;
                case MemberPointLoad point:
                    Warn(diagnostics, "SM2M3-POINT-LOAD", $"Member point load on element {point.ElementId} was not transferred because Model3D V1 currently defines nodal and distributed-line assignments only.");
                    break;
                case MemberTemperatureLoad temperature:
                    Warn(diagnostics, "SM2M3-TEMPERATURE", $"Temperature load on element {temperature.ElementId} was not transferred because Model3D V1 load assignment semantics do not yet include it.");
                    break;
            }
        }
    }

    private static void AddDefaultAppliedForces(StructuralModel source, ProjectDocument document, IReadOnlyDictionary<int, Guid> nodes, List<AdapterDiagnostic> diagnostics)
    {
        var nodesWithLoads = source.Nodes.Where(node => node.AppliedForce.Magnitude > 0 || node.AppliedMoment.Magnitude > 0).ToList();
        if (nodesWithLoads.Count == 0) return;
        Guid patternId = StableId("load-pattern", DefaultLoadCaseId);
        document.LoadDefinitions.LoadPatterns.Add(new LoadPattern3D { Id = patternId, Label = DefaultLoadCaseId, Kind = LoadPatternKind.Other });
        foreach (var node in nodesWithLoads)
            document.LoadDefinitions.Assignments.Add(new NodalLoadAssignment3D { Id = StableId("default-node-load", node.Id), Label = $"Default N{node.Id}", LoadPatternId = patternId, NodeId = nodes[node.Id], Force = ToVector(node.AppliedForce), Moment = ToVector(node.AppliedMoment) });
        Warn(diagnostics, "SM2M3-DEFAULT-LOAD", "Node AppliedForce/AppliedMoment values were exported as the explicit LEGACY-DEFAULT load pattern.");
    }

    private static void AddCombinations(StructuralModel source, ProjectDocument document, List<AdapterDiagnostic> diagnostics)
    {
        foreach (var combination in source.LoadCombinations)
        {
            var factors = new Dictionary<Guid, double>();
            foreach (var factor in combination.LoadCases)
            {
                Guid patternId = StableId("load-pattern", factor.Key);
                if (!document.LoadDefinitions.LoadPatterns.Any(pattern => pattern.Id == patternId))
                {
                    Warn(diagnostics, "SM2M3-COMBINATION-REFERENCE", $"Load combination '{combination.CombinationId}' references missing load case '{factor.Key}' and was not transferred.");
                    factors.Clear();
                    break;
                }
                factors[patternId] = factor.Value;
            }
            if (factors.Count > 0)
                document.LoadDefinitions.LoadCombinations.Add(new LoadCombination3D { Id = StableId("combination", combination.CombinationId), Label = string.IsNullOrWhiteSpace(combination.Name) ? combination.CombinationId : combination.Name, LoadPatternFactors = factors });
        }
    }

    private static void AddModel3DLoads(ProjectDocument source, StructuralModel target, IReadOnlyDictionary<Guid, int> nodes, IReadOnlyDictionary<Guid, int> lines, IReadOnlyDictionary<Guid, int> patterns, List<AdapterDiagnostic> diagnostics)
    {
        foreach (var assignment in source.LoadDefinitions.Assignments)
        {
            string caseId = PatternCaseId(source.LoadDefinitions.LoadPatterns.Single(pattern => pattern.Id == assignment.LoadPatternId), patterns[assignment.LoadPatternId]);
            switch (assignment)
            {
                case NodalLoadAssignment3D nodal:
                    target.Loads.Add(new NodalLoad { LoadCaseId = caseId, NodeId = nodes[nodal.NodeId], Force = ToVector(nodal.Force), Moment = ToVector(nodal.Moment) });
                    break;
                case LineLoadAssignment3D line:
                    target.Loads.Add(new MemberDistributedLoad { LoadCaseId = caseId, ElementId = lines[line.LineObjectId], ForcePerLength = ToVector(line.ForcePerLength), Direction = line.Basis == LoadCoordinateBasis.Local ? LoadDirection.LocalX : LoadDirection.GlobalX, StartRelativeDistance = line.StartRelativePosition, EndRelativeDistance = line.EndRelativePosition });
                    break;
            }
        }
    }

    private static void AddModel3DCombinations(ProjectDocument source, StructuralModel target, IReadOnlyDictionary<Guid, int> patterns, List<AdapterDiagnostic> diagnostics)
    {
        foreach (var combination in source.LoadDefinitions.LoadCombinations)
        {
            var factors = new Dictionary<string, double>();
            foreach (var factor in combination.LoadPatternFactors)
            {
                var pattern = source.LoadDefinitions.LoadPatterns.Single(pattern => pattern.Id == factor.Key);
                factors[PatternCaseId(pattern, patterns[pattern.Id])] = factor.Value;
            }
            target.LoadCombinations.Add(new LoadCombination { CombinationId = combination.Id.ToString("N"), Name = combination.Label, LoadCases = factors });
        }
    }

    private static string PatternCaseId(LoadPattern3D pattern, int fallback) =>
        !string.IsNullOrWhiteSpace(pattern.Source.SourceObjectId) ? pattern.Source.SourceObjectId : $"LP-{fallback}";

    private static Guid StableId(string type, object value) => GuidUtility.Create($"gostructanalysis/model3d-v1/{type}/{value}");
    private static string BuildDocumentIdentity(StructuralModel source) => $"gostructanalysis/model3d-v1/document/{string.Join("|", source.Nodes.OrderBy(node => node.Id).Select(node => $"N:{node.Id}:{node.Position.X:R}:{node.Position.Y:R}:{node.Position.Z:R}"))}/{string.Join("|", source.Elements.OrderBy(element => element.Id).Select(element => $"E:{element.Id}:{element.StartNodeId}:{element.EndNodeId}:{element.MaterialId}:{element.SectionId}"))}";
    private static SourceMetadata LegacySource(string type, int id) => new() { SourceSystem = SourceSystem, SourceObjectId = id.ToString(CultureInfo.InvariantCulture), Notes = type };
    private static Point3DValue ToPoint(Point3D value) => new(value.X, value.Y, value.Z);
    private static Vector3DValue ToVector(Vector3D value) => new(value.X, value.Y, value.Z);
    private static Vector3D ToVector(Vector3DValue value) => new(value.X, value.Y, value.Z);
    private static bool IsLocal(LoadDirection direction) => direction is LoadDirection.LocalX or LoadDirection.LocalY or LoadDirection.LocalZ;
    private static bool HasValues(DofValues value) => value != new DofValues();
    private static void Warn(List<AdapterDiagnostic> diagnostics, string code, string message) => diagnostics.Add(new(AdapterDiagnosticSeverity.Warning, code, message));
    private static void AddPositive(Dictionary<string, double> values, string key, double value) { if (value > 0) values[key] = value; }
    private static double Dimension(Section3D section, string key) => section.DisplayDimensions.TryGetValue(key, out double value) ? value : 0;

    private static DofRestraints ToDofRestraints(Node node) => new(node.ConstraintX, node.ConstraintY, node.ConstraintZ, node.ConstraintRX, node.ConstraintRY, node.ConstraintRZ);
    private static void ApplyRestraints(Node node, DofRestraints value)
    {
        node.ConstraintX = value.UX; node.ConstraintY = value.UY; node.ConstraintZ = value.UZ;
        node.ConstraintRX = value.RX; node.ConstraintRY = value.RY; node.ConstraintRZ = value.RZ;
    }
    private static Vector3DValue SelectReferenceAxis(StructuralModel model, StructuralElement element)
    {
        var start = model.Nodes.Single(node => node.Id == element.StartNodeId).Position;
        var end = model.Nodes.Single(node => node.Id == element.EndNodeId).Position;
        var axis = end.Subtract(start).Normalize();
        return Math.Abs(axis.Dot(new Vector3D(0, 0, 1))) > 0.95 ? new Vector3DValue(0, 1, 0) : new Vector3DValue(0, 0, 1);
    }
    private static MaterialKind MapMaterialKind(MaterialType value) => value switch { MaterialType.Steel => MaterialKind.Steel, MaterialType.Concrete => MaterialKind.Concrete, MaterialType.Aluminum => MaterialKind.Aluminum, MaterialType.Timber => MaterialKind.Timber, _ => MaterialKind.Custom };
    private static MaterialType MapMaterialType(MaterialKind value) => value switch { MaterialKind.Steel => MaterialType.Steel, MaterialKind.Concrete => MaterialType.Concrete, MaterialKind.Aluminum => MaterialType.Aluminum, MaterialKind.Timber => MaterialType.Timber, _ => MaterialType.Custom };
    private static SectionShape MapSectionShape(SectionType value) => value switch { SectionType.Rectangular => SectionShape.Rectangle, SectionType.RC_Rectangular => SectionShape.Rectangle, SectionType.Circular => SectionShape.Circle, SectionType.Pipe => SectionShape.Pipe, SectionType.IShape => SectionShape.IShape, SectionType.Channel => SectionShape.Channel, SectionType.Box => SectionShape.Box, _ => SectionShape.Generic };
    private static SectionType MapSectionType(SectionShape value) => value switch { SectionShape.Rectangle => SectionType.Rectangular, SectionShape.Circle => SectionType.Circular, SectionShape.Pipe => SectionType.Pipe, SectionShape.IShape => SectionType.IShape, SectionShape.Channel => SectionType.Channel, SectionShape.Box => SectionType.Box, _ => SectionType.Generic };
    private static LoadPatternKind MapLoadPatternKind(LoadCaseType value) => value switch { LoadCaseType.Wind => LoadPatternKind.Wind, LoadCaseType.Seismic => LoadPatternKind.Seismic, LoadCaseType.Temperature => LoadPatternKind.Temperature, _ => LoadPatternKind.Other };
    private static LoadCaseType MapLoadCaseType(LoadPatternKind value) => value switch { LoadPatternKind.Wind => LoadCaseType.Wind, LoadPatternKind.Seismic => LoadCaseType.Seismic, LoadPatternKind.Temperature => LoadCaseType.Temperature, _ => LoadCaseType.Static };
}

internal static class GuidUtility
{
    public static Guid Create(string value)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }
}

internal static class LegacyIdMap
{
    public static IReadOnlyDictionary<Guid, int> Create<T>(IEnumerable<T> objects, Func<T, SourceMetadata> source, string type, List<AdapterDiagnostic> diagnostics) where T : IPersistentModelObject
    {
        var result = new Dictionary<Guid, int>();
        var used = new HashSet<int>();
        int next = 1;
        foreach (var item in objects)
        {
            int id;
            if (!int.TryParse(source(item).SourceObjectId, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0 || !used.Add(id))
            {
                while (used.Contains(next)) next++;
                id = next++;
                used.Add(id);
                diagnostics.Add(new AdapterDiagnostic(AdapterDiagnosticSeverity.Warning, "M32SM-NEW-LEGACY-ID", $"Assigned StructuralModel {type} ID {id} to Model3D object '{item.Label}'."));
            }
            result[item.Id] = id;
        }
        return result;
    }
}
