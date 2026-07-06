namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Design.Concrete;
using TrussAnalyzer.Core.Models;
using Xunit;

public class ConcreteDesignServiceTests
{
    [Fact]
    public void DesignFlexure_MissingRebarData_ReturnsMissingData()
    {
        var model = CreateConcreteModel(Section.RcRectangular(1, "RC", 0.3, 0.5));
        var forces = new ElementForceResult { ElementId = 1, MomentZ = 10_000 };

        var check = new ConcreteDesignService(model).DesignFlexure(forces);

        Assert.Equal("RC flexure", check.CheckType);
        Assert.Equal(DesignCheckStatus.MissingData, check.Status);
        Assert.Contains("Rebar area", check.Notes);
    }

    [Fact]
    public void DesignFlexure_LowMomentDemand_ReturnsOk()
    {
        var model = CreateConcreteModel(Section.RcRectangular(1, "RC", 0.3, 0.5, rebarArea: 0.002, effectiveDepth: 0.45));
        var forces = new ElementForceResult { ElementId = 1, MomentZ = 100_000 };

        var check = new ConcreteDesignService(model).DesignFlexure(forces);

        Assert.Equal(DesignCheckStatus.OK, check.Status);
        Assert.True(check.Capacity > check.Demand);
        Assert.Contains("Preliminary", check.Notes);
    }

    [Fact]
    public void DesignFlexure_HighMomentDemand_ReturnsNg()
    {
        var model = CreateConcreteModel(Section.RcRectangular(1, "RC", 0.3, 0.5, rebarArea: 0.0002, effectiveDepth: 0.40));
        var forces = new ElementForceResult { ElementId = 1, MomentY = 500_000 };

        var check = new ConcreteDesignService(model).DesignFlexure(forces);

        Assert.Equal(DesignCheckStatus.NG, check.Status);
        Assert.True(check.Utilization > 1);
    }

    [Fact]
    public void StructuralSolver_UsesConcreteDesignServiceForFlexure()
    {
        var model = CreateConcreteModel(Section.RcRectangular(1, "RC", 0.3, 0.5, rebarArea: 0.002, effectiveDepth: 0.45));
        model.Nodes.Single(n => n.Id == 2).ApplyForce(0, -10_000, 0);

        var solverResult = new StructuralSolver(model).Analyze();
        var serviceCheck = new ConcreteDesignService(model).DesignFlexure(solverResult.ElementResults.Single());
        var solverCheck = solverResult.DesignChecks.Single(c => c.CheckType == "RC flexure");

        Assert.Equal(ToComparable(serviceCheck), ToComparable(solverCheck));
    }

    private static StructuralModel CreateConcreteModel(Section section)
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.Concrete30MPa with { Id = 1, YieldStrength = 420e6 });
        model.Sections.Add(section);
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
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }

    private static string ToComparable(DesignCheckResult check)
    {
        return $"{check.ElementId}|{check.CheckType}|{check.Demand:E6}|{check.Capacity:E6}|{check.Utilization:E6}|{check.Status}|{check.Notes}";
    }
}

