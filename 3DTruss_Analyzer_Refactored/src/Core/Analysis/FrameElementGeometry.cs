namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;

public sealed record FrameElementGeometry(
    Point3D StartConnection,
    Point3D EndConnection,
    LocalAxes LocalAxes,
    double Length,
    double[,] AnalysisTransformation);

public static class FrameElementGeometryResolver
{
    public static FrameElementGeometry Resolve(StructuralElement element, Node startNode, Node endNode)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(startNode);
        ArgumentNullException.ThrowIfNull(endNode);

        var nodeAxes = FrameCoordinateSystem.GetLocalAxes(startNode.Position, endNode.Position, element.RollAngleRadians);
        if (element.Type != ElementType.Frame3D)
        {
            double trussLength = startNode.Position.DistanceTo(endNode.Position);
            return new FrameElementGeometry(
                startNode.Position,
                endNode.Position,
                nodeAxes,
                trussLength,
                FrameCoordinateSystem.BuildTransformation(startNode.Position, endNode.Position, element.RollAngleRadians));
        }

        var startOffset = element.StartInsertionPointLocal.Add(new Vector3D(element.StartRigidEndOffset, 0, 0));
        var endOffset = element.EndInsertionPointLocal.Add(new Vector3D(-element.EndRigidEndOffset, 0, 0));
        var startConnection = Offset(startNode.Position, ToGlobal(startOffset, nodeAxes));
        var endConnection = Offset(endNode.Position, ToGlobal(endOffset, nodeAxes));
        double length = startConnection.DistanceTo(endConnection);
        if (length < 1e-10)
            throw new InvalidOperationException($"Frame element {element.Id} has zero or negative flexible length after rigid-end/insertion offsets.");

        var axes = FrameCoordinateSystem.GetLocalAxes(startConnection, endConnection, element.RollAngleRadians);
        var localTransformation = FrameCoordinateSystem.BuildTransformation(startConnection, endConnection, element.RollAngleRadians);
        var connectionKinematics = BuildConnectionKinematics(
            startConnection.Subtract(startNode.Position),
            endConnection.Subtract(endNode.Position));

        return new FrameElementGeometry(
            startConnection,
            endConnection,
            axes,
            length,
            Matrix.Multiply(localTransformation, connectionKinematics));
    }

    private static double[,] BuildConnectionKinematics(Vector3D startOffset, Vector3D endOffset)
    {
        var kinematics = Matrix.CreateIdentity(12);
        AddTranslationRotationCoupling(kinematics, 0, startOffset);
        AddTranslationRotationCoupling(kinematics, 6, endOffset);
        return kinematics;
    }

    private static void AddTranslationRotationCoupling(double[,] matrix, int offset, Vector3D arm)
    {
        // u_connection = u_node + theta x arm = u_node - skew(arm) theta.
        matrix[offset, offset + 4] = arm.Z;
        matrix[offset, offset + 5] = -arm.Y;
        matrix[offset + 1, offset + 3] = -arm.Z;
        matrix[offset + 1, offset + 5] = arm.X;
        matrix[offset + 2, offset + 3] = arm.Y;
        matrix[offset + 2, offset + 4] = -arm.X;
    }

    private static Vector3D ToGlobal(Vector3D local, LocalAxes axes) =>
        axes.XAxis.Scale(local.X).Add(axes.YAxis.Scale(local.Y)).Add(axes.ZAxis.Scale(local.Z));

    private static Point3D Offset(Point3D point, Vector3D vector) =>
        new(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);
}
