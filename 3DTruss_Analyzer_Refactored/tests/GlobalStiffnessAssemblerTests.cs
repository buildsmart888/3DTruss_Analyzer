namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using Xunit;

public class GlobalStiffnessAssemblerTests
{
    [Fact]
    public void Assemble_IdentityTransformation_AccumulatesLocalMatrixAtMappedDofs()
    {
        var global = new double[4, 4];
        global[2, 2] = 5;
        var local = new double[,]
        {
            { 10, -10 },
            { -10, 10 }
        };
        var identity = new double[,]
        {
            { 1, 0 },
            { 0, 1 }
        };

        new GlobalStiffnessAssembler().Assemble(global, local, identity, new[] { 2, 0 });

        Assert.Equal(15, global[2, 2]);
        Assert.Equal(-10, global[2, 0]);
        Assert.Equal(-10, global[0, 2]);
        Assert.Equal(10, global[0, 0]);
        Assert.Equal(0, global[1, 1]);
    }

    [Fact]
    public void Assemble_RotatedTransformation_UsesLocalToGlobalCongruenceTransform()
    {
        var global = new double[2, 2];
        var local = new double[,]
        {
            { 2, 0 },
            { 0, 3 }
        };
        var rotation = new double[,]
        {
            { 0, 1 },
            { -1, 0 }
        };

        new GlobalStiffnessAssembler().Assemble(global, local, rotation, new[] { 0, 1 });

        Assert.Equal(3, global[0, 0]);
        Assert.Equal(0, global[0, 1]);
        Assert.Equal(0, global[1, 0]);
        Assert.Equal(2, global[1, 1]);
    }

    [Fact]
    public void Assemble_MismatchedDofMap_ThrowsClearError()
    {
        var global = new double[3, 3];
        var local = new double[2, 2];
        var identity = new double[,]
        {
            { 1, 0 },
            { 0, 1 }
        };

        var error = Assert.Throws<ArgumentException>(() =>
            new GlobalStiffnessAssembler().Assemble(global, local, identity, new[] { 0 }));

        Assert.Contains("DOF map length", error.Message);
    }
}
