namespace TrussAnalyzer.Core.Analysis;

public sealed class BoundaryConditionApplier
{
    public void Apply(double[,] stiffness, double[] loadVector, IEnumerable<int> constrainedDofs)
    {
        ArgumentNullException.ThrowIfNull(constrainedDofs);
        Apply(stiffness, loadVector, constrainedDofs.Distinct().ToDictionary(dof => dof, _ => 0.0));
    }

    public void Apply(double[,] stiffness, double[] loadVector, IReadOnlyDictionary<int, double> prescribedDofValues)
    {
        ArgumentNullException.ThrowIfNull(stiffness);
        ArgumentNullException.ThrowIfNull(loadVector);
        ArgumentNullException.ThrowIfNull(prescribedDofValues);

        int rowCount = stiffness.GetLength(0);
        int columnCount = stiffness.GetLength(1);
        if (rowCount != columnCount || rowCount != loadVector.Length)
            throw new ArgumentException("Stiffness matrix must be square and match the load vector length.");

        foreach (var prescribedDof in prescribedDofValues.OrderBy(entry => entry.Key))
            ApplyConstraint(stiffness, loadVector, prescribedDof.Key, prescribedDof.Value);
    }

    private static void ApplyConstraint(double[,] stiffness, double[] loadVector, int dof, double prescribedValue)
    {
        int totalDof = loadVector.Length;
        if (dof < 0 || dof >= totalDof)
            throw new ArgumentOutOfRangeException(nameof(dof));

        for (int i = 0; i < totalDof; i++)
        {
            if (i != dof)
                loadVector[i] -= stiffness[i, dof] * prescribedValue;
        }

        for (int i = 0; i < totalDof; i++)
        {
            stiffness[dof, i] = 0;
            stiffness[i, dof] = 0;
        }

        stiffness[dof, dof] = 1;
        loadVector[dof] = prescribedValue;
    }
}
