namespace TrussAnalyzer.Tests;

using System.Diagnostics;
using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;
using Xunit;

public sealed class MilestoneGQualificationTests
{
    [Fact]
    public void OpenSeesExporter_ProducesDeterministicBenchmarkContract()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Bar", .01, 1e-6, 1e-6, 1e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true, ConstraintY = true, ConstraintZ = true });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0)) { AppliedForce = new Vector3D(1000, 0, 0) });
        model.Elements.Add(new TrussElement(1, 1, 2, 1, 1));
        model.LoadCases.Add(new LoadCase { CaseId = "L", NodeForces = { [2] = new ForceVector(1000, 0, 0) } });

        var exporter = new OpenSeesTclExporter();
        var first = exporter.Export(model, "L");
        var second = exporter.Export(model, "L");

        Assert.Equal(first, second);
        Assert.Contains("model BasicBuilder -ndm 3 -ndf 6", first);
        Assert.Contains("element truss 1 1 2", first);
        Assert.Contains("pattern Plain 1 Linear", first);
    }

    [Fact]
    public void SparsePrototype_MatchesDenseOnLargePositiveDefiniteSystem()
    {
        const int size = 300;
        var matrix = new double[size, size];
        var rhs = new double[size];
        for (int i = 0; i < size; i++)
        {
            matrix[i, i] = 4;
            if (i > 0) matrix[i, i - 1] = matrix[i - 1, i] = -1;
            rhs[i] = i + 1;
        }

        var stopwatch = Stopwatch.StartNew();
        var dense = new DenseLinearSystemSolver().Solve(matrix, rhs);
        var sparse = new SparsePrototypeLinearSystemSolver().Solve(matrix, rhs);
        stopwatch.Stop();

        Assert.Equal(dense.Length, sparse.Length);
        for (int i = 0; i < size; i++) Assert.Equal(dense[i], sparse[i], precision: 10);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
    }
}
