namespace TrussAnalyzer.Core.Application;

using System.Globalization;
using System.Text;
using TrussAnalyzer.Core.Models;

/// <summary>Writes a deterministic, SI-unit OpenSees Tcl benchmark model.</summary>
public sealed class OpenSeesTclExporter
{
    public string Export(StructuralModel model, string? loadCaseId = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var text = new StringBuilder();
        text.AppendLine("# GOStructAnalysis OpenSees comparison contract v1");
        text.AppendLine("# Units: m, N, Pa, kg/m3");
        text.AppendLine("wipe; model BasicBuilder -ndm 3 -ndf 6");
        foreach (var node in model.Nodes.OrderBy(n => n.Id))
            text.AppendLine($"node {node.Id} {F(node.Position.X)} {F(node.Position.Y)} {F(node.Position.Z)}");
        foreach (var node in model.Nodes.OrderBy(n => n.Id))
        {
            var fix = string.Join(' ', new[] { node.ConstraintX, node.ConstraintY, node.ConstraintZ, node.ConstraintRX, node.ConstraintRY, node.ConstraintRZ }.Select(v => v ? "1" : "0"));
            if (fix.Any(c => c == '1')) text.AppendLine($"fix {node.Id} {fix}");
        }
        foreach (var material in model.Materials.OrderBy(m => m.Id))
        {
            text.AppendLine($"# material {material.Id} {material.Name}");
            text.AppendLine($"uniaxialMaterial Elastic {material.Id} {F(material.YoungsModulus)}");
        }
        text.AppendLine("geomTransf Linear 1 0 0 1");
        foreach (var element in model.Elements.OrderBy(e => e.Id))
        {
            var start = model.Nodes.Single(n => n.Id == element.StartNodeId);
            var end = model.Nodes.Single(n => n.Id == element.EndNodeId);
            var material = model.Materials.Single(m => m.Id == element.MaterialId);
            var section = model.Sections.Single(s => s.Id == element.SectionId);
            if (element.Type == ElementType.Truss)
                text.AppendLine($"element truss {element.Id} {element.StartNodeId} {element.EndNodeId} {F(section.Area)} {element.MaterialId}");
            else
                text.AppendLine($"element elasticBeamColumn {element.Id} {element.StartNodeId} {element.EndNodeId} {F(section.Area)} {F(material.YoungsModulus)} {F(material.EffectiveShearModulus)} {F(section.J)} {F(section.Iy)} {F(section.Iz)} 1");
        }
        var loadCase = model.LoadCases.FirstOrDefault(c => string.IsNullOrWhiteSpace(loadCaseId) || string.Equals(c.CaseId, loadCaseId, StringComparison.OrdinalIgnoreCase));
        if (loadCase is not null)
        {
            text.AppendLine($"pattern Plain 1 Linear {{ # {loadCase.CaseId}");
            foreach (var force in loadCase.NodeForces.OrderBy(p => p.Key))
                text.AppendLine($"  load {force.Key} {F(force.Value.Fx)} {F(force.Value.Fy)} {F(force.Value.Fz)} 0 0 0");
            text.AppendLine("}");
        }
        text.AppendLine("# end benchmark model");
        return text.ToString();
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
