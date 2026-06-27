namespace TrussAnalyzer.Core.IO;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using TrussAnalyzer.Core.Models;

/// <summary>
/// Handles import and export of truss structure data.
/// Supports JSON, CSV, and custom text formats.
/// </summary>
public static class StructureImporterExporter
{
    public static string ExportStructuralModelToJson(StructuralModel model)
    {
        var data = new
        {
            schemaVersion = 2,
            coordinateSystem = model.CoordinateSystem.ToString(),
            displaySettings = model.DisplaySettings,
            activeLoadCaseId = model.ActiveLoadCaseId,
            nodes = model.Nodes.Select(n => new
            {
                n.Id,
                x = n.Position.X,
                y = n.Position.Y,
                z = n.Position.Z,
                fixUx = n.ConstraintX,
                fixUy = n.ConstraintY,
                fixUz = n.ConstraintZ,
                fixRx = n.ConstraintRX,
                fixRy = n.ConstraintRY,
                fixRz = n.ConstraintRZ,
                fx = n.AppliedForce.X,
                fy = n.AppliedForce.Y,
                fz = n.AppliedForce.Z,
                mx = n.AppliedMoment.X,
                my = n.AppliedMoment.Y,
                mz = n.AppliedMoment.Z
            }),
            materials = model.Materials.Select(m => new
            {
                m.Id,
                m.Name,
                type = m.Type.ToString(),
                m.YoungsModulus,
                m.ShearModulus,
                m.PoissonsRatio,
                m.Density,
                m.YieldStrength,
                m.UltimateStrength,
                m.ConcreteCompressiveStrength
            }),
            sections = model.Sections.Select(s => new
            {
                s.Id,
                s.Name,
                type = s.Type.ToString(),
                s.Area,
                s.Iy,
                s.Iz,
                s.J,
                s.Depth,
                s.Width,
                s.Thickness,
                s.Diameter,
                s.RebarArea,
                s.EffectiveDepth
            }),
            elements = model.Elements.Select(e => new
            {
                e.Id,
                type = e.Type.ToString(),
                startNodeId = e.StartNodeId,
                endNodeId = e.EndNodeId,
                materialId = e.MaterialId,
                sectionId = e.SectionId,
                rollAngleRadians = e.RollAngleRadians,
                releases = new
                {
                    e.Releases.StartMomentY,
                    e.Releases.StartMomentZ,
                    e.Releases.EndMomentY,
                    e.Releases.EndMomentZ
                }
            }),
            loadCases = model.LoadCases,
            loadCombinations = model.LoadCombinations,
            designSettings = model.DesignSettings,
            loads = model.Loads.Select(l => l switch
            {
                NodalLoad n => new
                {
                    kind = "Nodal",
                    n.LoadCaseId,
                    nodeId = n.NodeId,
                    fx = n.Force.X,
                    fy = n.Force.Y,
                    fz = n.Force.Z,
                    mx = n.Moment.X,
                    my = n.Moment.Y,
                    mz = n.Moment.Z,
                    elementId = 0,
                    relativeDistance = 0.0,
                    wx = 0.0,
                    wy = 0.0,
                    wz = 0.0,
                    direction = "",
                    startRelativeDistance = 0.0,
                    endRelativeDistance = 1.0
                },
                MemberPointLoad p => new
                {
                    kind = "MemberPoint",
                    p.LoadCaseId,
                    nodeId = 0,
                    fx = p.Force.X,
                    fy = p.Force.Y,
                    fz = p.Force.Z,
                    mx = p.Moment.X,
                    my = p.Moment.Y,
                    mz = p.Moment.Z,
                    elementId = p.ElementId,
                    relativeDistance = p.RelativeDistance,
                    wx = 0.0,
                    wy = 0.0,
                    wz = 0.0,
                    direction = p.Direction.ToString(),
                    startRelativeDistance = 0.0,
                    endRelativeDistance = 1.0
                },
                MemberDistributedLoad d => new
                {
                    kind = "MemberDistributed",
                    d.LoadCaseId,
                    nodeId = 0,
                    fx = 0.0,
                    fy = 0.0,
                    fz = 0.0,
                    mx = 0.0,
                    my = 0.0,
                    mz = 0.0,
                    elementId = d.ElementId,
                    relativeDistance = 0.0,
                    wx = d.ForcePerLength.X,
                    wy = d.ForcePerLength.Y,
                    wz = d.ForcePerLength.Z,
                    direction = d.Direction.ToString(),
                    startRelativeDistance = d.StartRelativeDistance,
                    endRelativeDistance = d.EndRelativeDistance
                },
                _ => throw new NotSupportedException($"Unsupported load item type {l.GetType().Name}.")
            })
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    public static StructuralModel ImportStructuralModelFromJson(string jsonContent)
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        int schemaVersion = TryGetPropertyCaseInsensitive(root, "schemaVersion", out var version)
            ? version.GetInt32()
            : 1;

        if (schemaVersion < 2)
            return StructuralModel.FromTrussSolver(ImportFromJson(jsonContent));

        var model = new StructuralModel();
        if (TryGetPropertyCaseInsensitive(root, "coordinateSystem", out var coordinateElem) &&
            Enum.TryParse<CoordinateConvention>(coordinateElem.GetString(), ignoreCase: true, out var coordinate))
        {
            model.CoordinateSystem = coordinate;
        }

        if (TryGetPropertyCaseInsensitive(root, "displaySettings", out var displayElem))
        {
            var display = JsonSerializer.Deserialize<ViewerDisplayOptions>(displayElem.GetRawText());
            if (display != null)
                model.DisplaySettings = display;
        }

        if (TryGetPropertyCaseInsensitive(root, "activeLoadCaseId", out var activeLoadCaseElem))
            model.ActiveLoadCaseId = activeLoadCaseElem.GetString() ?? string.Empty;

        if (TryGetPropertyCaseInsensitive(root, "nodes", out var nodesElem))
        {
            foreach (var n in nodesElem.EnumerateArray())
            {
                var node = new Node(GetPropertyCaseInsensitive(n, "id").GetInt32(), new Point3D(
                    GetDoubleOrDefault(n, "x"),
                    GetDoubleOrDefault(n, "y"),
                    GetDoubleOrDefault(n, "z")))
                {
                    ConstraintX = GetBoolOrDefault(n, "fixUx"),
                    ConstraintY = GetBoolOrDefault(n, "fixUy"),
                    ConstraintZ = GetBoolOrDefault(n, "fixUz"),
                    ConstraintRX = GetBoolOrDefault(n, "fixRx"),
                    ConstraintRY = GetBoolOrDefault(n, "fixRy"),
                    ConstraintRZ = GetBoolOrDefault(n, "fixRz")
                };
                node.ApplyForce(GetDoubleOrDefault(n, "fx"), GetDoubleOrDefault(n, "fy"), GetDoubleOrDefault(n, "fz"));
                node.ApplyMoment(GetDoubleOrDefault(n, "mx"), GetDoubleOrDefault(n, "my"), GetDoubleOrDefault(n, "mz"));
                model.Nodes.Add(node);
            }
        }

        if (TryGetPropertyCaseInsensitive(root, "materials", out var materialsElem))
        {
            foreach (var m in materialsElem.EnumerateArray())
            {
                model.Materials.Add(new Material
                {
                    Id = GetPropertyCaseInsensitive(m, "id").GetInt32(),
                    Name = GetStringOrDefault(m, "name", "Material"),
                    Type = Enum.Parse<MaterialType>(GetStringOrDefault(m, "type", "Custom"), ignoreCase: true),
                    YoungsModulus = GetDoubleOrDefault(m, "youngsModulus"),
                    ShearModulus = GetDoubleOrDefault(m, "shearModulus"),
                    PoissonsRatio = GetDoubleOrDefault(m, "poissonsRatio"),
                    Density = GetDoubleOrDefault(m, "density"),
                    YieldStrength = GetDoubleOrDefault(m, "yieldStrength"),
                    UltimateStrength = GetDoubleOrDefault(m, "ultimateStrength"),
                    ConcreteCompressiveStrength = GetDoubleOrDefault(m, "concreteCompressiveStrength")
                });
            }
        }

        if (TryGetPropertyCaseInsensitive(root, "sections", out var sectionsElem))
        {
            foreach (var s in sectionsElem.EnumerateArray())
            {
                model.Sections.Add(new Section
                {
                    Id = GetPropertyCaseInsensitive(s, "id").GetInt32(),
                    Name = GetStringOrDefault(s, "name", "Section"),
                    Type = Enum.Parse<SectionType>(GetStringOrDefault(s, "type", "Generic"), ignoreCase: true),
                    Area = GetDoubleOrDefault(s, "area"),
                    Iy = GetDoubleOrDefault(s, "iy"),
                    Iz = GetDoubleOrDefault(s, "iz"),
                    J = GetDoubleOrDefault(s, "j"),
                    Depth = GetDoubleOrDefault(s, "depth"),
                    Width = GetDoubleOrDefault(s, "width"),
                    Thickness = GetDoubleOrDefault(s, "thickness"),
                    Diameter = GetDoubleOrDefault(s, "diameter"),
                    RebarArea = GetDoubleOrDefault(s, "rebarArea"),
                    EffectiveDepth = GetDoubleOrDefault(s, "effectiveDepth")
                });
            }
        }

        if (TryGetPropertyCaseInsensitive(root, "elements", out var elementsElem))
        {
            foreach (var e in elementsElem.EnumerateArray())
            {
                var type = Enum.Parse<ElementType>(GetStringOrDefault(e, "type", "Truss"), ignoreCase: true);
                int id = GetPropertyCaseInsensitive(e, "id").GetInt32();
                int start = GetPropertyCaseInsensitive(e, "startNodeId").GetInt32();
                int end = GetPropertyCaseInsensitive(e, "endNodeId").GetInt32();
                int materialId = GetPropertyCaseInsensitive(e, "materialId").GetInt32();
                int sectionId = GetPropertyCaseInsensitive(e, "sectionId").GetInt32();
                double rollAngle = GetDoubleOrDefault(e, "rollAngleRadians");
                var releases = ReadReleases(e);
                model.Elements.Add(type == ElementType.Frame3D
                    ? new FrameElement3D
                    {
                        Id = id,
                        StartNodeId = start,
                        EndNodeId = end,
                        MaterialId = materialId,
                        SectionId = sectionId,
                        RollAngleRadians = rollAngle,
                        Releases = releases
                    }
                    : new TrussElement
                    {
                        Id = id,
                        StartNodeId = start,
                        EndNodeId = end,
                        MaterialId = materialId,
                        SectionId = sectionId,
                        RollAngleRadians = rollAngle,
                        Releases = releases
                    });
            }
        }

        if (TryGetPropertyCaseInsensitive(root, "designSettings", out var designSettingsElem))
        {
            model.DesignSettings = new DesignSettings
            {
                SteelResistanceFactor = GetDoubleOrDefault(designSettingsElem, "steelResistanceFactor", 0.9),
                ConcreteFlexureResistanceFactor = GetDoubleOrDefault(designSettingsElem, "concreteFlexureResistanceFactor", 0.9),
                ConcreteShearResistanceFactor = GetDoubleOrDefault(designSettingsElem, "concreteShearResistanceFactor", 0.75),
                CompressionEffectiveLengthFactor = GetDoubleOrDefault(designSettingsElem, "compressionEffectiveLengthFactor", 1.0),
                DefaultSteelYieldStrength = GetDoubleOrDefault(designSettingsElem, "defaultSteelYieldStrength", 250e6),
                DefaultRebarYieldStrength = GetDoubleOrDefault(designSettingsElem, "defaultRebarYieldStrength", 420e6),
                SectionClassification = GetStringOrDefault(designSettingsElem, "sectionClassification", "MVP compact placeholder")
            };
        }

        if (TryGetPropertyCaseInsensitive(root, "loadCases", out var loadCasesElem))
        {
            var cases = JsonSerializer.Deserialize<List<LoadCase>>(loadCasesElem.GetRawText());
            if (cases != null)
                model.LoadCases.AddRange(cases);
        }

        if (TryGetPropertyCaseInsensitive(root, "loadCombinations", out var combinationsElem))
        {
            var combinations = JsonSerializer.Deserialize<List<LoadCombination>>(combinationsElem.GetRawText());
            if (combinations != null)
                model.LoadCombinations.AddRange(combinations);
        }

        if (TryGetPropertyCaseInsensitive(root, "loads", out var loadsElem))
        {
            foreach (var load in loadsElem.EnumerateArray())
            {
                string kind = GetStringOrDefault(load, "kind", "");
                string loadCaseId = GetStringOrDefault(load, "loadCaseId", "");
                if (kind == "Nodal")
                {
                    model.Loads.Add(new NodalLoad
                    {
                        LoadCaseId = loadCaseId,
                        NodeId = GetPropertyCaseInsensitive(load, "nodeId").GetInt32(),
                        Force = new Vector3D(GetDoubleOrDefault(load, "fx"), GetDoubleOrDefault(load, "fy"), GetDoubleOrDefault(load, "fz")),
                        Moment = new Vector3D(GetDoubleOrDefault(load, "mx"), GetDoubleOrDefault(load, "my"), GetDoubleOrDefault(load, "mz"))
                    });
                }
                else if (kind == "MemberPoint")
                {
                    model.Loads.Add(new MemberPointLoad
                    {
                        LoadCaseId = loadCaseId,
                        ElementId = GetPropertyCaseInsensitive(load, "elementId").GetInt32(),
                        RelativeDistance = GetDoubleOrDefault(load, "relativeDistance", 0.5),
                        Force = new Vector3D(GetDoubleOrDefault(load, "fx"), GetDoubleOrDefault(load, "fy"), GetDoubleOrDefault(load, "fz")),
                        Moment = new Vector3D(GetDoubleOrDefault(load, "mx"), GetDoubleOrDefault(load, "my"), GetDoubleOrDefault(load, "mz")),
                        Direction = Enum.Parse<LoadDirection>(GetStringOrDefault(load, "direction", "GlobalZ"), ignoreCase: true)
                    });
                }
                else if (kind == "MemberDistributed")
                {
                    model.Loads.Add(new MemberDistributedLoad
                    {
                        LoadCaseId = loadCaseId,
                        ElementId = GetPropertyCaseInsensitive(load, "elementId").GetInt32(),
                        ForcePerLength = new Vector3D(GetDoubleOrDefault(load, "wx"), GetDoubleOrDefault(load, "wy"), GetDoubleOrDefault(load, "wz")),
                        Direction = Enum.Parse<LoadDirection>(GetStringOrDefault(load, "direction", "GlobalZ"), ignoreCase: true),
                        StartRelativeDistance = GetDoubleOrDefault(load, "startRelativeDistance", 0.0),
                        EndRelativeDistance = GetDoubleOrDefault(load, "endRelativeDistance", 1.0)
                    });
                }
            }
        }

        return model;
    }

    /// <summary>
    /// Exports structure to JSON format.
    /// </summary>
    public static string ExportToJson(TrussSolver solver)
    {
        var data = new
        {
            Nodes = solver.GetNodes().Select(n => new
            {
                n.Id,
                X = n.Position.X,
                Y = n.Position.Y,
                Z = n.Position.Z,
                ConstraintX = n.ConstraintX,
                ConstraintY = n.ConstraintY,
                ConstraintZ = n.ConstraintZ,
                ForceX = n.AppliedForce.X,
                ForceY = n.AppliedForce.Y,
                ForceZ = n.AppliedForce.Z
            }),
            Elements = solver.GetElements().Select(e => new
            {
                e.Id,
                StartNodeId = e.StartNodeId,
                EndNodeId = e.EndNodeId,
                Area = e.Area,
                Material = new
                {
                    Name = e.Material.Name,
                    YoungsModulus = e.Material.YoungsModulus,
                    PoissonsRatio = e.Material.PoissonsRatio,
                    Density = e.Material.Density
                }
            })
        };
        
        return JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
    
    /// <summary>
    /// Imports structure from JSON format.
    /// </summary>
    public static TrussSolver ImportFromJson(string jsonContent)
    {
        var solver = new TrussSolver();
        var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        
        // Import nodes
        if (TryGetPropertyCaseInsensitive(root, "Nodes", out var nodesElem))
        {
            foreach (var nodeElem in nodesElem.EnumerateArray())
            {
                var node = new Node(
                    GetPropertyCaseInsensitive(nodeElem, "Id").GetInt32(),
                    new Point3D(
                        GetPropertyCaseInsensitive(nodeElem, "X").GetDouble(),
                        GetPropertyCaseInsensitive(nodeElem, "Y").GetDouble(),
                        GetPropertyCaseInsensitive(nodeElem, "Z").GetDouble()
                    )
                );
                
                if (TryGetPropertyCaseInsensitive(nodeElem, "ConstraintX", out var cx))
                    node.ConstraintX = cx.GetBoolean();
                if (TryGetPropertyCaseInsensitive(nodeElem, "ConstraintY", out var cy))
                    node.ConstraintY = cy.GetBoolean();
                if (TryGetPropertyCaseInsensitive(nodeElem, "ConstraintZ", out var cz))
                    node.ConstraintZ = cz.GetBoolean();
                    
                double fx = 0, fy = 0, fz = 0;
                if (TryGetPropertyCaseInsensitive(nodeElem, "ForceX", out var fxElem) ||
                    TryGetPropertyCaseInsensitive(nodeElem, "LoadX", out fxElem)) fx = fxElem.GetDouble();
                if (TryGetPropertyCaseInsensitive(nodeElem, "ForceY", out var fyElem) ||
                    TryGetPropertyCaseInsensitive(nodeElem, "LoadY", out fyElem)) fy = fyElem.GetDouble();
                if (TryGetPropertyCaseInsensitive(nodeElem, "ForceZ", out var fzElem) ||
                    TryGetPropertyCaseInsensitive(nodeElem, "LoadZ", out fzElem)) fz = fzElem.GetDouble();
                
                node.ApplyForce(fx, fy, fz);
                solver.AddNode(node);
            }
        }
        
        // Import elements
        Dictionary<int, Material> materials = new();
        if (TryGetPropertyCaseInsensitive(root, "Materials", out var materialsElem))
        {
            foreach (var materialElem in materialsElem.EnumerateArray())
            {
                int id = GetPropertyCaseInsensitive(materialElem, "Id").GetInt32();
                double poissonsRatio = TryGetPropertyCaseInsensitive(materialElem, "PoissonsRatio", out var pr)
                    ? pr.GetDouble()
                    : 0.3;
                materials[id] = new Material(
                    GetPropertyCaseInsensitive(materialElem, "Name").GetString() ?? "Unknown",
                    GetPropertyCaseInsensitive(materialElem, "YoungsModulus").GetDouble(),
                    poissonsRatio,
                    GetPropertyCaseInsensitive(materialElem, "Density").GetDouble());
            }
        }

        if (TryGetPropertyCaseInsensitive(root, "Elements", out var elemsElem))
        {
            foreach (var elemElem in elemsElem.EnumerateArray())
            {
                Material material;
                if (TryGetPropertyCaseInsensitive(elemElem, "MaterialId", out var materialIdElem) &&
                    materials.TryGetValue(materialIdElem.GetInt32(), out var referencedMaterial))
                {
                    material = referencedMaterial;
                }
                else
                {
                    var materialElem = GetPropertyCaseInsensitive(elemElem, "Material");
                    double poissonsRatio = TryGetPropertyCaseInsensitive(materialElem, "PoissonsRatio", out var pr)
                        ? pr.GetDouble()
                        : 0.3;
                    material = new Material(
                        GetPropertyCaseInsensitive(materialElem, "Name").GetString() ?? "Unknown",
                        GetPropertyCaseInsensitive(materialElem, "YoungsModulus").GetDouble(),
                        poissonsRatio,
                        GetPropertyCaseInsensitive(materialElem, "Density").GetDouble()
                    );
                }
                
                var element = new Element(
                    GetPropertyCaseInsensitive(elemElem, "Id").GetInt32(),
                    GetPropertyCaseInsensitive(elemElem, "StartNodeId").GetInt32(),
                    GetPropertyCaseInsensitive(elemElem, "EndNodeId").GetInt32(),
                    GetPropertyCaseInsensitive(elemElem, "Area").GetDouble(),
                    material
                );
                
                solver.AddElement(element);
            }
        }
        
        return solver;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static JsonElement GetPropertyCaseInsensitive(JsonElement element, string propertyName)
    {
        if (TryGetPropertyCaseInsensitive(element, propertyName, out var value))
            return value;

        throw new KeyNotFoundException($"Required JSON property '{propertyName}' was not found.");
    }

    private static double GetDoubleOrDefault(JsonElement element, string propertyName, double defaultValue = 0)
    {
        return TryGetPropertyCaseInsensitive(element, propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDouble()
            : defaultValue;
    }

    private static bool GetBoolOrDefault(JsonElement element, string propertyName, bool defaultValue = false)
    {
        return TryGetPropertyCaseInsensitive(element, propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetBoolean()
            : defaultValue;
    }

    private static string GetStringOrDefault(JsonElement element, string propertyName, string defaultValue)
    {
        return TryGetPropertyCaseInsensitive(element, propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() ?? defaultValue
            : defaultValue;
    }

    private static FrameMemberRelease ReadReleases(JsonElement element)
    {
        if (!TryGetPropertyCaseInsensitive(element, "releases", out var releases) || releases.ValueKind != JsonValueKind.Object)
            return new FrameMemberRelease();

        return new FrameMemberRelease
        {
            StartMomentY = GetBoolOrDefault(releases, "startMomentY"),
            StartMomentZ = GetBoolOrDefault(releases, "startMomentZ"),
            EndMomentY = GetBoolOrDefault(releases, "endMomentY"),
            EndMomentZ = GetBoolOrDefault(releases, "endMomentZ")
        };
    }
    
    /// <summary>
    /// Exports analysis results to CSV format.
    /// </summary>
    public static void ExportResultsToCsv(AnalysisResult result, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        
        // Node results
        writer.WriteLine("=== NODE RESULTS ===");
        writer.WriteLine("NodeID,Displacement_X_mm,Displacement_Y_mm,Displacement_Z_mm,Reaction_X_N,Reaction_Y_N,Reaction_Z_N");
        foreach (var node in result.Nodes)
        {
            writer.WriteLine($"{node.Id},{node.Displacement.X * 1000:F4},{node.Displacement.Y * 1000:F4},{node.Displacement.Z * 1000:F4},{node.ReactionForce.X:F2},{node.ReactionForce.Y:F2},{node.ReactionForce.Z:F2}");
        }
        
        writer.WriteLine();
        
        // Element results
        writer.WriteLine("=== ELEMENT RESULTS ===");
        writer.WriteLine("ElementID,AxialForce_N,Stress_MPa,Strain,Length_m,Utilization,Status");
        foreach (var elem in result.Elements)
        {
            var check = result.SafetyChecks.ElementChecks.FirstOrDefault(c => c.ElementId == elem.Id);
            writer.WriteLine($"{elem.Id},{elem.AxialForce:F2},{elem.Stress / 1e6:F3},{elem.Strain:F6},{elem.Length:F3},{check?.UtilizationRatio ?? 0:F3},{check?.Status ?? "N/A"}");
        }
        
        writer.WriteLine();
        writer.WriteLine("=== SUMMARY ===");
        writer.WriteLine($"Equilibrium_Satisfied,{result.EquilibriumSatisfied}");
        writer.WriteLine($"Max_Displacement_m,{result.MaxDisplacement:E4}");
        writer.WriteLine($"Max_AxialForce_N,{result.MaxAxialForce:E2}");
        writer.WriteLine($"Max_Stress_Pa,{result.MaxStress:E2}");
        writer.WriteLine($"Max_Utilization,{result.SafetyChecks.MaxUtilizationRatio:F3}");
    }
    
    /// <summary>
    /// Exports analysis results to detailed text report.
    /// </summary>
    public static void ExportReportToText(AnalysisResult result, string filePath, string projectName = "Unnamed Project")
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        
        writer.WriteLine("╔════════════════════════════════════════════════════════╗");
        writer.WriteLine("║         3D TRUSS ANALYSIS REPORT                       ║");
        writer.WriteLine("╚════════════════════════════════════════════════════════╝");
        writer.WriteLine();
        writer.WriteLine($"Project: {projectName}");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine();
        
        writer.WriteLine("┌─────────────────────────────────────────────────────────┐");
        writer.WriteLine("│ ANALYSIS SUMMARY                                        │");
        writer.WriteLine("└─────────────────────────────────────────────────────────┘");
        writer.WriteLine($"  Equilibrium Status: {(result.EquilibriumSatisfied ? "✓ SATISFIED" : "✗ NOT SATISFIED")}");
        writer.WriteLine($"  Maximum Displacement: {result.MaxDisplacement:E4} m  ({result.MaxDisplacement * 1000:F2} mm)");
        writer.WriteLine($"  Maximum Axial Force:  {result.MaxAxialForce:E2} N  ({result.MaxAxialForce / 1000:F2} kN)");
        writer.WriteLine($"  Maximum Stress:       {result.MaxStress:E2} Pa  ({result.MaxStress / 1e6:F2} MPa)");
        writer.WriteLine($"  Maximum Utilization:  {result.SafetyChecks.MaxUtilizationRatio:F3}");
        writer.WriteLine();
        
        writer.WriteLine("┌─────────────────────────────────────────────────────────┐");
        writer.WriteLine("│ NODE DISPLACEMENTS                                      │");
        writer.WriteLine("└─────────────────────────────────────────────────────────┘");
        writer.WriteLine($"  {"ID",-6} {"δX (mm)",-12} {"δY (mm)",-12} {"δZ (mm)",-12}");
        writer.WriteLine("  " + new string('-', 42));
        
        foreach (var node in result.Nodes)
        {
            writer.WriteLine($"  {node.Id,-6} {node.Displacement.X * 1000,-12:F4} {node.Displacement.Y * 1000,-12:F4} {node.Displacement.Z * 1000,-12:F4}");
        }
        writer.WriteLine();
        
        writer.WriteLine("┌─────────────────────────────────────────────────────────┐");
        writer.WriteLine("│ SUPPORT REACTIONS                                       │");
        writer.WriteLine("└─────────────────────────────────────────────────────────┘");
        writer.WriteLine($"  {"ID",-6} {"Rx (N)",-12} {"Ry (N)",-12} {"Rz (N)",-12}");
        writer.WriteLine("  " + new string('-', 42));
        
        foreach (var node in result.Nodes)
        {
            if (node.ReactionForce.Magnitude > 1e-6)
            {
                writer.WriteLine($"  {node.Id,-6} {node.ReactionForce.X,-12:F2} {node.ReactionForce.Y,-12:F2} {node.ReactionForce.Z,-12:F2}");
            }
        }
        writer.WriteLine();
        
        writer.WriteLine("┌─────────────────────────────────────────────────────────┐");
        writer.WriteLine("│ ELEMENT FORCES & STRESSES                               │");
        writer.WriteLine("└─────────────────────────────────────────────────────────┘");
        writer.WriteLine($"  {"ID",-5} {"Force (N)",-12} {"Stress (MPa)",-14} {"Util.",-8} {"Status",-10}");
        writer.WriteLine("  " + new string('-', 56));
        
        foreach (var elem in result.Elements)
        {
            var check = result.SafetyChecks.ElementChecks.FirstOrDefault(c => c.ElementId == elem.Id);
            writer.WriteLine($"  {elem.Id,-5} {elem.AxialForce,-12:F2} {(elem.Stress / 1e6),-14:F3} {check?.UtilizationRatio ?? 0,-8:F3} {check?.Status ?? "N/A",-10}");
        }
        writer.WriteLine();
        
        writer.WriteLine("┌─────────────────────────────────────────────────────────┐");
        writer.WriteLine("│ ENGINEERING NOTES                                       │");
        writer.WriteLine("└─────────────────────────────────────────────────────────┘");
        writer.WriteLine("  • Positive force = Tension, Negative force = Compression");
        writer.WriteLine("  • Results based on linear elastic analysis");
        writer.WriteLine("  • Verify equilibrium before using results for design");
        writer.WriteLine();
        writer.WriteLine("═══════════════════════════════════════════════════════════");
        writer.WriteLine("End of Report");
    }
    
    /// <summary>
    /// Imports structure from simple CSV format.
    /// Expected format: First section nodes, second section elements.
    /// </summary>
    public static TrussSolver ImportFromCsv(string csvContent)
    {
        var solver = new TrussSolver();
        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        bool inElementsSection = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;
                
            if (trimmed.Equals("[ELEMENTS]", StringComparison.OrdinalIgnoreCase))
            {
                inElementsSection = true;
                continue;
            }
            
            var parts = trimmed.Split(',');
            
            if (!inElementsSection && parts.Length >= 4)
            {
                // Node line: Id,X,Y,Z,CX,CY,CZ,FX,FY,FZ
                int id = int.Parse(parts[0], CultureInfo.InvariantCulture);
                double x = double.Parse(parts[1], CultureInfo.InvariantCulture);
                double y = double.Parse(parts[2], CultureInfo.InvariantCulture);
                double z = double.Parse(parts[3], CultureInfo.InvariantCulture);
                
                var node = new Node(id, new Point3D(x, y, z));
                
                if (parts.Length > 4) node.ConstraintX = bool.Parse(parts[4]);
                if (parts.Length > 5) node.ConstraintY = bool.Parse(parts[5]);
                if (parts.Length > 6) node.ConstraintZ = bool.Parse(parts[6]);
                
                if (parts.Length > 7)
                {
                    double fx = double.Parse(parts[7], CultureInfo.InvariantCulture);
                    double fy = parts.Length > 8 ? double.Parse(parts[8], CultureInfo.InvariantCulture) : 0;
                    double fz = parts.Length > 9 ? double.Parse(parts[9], CultureInfo.InvariantCulture) : 0;
                    node.ApplyForce(fx, fy, fz);
                }
                
                solver.AddNode(node);
            }
            else if (inElementsSection && parts.Length >= 5)
            {
                // Element line: Id,StartNode,EndNode,Area,E,Density
                int id = int.Parse(parts[0], CultureInfo.InvariantCulture);
                int startNode = int.Parse(parts[1], CultureInfo.InvariantCulture);
                int endNode = int.Parse(parts[2], CultureInfo.InvariantCulture);
                double area = double.Parse(parts[3], CultureInfo.InvariantCulture);
                double eModulus = parts.Length > 4 ? double.Parse(parts[4], CultureInfo.InvariantCulture) : 200e9;
                double density = parts.Length > 5 ? double.Parse(parts[5], CultureInfo.InvariantCulture) : 7850;
                
                var material = new Material("Imported", eModulus, 0.3, density);
                var element = new Element(id, startNode, endNode, area, material);
                solver.AddElement(element);
            }
        }
        
        return solver;
    }
}
