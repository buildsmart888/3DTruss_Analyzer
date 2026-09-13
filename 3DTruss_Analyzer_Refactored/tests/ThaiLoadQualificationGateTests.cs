namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1;
using Xunit;

public sealed class ThaiLoadQualificationGateTests
{
    [Fact]
    public void BenchmarkIsDeterministicAndNeverAdvertisesQualificationWithoutReview()
    {
        var document = new ProjectDocument { Model = new Model3D { Nodes = new() { new Node3D(), new Node3D() } } };
        var gate = new ThaiLoadQualificationGate();
        var first = gate.Evaluate(document, 1.5, .1); var second = gate.Evaluate(document, 1.5, .1);
        Assert.Equal("PRELIMINARY", first.Status); Assert.Equal(first.WindResultant, second.WindResultant); Assert.Equal(ThaiLoadQualificationGate.CodeBasis, first.CodeBasis);
    }
}
