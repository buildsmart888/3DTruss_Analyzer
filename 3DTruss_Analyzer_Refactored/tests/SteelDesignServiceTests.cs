namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Design.Steel;
using TrussAnalyzer.Core.Models;
using Xunit;

public class SteelDesignServiceTests
{
    [Fact]
    public void DesignElement_LowDemandSteelMember_ReturnsOkChecks()
    {
        var model = CreateSteelCantilever(area: 0.003);
        var forces = new ElementForceResult
        {
            ElementId = 1,
            AxialForce = 1_000,
            ShearY = 100,
            MomentZ = 100
        };

        var checks = new SteelDesignService(model).DesignElement(forces);

        Assert.Equal(5, checks.Count);
        Assert.All(checks, check => Assert.Equal(DesignCheckStatus.OK, check.Status));
        Assert.Contains(checks, c => c.CheckType == "Steel tension/yield");
        Assert.Contains(checks, c => c.CheckType == "Steel compression buckling");
        Assert.Contains(checks, c => c.CheckType == "Axial + bending");
    }

    [Fact]
    public void DesignElement_HighDemandSteelMember_ReturnsNgChecks()
    {
        var model = CreateSteelCantilever(area: 1e-5);
        var forces = new ElementForceResult
        {
            ElementId = 1,
            AxialForce = 100_000,
            ShearY = 30_000,
            MomentZ = 5_000
        };

        var checks = new SteelDesignService(model).DesignElement(forces);

        Assert.Contains(checks, c => c.Status == DesignCheckStatus.NG);
        Assert.Contains(checks, c => c.CheckType == "Steel tension/yield" && c.Utilization > 1);
    }

    [Fact]
    public void StructuralSolver_UsesSteelDesignServiceOutputShape()
    {
        var model = CreateSteelCantilever(area: 0.003);
        model.Nodes.Single(n => n.Id == 2).ApplyForce(1_000, 0, 0);

        var solverResult = new StructuralSolver(model).Analyze();
        var serviceChecks = new SteelDesignService(model).Design(solverResult.ElementResults);

        Assert.Equal(
            serviceChecks.Select(ToComparable),
            solverResult.DesignChecks.Select(ToComparable));
    }

    private static StructuralModel CreateSteelCantilever(double area)
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(new Section
        {
            Id = 1,
            Name = "Steel test section",
            Type = SectionType.Generic,
            Area = area,
            Iy = 6e-6,
            Iz = 8e-6,
            J = 2e-6,
            Width = 0.2,
            Depth = 0.3
        });

        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Nodes.Add(new Node(2, new Point3D(1, 0, 0))
        {
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }

    private static string ToComparable(DesignCheckResult check)
    {
        return $"{check.ElementId}|{check.CheckType}|{check.Demand:E6}|{check.Capacity:E6}|{check.Utilization:E6}|{check.Status}|{check.Notes}";
    }
}

