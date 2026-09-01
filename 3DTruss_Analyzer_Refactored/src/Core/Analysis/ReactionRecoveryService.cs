namespace TrussAnalyzer.Core.Analysis;

public sealed class ReactionRecoveryService
{
    public double[] RecoverNodeReaction(double[,] originalStiffness, double[] originalLoadVector, double[] displacements, int nodeDofBase)
    {
        ArgumentNullException.ThrowIfNull(originalStiffness);
        ArgumentNullException.ThrowIfNull(originalLoadVector);
        ArgumentNullException.ThrowIfNull(displacements);

        if (originalStiffness.GetLength(0) != originalLoadVector.Length || originalStiffness.GetLength(1) != displacements.Length)
            throw new ArgumentException("Original stiffness matrix dimensions must match the load vector and displacement vector.");
        if (nodeDofBase < 0 || nodeDofBase + DofIndexer.DofPerNode > originalLoadVector.Length)
            throw new ArgumentOutOfRangeException(nameof(nodeDofBase));

        var reaction = new double[DofIndexer.DofPerNode];
        for (int localDof = 0; localDof < reaction.Length; localDof++)
        {
            int row = nodeDofBase + localDof;
            double internalForce = 0;
            for (int column = 0; column < displacements.Length; column++)
                internalForce += originalStiffness[row, column] * displacements[column];
            reaction[localDof] = internalForce - originalLoadVector[row];
        }

        return reaction;
    }
}
