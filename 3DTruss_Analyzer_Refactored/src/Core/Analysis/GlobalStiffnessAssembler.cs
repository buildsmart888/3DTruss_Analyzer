namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Utilities;

public sealed class GlobalStiffnessAssembler
{
    public void Assemble(
        double[,] globalStiffness,
        double[,] localStiffness,
        double[,] transformation,
        IReadOnlyList<int> elementDofMap)
    {
        ArgumentNullException.ThrowIfNull(globalStiffness);
        ArgumentNullException.ThrowIfNull(localStiffness);
        ArgumentNullException.ThrowIfNull(transformation);
        ArgumentNullException.ThrowIfNull(elementDofMap);

        int globalSize = globalStiffness.GetLength(0);
        int localSize = localStiffness.GetLength(0);
        if (globalSize != globalStiffness.GetLength(1))
            throw new ArgumentException("Global stiffness matrix must be square.", nameof(globalStiffness));
        if (localSize != localStiffness.GetLength(1))
            throw new ArgumentException("Local stiffness matrix must be square.", nameof(localStiffness));
        if (transformation.GetLength(0) != localSize || transformation.GetLength(1) != localSize)
            throw new ArgumentException("Transformation matrix dimensions must match the local stiffness matrix.", nameof(transformation));
        if (elementDofMap.Count != localSize)
            throw new ArgumentException("Element DOF map length must match the local stiffness matrix.", nameof(elementDofMap));
        if (elementDofMap.Any(dof => dof < 0 || dof >= globalSize))
            throw new ArgumentOutOfRangeException(nameof(elementDofMap), "Element DOF map contains an index outside the global stiffness matrix.");

        var elementGlobalStiffness = Matrix.Multiply(
            Matrix.Multiply(Matrix.Transpose(transformation), localStiffness),
            transformation);

        for (int localRow = 0; localRow < localSize; localRow++)
        {
            for (int localColumn = 0; localColumn < localSize; localColumn++)
                globalStiffness[elementDofMap[localRow], elementDofMap[localColumn]] += elementGlobalStiffness[localRow, localColumn];
        }
    }
}
