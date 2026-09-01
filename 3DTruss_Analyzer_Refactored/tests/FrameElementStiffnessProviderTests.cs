namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class FrameElementStiffnessProviderTests
{
    [Fact]
    public void BuildLocalStiffness_TrussElement_ReturnsExpectedAxialTerms()
    {
        var material = Material.StructuralSteel;
        var section = Section.Generic(1, "Truss", 0.003, 1e-6, 1e-6, 1e-6);
        var element = new TrussElement(7, 1, 2, materialId: 1, sectionId: 1);

        var stiffness = new FrameElementStiffnessProvider().BuildLocalStiffness(element, material, section, length: 3);

        double axial = material.YoungsModulus * section.Area / 3;
        Assert.Equal(axial, stiffness[0, 0], precision: 6);
        Assert.Equal(-axial, stiffness[0, 6], precision: 6);
        Assert.Equal(-axial, stiffness[6, 0], precision: 6);
        Assert.Equal(axial, stiffness[6, 6], precision: 6);
        Assert.Equal(0, stiffness[1, 1]);
    }

    [Fact]
    public void BuildLocalStiffness_FrameElement_ReturnsSymmetricEulerBernoulliTerms()
    {
        var material = Material.StructuralSteel;
        var section = Section.Generic(1, "Frame", 0.003, 4e-6, 6e-6, 2e-6);
        var element = new FrameElement3D(8, 1, 2, materialId: 1, sectionId: 1);
        const double length = 3;

        var stiffness = new FrameElementStiffnessProvider().BuildLocalStiffness(element, material, section, length);

        double yBending = 12 * material.YoungsModulus * section.Iz / Math.Pow(length, 3);
        double yRotationCoupling = 6 * material.YoungsModulus * section.Iz / Math.Pow(length, 2);
        double zRotationCoupling = -6 * material.YoungsModulus * section.Iy / Math.Pow(length, 2);
        double torsion = material.EffectiveShearModulus * section.J / length;

        Assert.Equal(material.YoungsModulus * section.Area / length, stiffness[0, 0], precision: 6);
        Assert.Equal(torsion, stiffness[3, 3], precision: 6);
        Assert.Equal(yBending, stiffness[1, 1], precision: 6);
        Assert.Equal(yRotationCoupling, stiffness[1, 5], precision: 6);
        Assert.Equal(zRotationCoupling, stiffness[2, 4], precision: 6);
        Assert.Equal(stiffness[1, 5], stiffness[5, 1], precision: 12);
        Assert.Equal(stiffness[2, 4], stiffness[4, 2], precision: 12);
    }

    [Fact]
    public void BuildLocalStiffness_FrameMomentRelease_ZeroesReleasedDof()
    {
        var material = Material.StructuralSteel;
        var section = Section.Generic(1, "Frame", 0.003, 4e-6, 6e-6, 2e-6);
        var element = new FrameElement3D(9, 1, 2, materialId: 1, sectionId: 1)
        {
            Releases = new FrameMemberRelease { StartMomentY = true }
        };

        var stiffness = new FrameElementStiffnessProvider().BuildLocalStiffness(element, material, section, length: 3);

        Assert.All(Enumerable.Range(0, 12), index => Assert.Equal(0, stiffness[4, index]));
        Assert.All(Enumerable.Range(0, 12), index => Assert.Equal(0, stiffness[index, 4]));
    }

    [Fact]
    public void BuildLocalStiffness_EndMomentRelease_UsesStaticCondensationForRemainingDofs()
    {
        var material = Material.StructuralSteel;
        var section = Section.Generic(1, "Frame", 0.003, 4e-6, 6e-6, 2e-6);
        var element = new FrameElement3D(10, 1, 2, materialId: 1, sectionId: 1)
        {
            Releases = new FrameMemberRelease { EndMomentZ = true }
        };

        var stiffness = new FrameElementStiffnessProvider().BuildLocalStiffness(element, material, section, length: 3);

        double expected = 3 * material.YoungsModulus * section.Iz / Math.Pow(3, 3);
        Assert.Equal(expected, stiffness[1, 1], precision: 6);
        Assert.All(Enumerable.Range(0, 12), index => Assert.Equal(0, stiffness[11, index]));
    }

    [Fact]
    public void BuildLocalStiffness_FrameWithInvalidSection_PreservesValidationMessage()
    {
        var material = Material.StructuralSteel;
        var section = Section.Generic(1, "Invalid", 0, 4e-6, 6e-6, 2e-6);
        var element = new FrameElement3D(12, 1, 2, materialId: 1, sectionId: 1);

        var error = Assert.Throws<InvalidOperationException>(() =>
            new FrameElementStiffnessProvider().BuildLocalStiffness(element, material, section, length: 3));

        Assert.Contains("Element 12", error.Message);
    }
}
