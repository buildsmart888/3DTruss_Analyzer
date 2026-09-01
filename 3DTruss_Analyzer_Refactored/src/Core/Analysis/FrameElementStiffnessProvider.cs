namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;

public sealed class FrameElementStiffnessProvider
{
    private readonly FrameAnalysisOptions _options;

    public FrameElementStiffnessProvider(FrameAnalysisOptions? options = null)
    {
        _options = options ?? new FrameAnalysisOptions();
    }

    public double[,] BuildLocalStiffness(StructuralElement element, Material material, Section section, double length)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(section);

        var stiffness = element.Type == ElementType.Truss
            ? BuildTrussLocalStiffness(material, section, length)
            : BuildFrameLocalStiffness(material, section, length, element.Id);

        ApplyFrameReleases(stiffness, element);
        return stiffness;
    }

    public double[] CondenseEquivalentLocalLoad(
        StructuralElement element,
        Material material,
        Section section,
        double length,
        double[] equivalentLocalLoad)
    {
        ArgumentNullException.ThrowIfNull(equivalentLocalLoad);
        if (equivalentLocalLoad.Length != 12)
            throw new ArgumentException("Equivalent member load must have 12 local DOF entries.", nameof(equivalentLocalLoad));
        if (element.Type != ElementType.Frame3D || !element.Releases.HasAny)
            return (double[])equivalentLocalLoad.Clone();

        var unreleasedStiffness = BuildFrameLocalStiffness(material, section, length, element.Id);
        var releasedDofs = GetReleasedDofs(element).ToArray();
        var retainedDofs = Enumerable.Range(0, 12).Except(releasedDofs).ToArray();
        var releasedStiffness = new double[releasedDofs.Length, releasedDofs.Length];
        var releasedLoads = new double[releasedDofs.Length];
        for (int row = 0; row < releasedDofs.Length; row++)
        {
            releasedLoads[row] = equivalentLocalLoad[releasedDofs[row]];
            for (int column = 0; column < releasedDofs.Length; column++)
                releasedStiffness[row, column] = unreleasedStiffness[releasedDofs[row], releasedDofs[column]];
        }

        var releasedResponse = Matrix.Solve(releasedStiffness, releasedLoads);
        var condensed = (double[])equivalentLocalLoad.Clone();
        foreach (int retainedDof in retainedDofs)
        {
            for (int releasedIndex = 0; releasedIndex < releasedDofs.Length; releasedIndex++)
                condensed[retainedDof] -= unreleasedStiffness[retainedDof, releasedDofs[releasedIndex]] * releasedResponse[releasedIndex];
        }

        foreach (int releasedDof in releasedDofs)
            condensed[releasedDof] = 0;
        return condensed;
    }

    private static double[,] BuildTrussLocalStiffness(Material material, Section section, double length)
    {
        if (section.Area <= 0)
            throw new InvalidOperationException("Truss section area must be positive.");

        var stiffness = new double[12, 12];
        double axial = material.YoungsModulus * section.Area / length;
        stiffness[0, 0] = axial;
        stiffness[0, 6] = -axial;
        stiffness[6, 0] = -axial;
        stiffness[6, 6] = axial;
        return stiffness;
    }

    private double[,] BuildFrameLocalStiffness(Material material, Section section, double length, int elementId)
    {
        section.ValidateForAnalysis(elementId);
        var stiffness = new double[12, 12];
        double lengthSquared = length * length;
        double lengthCubed = lengthSquared * length;
        double youngsModulus = material.YoungsModulus;
        double shearModulus = material.EffectiveShearModulus;

        double axial = youngsModulus * section.Area / length;
        stiffness[0, 0] = axial;
        stiffness[0, 6] = -axial;
        stiffness[6, 0] = -axial;
        stiffness[6, 6] = axial;

        double torsion = shearModulus * section.J / length;
        stiffness[3, 3] = torsion;
        stiffness[3, 9] = -torsion;
        stiffness[9, 3] = -torsion;
        stiffness[9, 9] = torsion;

        double phiY = GetShearDeformationParameter(youngsModulus * section.Iz, shearModulus, section.Area, lengthSquared, _options.ShearCorrectionFactorY);
        double phiZ = GetShearDeformationParameter(youngsModulus * section.Iy, shearModulus, section.Area, lengthSquared, _options.ShearCorrectionFactorZ);
        AddBending(stiffness, 1, 5, 7, 11, youngsModulus * section.Iz, length, lengthSquared, lengthCubed, phiY, positiveCoupling: true);
        AddBending(stiffness, 2, 4, 8, 10, youngsModulus * section.Iy, length, lengthSquared, lengthCubed, phiZ, positiveCoupling: false);
        Symmetrize(stiffness);
        return stiffness;
    }

    private static void AddBending(
        double[,] stiffness,
        int v1,
        int r1,
        int v2,
        int r2,
        double flexuralRigidity,
        double length,
        double lengthSquared,
        double lengthCubed,
        double shearDeformationParameter,
        bool positiveCoupling)
    {
        double denominator = 1 + shearDeformationParameter;
        double a = 12 * flexuralRigidity / (lengthCubed * denominator);
        double b = 6 * flexuralRigidity / (lengthSquared * denominator);
        double c = (4 + shearDeformationParameter) * flexuralRigidity / (length * denominator);
        double d = (2 - shearDeformationParameter) * flexuralRigidity / (length * denominator);
        double sign = positiveCoupling ? 1.0 : -1.0;

        stiffness[v1, v1] += a;
        stiffness[v1, r1] += sign * b;
        stiffness[v1, v2] += -a;
        stiffness[v1, r2] += sign * b;
        stiffness[r1, r1] += c;
        stiffness[r1, v2] += -sign * b;
        stiffness[r1, r2] += d;
        stiffness[v2, v2] += a;
        stiffness[v2, r2] += -sign * b;
        stiffness[r2, r2] += c;
    }

    private double GetShearDeformationParameter(
        double flexuralRigidity,
        double shearModulus,
        double area,
        double lengthSquared,
        double shearCorrectionFactor)
    {
        if (!_options.UsesTimoshenkoShearDeformation)
            return 0;
        if (shearCorrectionFactor <= 0)
            throw new InvalidOperationException("Timoshenko shear correction factors must be positive.");

        return 12 * flexuralRigidity / (shearCorrectionFactor * shearModulus * area * lengthSquared);
    }

    private static void Symmetrize(double[,] matrix)
    {
        int size = matrix.GetLength(0);
        for (int i = 0; i < size; i++)
        {
            for (int j = i + 1; j < size; j++)
                matrix[j, i] = matrix[i, j];
        }
    }

    private static void ApplyFrameReleases(double[,] localStiffness, StructuralElement element)
    {
        if (element.Type != ElementType.Frame3D || !element.Releases.HasAny)
            return;

        var releasedDofs = GetReleasedDofs(element).ToArray();
        var retainedDofs = Enumerable.Range(0, 12).Except(releasedDofs).ToArray();
        var releasedStiffness = new double[releasedDofs.Length, releasedDofs.Length];
        for (int row = 0; row < releasedDofs.Length; row++)
        {
            for (int column = 0; column < releasedDofs.Length; column++)
                releasedStiffness[row, column] = localStiffness[releasedDofs[row], releasedDofs[column]];
        }

        var condensed = (double[,])localStiffness.Clone();
        foreach (int retainedRow in retainedDofs)
        {
            for (int retainedColumn = 0; retainedColumn < retainedDofs.Length; retainedColumn++)
            {
                int columnDof = retainedDofs[retainedColumn];
                var releasedColumn = releasedDofs.Select(dof => localStiffness[dof, columnDof]).ToArray();
                var releasedResponse = Matrix.Solve(releasedStiffness, releasedColumn);
                double correction = 0;
                for (int releasedIndex = 0; releasedIndex < releasedDofs.Length; releasedIndex++)
                    correction += localStiffness[retainedRow, releasedDofs[releasedIndex]] * releasedResponse[releasedIndex];
                condensed[retainedRow, columnDof] -= correction;
            }
        }

        for (int row = 0; row < 12; row++)
        {
            for (int column = 0; column < 12; column++)
                localStiffness[row, column] = condensed[row, column];
        }

        foreach (int dof in releasedDofs)
            ReleaseLocalDof(localStiffness, dof);
    }

    private static IEnumerable<int> GetReleasedDofs(StructuralElement element)
    {
        if (element.Releases.StartMomentY) yield return 4;
        if (element.Releases.StartMomentZ) yield return 5;
        if (element.Releases.EndMomentY) yield return 10;
        if (element.Releases.EndMomentZ) yield return 11;
    }

    private static void ReleaseLocalDof(double[,] matrix, int dof)
    {
        int size = matrix.GetLength(0);
        for (int i = 0; i < size; i++)
        {
            matrix[dof, i] = 0;
            matrix[i, dof] = 0;
        }
    }
}
