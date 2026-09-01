namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class MechanismDiagnosticsServiceTests
{
    [Fact]
    public void Analyze_ZeroStiffnessRow_ReportsNodeAndDof()
    {
        var nodes = CreateNodes();
        var stiffness = CreateIdentityMatrix(12);
        for (int column = 0; column < 12; column++)
            stiffness[7, column] = 0;

        var report = new MechanismDiagnosticsService().Analyze(stiffness, nodes);

        var suspect = Assert.Single(report.SuspectDofs);
        Assert.Equal(7, suspect.GlobalDof);
        Assert.Equal(2, suspect.NodeId);
        Assert.Equal("UY", suspect.DegreeOfFreedom);
        Assert.Contains("No effective stiffness", suspect.Reason);
        Assert.Equal(7, report.FailedPivotDof);
    }

    [Fact]
    public void Analyze_RankDeficientPivot_ReportsMappedDof()
    {
        var nodes = new[] { new Node(10, new Point3D(0, 0, 0)) };
        var stiffness = CreateIdentityMatrix(6);
        stiffness[0, 0] = 1;
        stiffness[0, 1] = 1;
        stiffness[1, 0] = 1;
        stiffness[1, 1] = 1;

        var report = new MechanismDiagnosticsService().Analyze(stiffness, nodes);

        Assert.Equal(1, report.FailedPivotDof);
        var suspect = Assert.Single(report.SuspectDofs);
        Assert.Equal(10, suspect.NodeId);
        Assert.Equal("UY", suspect.DegreeOfFreedom);
        Assert.Contains("Rank-deficient", suspect.Reason);
    }

    [Fact]
    public void StructuralSolver_UnstableTruss_ThrowsDiagnosticExceptionWithSuspectDofs()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Truss", 0.003, 4e-6, 6e-6, 2e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0)));
        model.Elements.Add(new TrussElement(1, 1, 2, materialId: 1, sectionId: 1));

        var error = Assert.Throws<StructuralInstabilityException>(() => new StructuralSolver(model).Analyze());

        Assert.Contains("Node 2 UY", error.Message);
        Assert.Contains(error.Diagnostics.SuspectDofs, diagnostic => diagnostic.NodeId == 2 && diagnostic.DegreeOfFreedom == "UY");
    }

    private static Node[] CreateNodes()
    {
        return new[]
        {
            new Node(1, new Point3D(0, 0, 0)),
            new Node(2, new Point3D(1, 0, 0))
        };
    }

    private static double[,] CreateIdentityMatrix(int size)
    {
        var matrix = new double[size, size];
        for (int index = 0; index < size; index++)
            matrix[index, index] = 1;
        return matrix;
    }
}
