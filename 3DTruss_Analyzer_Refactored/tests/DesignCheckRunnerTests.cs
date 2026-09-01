namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Design;
using TrussAnalyzer.Core.Design.Steel;
using TrussAnalyzer.Core.Models;
using Xunit;

public class DesignCheckRunnerTests
{
    [Fact]
    public void Run_SteelMember_MatchesSteelDesignServiceChecks()
    {
        var model = CreateSteelModel();
        var forces = new ElementForceResult { ElementId = 1, AxialForce = 1_000, MomentZ = 100 };

        var runnerChecks = new DesignCheckRunner(model).Run(new[] { forces });
        var serviceChecks = new SteelDesignService(model).DesignElement(forces);

        Assert.Equal(serviceChecks.Select(ToComparable), runnerChecks.Select(ToComparable));
    }

    [Fact]
    public void Run_ConcreteMember_ReturnsAxialFlexureAndShearChecksInExistingOrder()
    {
        var model = CreateConcreteModel();
        var forces = new ElementForceResult { ElementId = 1, AxialForce = 60_000, ShearY = 10_000, MomentZ = 100_000 };

        var checks = new DesignCheckRunner(model).Run(new[] { forces });

        Assert.Equal(new[] { "RC axial stress", "RC flexure", "RC shear" }, checks.Select(check => check.CheckType));
        Assert.All(checks, check => Assert.Equal(DesignCheckStatus.OK, check.Status));
        Assert.Contains(checks, check => check.Notes.Contains("Preliminary"));
    }

    private static StructuralModel CreateSteelModel()
    {
        var model = CreateBaseModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Steel", 0.003, 4e-6, 6e-6, 2e-6));
        return model;
    }

    private static StructuralModel CreateConcreteModel()
    {
        var model = CreateBaseModel();
        model.Materials.Add(Material.Concrete30MPa with { Id = 1, YieldStrength = 420e6 });
        model.Sections.Add(Section.RcRectangular(1, "RC", 0.3, 0.5, rebarArea: 0.002, effectiveDepth: 0.45));
        return model;
    }

    private static StructuralModel CreateBaseModel()
    {
        var model = new StructuralModel();
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)));
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0)));
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }

    private static string ToComparable(DesignCheckResult check)
    {
        return $"{check.ElementId}|{check.CheckType}|{check.Demand:E6}|{check.Capacity:E6}|{check.Utilization:E6}|{check.Status}|{check.Notes}";
    }
}
