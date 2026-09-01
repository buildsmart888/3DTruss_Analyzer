namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public static class FrameCoordinateSystem
{
    public static LocalAxes GetLocalAxes(Point3D start, Point3D end, double rollAngleRadians = 0)
    {
        var x = end.Subtract(start).Normalize();
        var reference = Math.Abs(x.Dot(new Vector3D(0, 0, 1))) > 0.95
            ? new Vector3D(0, 1, 0)
            : new Vector3D(0, 0, 1);
        var y = reference.Cross(x).Normalize();
        var z = x.Cross(y).Normalize();

        if (Math.Abs(rollAngleRadians) > 1e-12)
        {
            double cosine = Math.Cos(rollAngleRadians);
            double sine = Math.Sin(rollAngleRadians);
            y = y.Scale(cosine).Add(z.Scale(sine)).Normalize();
            z = x.Cross(y).Normalize();
        }

        return new LocalAxes(x, y, z);
    }

    public static double[,] BuildTransformation(Point3D start, Point3D end, double rollAngleRadians = 0)
    {
        var axes = GetLocalAxes(start, end, rollAngleRadians);
        var rotation = new[,]
        {
            { axes.XAxis.X, axes.XAxis.Y, axes.XAxis.Z },
            { axes.YAxis.X, axes.YAxis.Y, axes.YAxis.Z },
            { axes.ZAxis.X, axes.ZAxis.Y, axes.ZAxis.Z }
        };
        var transformation = new double[12, 12];
        for (int block = 0; block < 4; block++)
        {
            int offset = block * 3;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    transformation[offset + row, offset + column] = rotation[row, column];
            }
        }

        return transformation;
    }
}
