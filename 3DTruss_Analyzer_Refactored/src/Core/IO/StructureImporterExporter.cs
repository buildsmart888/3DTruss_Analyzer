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
