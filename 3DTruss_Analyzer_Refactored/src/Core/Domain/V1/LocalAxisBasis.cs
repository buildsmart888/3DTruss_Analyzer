namespace TrussAnalyzer.Core.Domain.V1;

public readonly record struct LocalAxisBasis(Vector3DValue X, Vector3DValue Y, Vector3DValue Z)
{
    public static LocalAxisBasis Create(Point3DValue start, Point3DValue end, LocalAxisReference localAxis)
    {
        var x = start.VectorTo(end).Normalize();
        var reference = localAxis.ReferenceVector.Normalize();
        var yUnrotated = reference.Cross(x);
        if (yUnrotated.Magnitude <= 1e-10)
            throw new InvalidOperationException("The local-axis reference vector is parallel or too close to the member axis.");

        var y = yUnrotated.Normalize();
        var z = x.Cross(y).Normalize();
        if (Math.Abs(localAxis.RollRadians) <= 1e-15)
            return new LocalAxisBasis(x, y, z);

        double c = Math.Cos(localAxis.RollRadians);
        double s = Math.Sin(localAxis.RollRadians);
        return new LocalAxisBasis(
            x,
            new Vector3DValue(y.X * c + z.X * s, y.Y * c + z.Y * s, y.Z * c + z.Z * s),
            new Vector3DValue(-y.X * s + z.X * c, -y.Y * s + z.Y * c, -y.Z * s + z.Z * c));
    }
}
