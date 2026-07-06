namespace TrussAnalyzer.Tests;

using System.Text;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Reporting;
using Xunit;

public class ReportGeneratorTests
{
    [Fact]
    public void PdfReportGenerator_IncludesCriteriaLimitationsAndMemberForceEnvelope()
    {
        var solver = new TrussSolver();
        solver.AddNode(new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true
        });
        var loaded = new Node(2, new Point3D(1, 0, 0))
        {
            ConstraintY = true,
            ConstraintZ = true
        };
        loaded.ApplyForce(10_000, 0, 0);
        solver.AddNode(loaded);
        solver.AddElement(new Element(1, 1, 2, 0.001, Material.StructuralSteel));

        var result = solver.Analyze();
        string pdfText = Encoding.UTF8.GetString(new PdfReportGenerator(result).GenerateReport());

        Assert.Contains("Design Criteria and Limitations", pdfText);
        Assert.Contains("Internal analysis units: m, N, N-m, Pa.", pdfText);
        Assert.Contains("Member Force Envelope", pdfText);
        Assert.Contains("Units: axial force=N, stress=MPa, utilization=ratio", pdfText);
        Assert.Contains("Element 1: N=", pdfText);
        Assert.Contains("Design checks are preliminary MVP checks", pdfText);
    }

    [Fact]
    public void AnalysisReportSnapshot_UsesStableAnalysisResultDto()
    {
        var result = new AnalysisResult
        {
            LoadCaseName = "Service",
            Nodes = new List<Node> { new(1, Point3D.Zero) },
            Elements = new List<Element>
            {
                new(3, 1, 1, 0.01, Material.StructuralSteel)
                {
                    AxialForce = 12_000,
                    Stress = 1.2e6
                }
            },
            SafetyChecks = new SafetyCheckSummary
            {
                ElementChecks = new List<ElementSafetyCheck>
                {
                    new()
                    {
                        ElementId = 3,
                        UtilizationRatio = 0.25,
                        Status = "OK",
                        IsPassing = true
                    }
                }
            },
            MaxDisplacement = 0.002,
            MaxAxialForce = 12_000,
            MaxStress = 1.2e6
        };

        var snapshot = AnalysisReportSnapshot.FromAnalysisResult(result);

        Assert.Equal("Service", snapshot.LoadCaseName);
        Assert.Equal(1, snapshot.NodeCount);
        Assert.Equal(1, snapshot.ElementCount);
        Assert.Equal(3, snapshot.MemberForceEnvelope.Single().ElementId);
        Assert.Equal(0.25, snapshot.MemberForceEnvelope.Single().Utilization, precision: 10);
        Assert.Contains(snapshot.Limitations, item => item.Contains("Linear elastic", StringComparison.Ordinal));
    }
}

