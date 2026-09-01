namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using Xunit;

public class ReactionRecoveryServiceTests
{
    [Fact]
    public void RecoverNodeReaction_UsesOriginalStiffnessDisplacementAndLoadVector()
    {
        var stiffness = new double[6, 6];
        for (int index = 0; index < 6; index++)
            stiffness[index, index] = index + 2;
        var loads = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        var displacements = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };

        var reaction = new ReactionRecoveryService().RecoverNodeReaction(stiffness, loads, displacements, nodeDofBase: 0);

        Assert.Equal(new[] { 1.0, 4.0, 9.0, 16.0, 25.0, 36.0 }, reaction);
    }

    [Fact]
    public void RecoverNodeReaction_InvalidNodeDofBase_ThrowsClearError()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReactionRecoveryService().RecoverNodeReaction(new double[6, 6], new double[6], new double[6], nodeDofBase: 1));

        Assert.Equal("nodeDofBase", error.ParamName);
    }
}
