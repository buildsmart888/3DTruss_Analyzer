namespace TrussAnalyzer.Core.Design.Foundation;

using TrussAnalyzer.Core.Models;

public sealed class GoPileCalculator
{
    public GoPileResult Calculate(GoPileInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var pilePositions = CreatePileLayout(input).ToList();
        var centroid = new Point3D(pilePositions.Average(p => p.X), pilePositions.Average(p => p.Y), 0);
        var relative = pilePositions
            .Select((p, index) => new { Id = index + 1, Absolute = p, X = p.X - centroid.X, Y = p.Y - centroid.Y })
            .ToList();

        double sumX2 = relative.Sum(p => p.X * p.X);
        double sumY2 = relative.Sum(p => p.Y * p.Y);
        double totalMomentX = input.MomentX + input.AxialCompression * input.ColumnOffsetY;
        double totalMomentY = input.MomentY - input.AxialCompression * input.ColumnOffsetX;
        double average = input.AxialCompression / relative.Count;

        var piles = relative.Select(p =>
        {
            double reaction = average;
            if (sumY2 > 1e-12)
                reaction += totalMomentX * p.Y / sumY2;
            if (sumX2 > 1e-12)
                reaction -= totalMomentY * p.X / sumX2;

            double tension = Math.Max(0, -reaction);
            return new PileReactionResult
            {
                Id = p.Id,
                Position = p.Absolute,
                Reaction = reaction,
                CompressionUtilization = input.PileCapacityCompression <= 0 ? double.PositiveInfinity : Math.Max(0, reaction) / input.PileCapacityCompression,
                TensionUtilization = input.PileCapacityTension <= 0 ? (tension > 1e-9 ? double.PositiveInfinity : 0) : tension / input.PileCapacityTension,
                CompressionPass = reaction <= input.PileCapacityCompression + 1e-9,
                TensionPass = tension <= input.PileCapacityTension + 1e-9
            };
        }).ToList();

        var notes = new List<string>
        {
            "Internal units are SI: m, N, Pa.",
            "Pile reaction sign convention: positive is compression, negative is uplift/tension.",
            "Moment convention: positive Mx increases compression at +Y; positive My increases compression at -X."
        };

        return new GoPileResult
        {
            Input = input,
            Piles = piles,
            PileCentroid = centroid,
            TotalCompression = piles.Sum(p => p.Reaction),
            AppliedMomentX = totalMomentX,
            AppliedMomentY = totalMomentY,
            MaxCompression = piles.Select(p => p.Reaction).DefaultIfEmpty(0).Max(),
            MaxTension = piles.Select(p => Math.Max(0, -p.Reaction)).DefaultIfEmpty(0).Max(),
            PileCapacityPass = piles.All(p => p.CompressionPass),
            UpliftPass = piles.All(p => p.TensionPass),
            ReinforcementX = DesignBottomReinforcement(input, "X", Math.Abs(totalMomentY), input.FootingWidthY),
            ReinforcementY = DesignBottomReinforcement(input, "Y", Math.Abs(totalMomentX), input.FootingLengthX),
            Notes = notes
        };
    }

    public IReadOnlyList<Point3D> CreatePileLayout(GoPileInput input)
    {
        double sx = input.PileSpacingX;
        double sy = input.PileSpacingY;
        return input.FoundationType switch
        {
            GoPileFoundationType.F1 => new[] { new Point3D(0, 0, 0) },
            GoPileFoundationType.F2 => new[] { new Point3D(-sx / 2, 0, 0), new Point3D(sx / 2, 0, 0) },
            GoPileFoundationType.F3 => new[] { new Point3D(-sx / 2, -sy / 3, 0), new Point3D(sx / 2, -sy / 3, 0), new Point3D(0, 2 * sy / 3, 0) },
            GoPileFoundationType.F4 => new[] { new Point3D(-sx / 2, -sy / 2, 0), new Point3D(sx / 2, -sy / 2, 0), new Point3D(sx / 2, sy / 2, 0), new Point3D(-sx / 2, sy / 2, 0) },
            GoPileFoundationType.F5 => new[] { new Point3D(-sx / 2, -sy / 2, 0), new Point3D(sx / 2, -sy / 2, 0), new Point3D(sx / 2, sy / 2, 0), new Point3D(-sx / 2, sy / 2, 0), new Point3D(0, 0, 0) },
            _ => throw new ArgumentOutOfRangeException(nameof(input.FoundationType), input.FoundationType, "Unsupported GO Pile foundation type.")
        };
    }

    private static ReinforcementDesignResult DesignBottomReinforcement(GoPileInput input, string direction, double designMoment, double stripWidth)
    {
        double effectiveDepth = input.FootingThickness - input.ConcreteCover - input.BarDiameter / 2.0;
        double leverArm = Math.Max(1e-9, 0.9 * effectiveDepth);
        double momentPerMeter = stripWidth <= 0 ? designMoment : designMoment / stripWidth;
        double required = momentPerMeter / Math.Max(1e-9, input.StrengthReductionFactor * input.RebarYieldStrength * leverArm);
        double barArea = Math.PI * input.BarDiameter * input.BarDiameter / 4.0;
        double spacing = required <= 1e-12 ? 0.25 : Math.Clamp(barArea / required, 0.075, 0.25);
        double provided = barArea / spacing;

        return new ReinforcementDesignResult
        {
            Direction = direction,
            DesignMoment = designMoment,
            EffectiveDepth = effectiveDepth,
            RequiredSteelAreaPerMeter = required,
            ProvidedSteelAreaPerMeter = provided,
            BarDiameter = input.BarDiameter,
            BarSpacing = spacing,
            IsPassing = provided + 1e-12 >= required
        };
    }

    private static void Validate(GoPileInput input)
    {
        if (input.AxialCompression < 0)
            throw new ArgumentException("Axial compression must be positive. Use moments/eccentricity for overturning effects.", nameof(input));
        if (input.PileSpacingX <= 0 || input.PileSpacingY <= 0)
            throw new ArgumentException("Pile spacing must be positive.", nameof(input));
        if (input.PileCapacityCompression <= 0)
            throw new ArgumentException("Pile compression capacity must be positive.", nameof(input));
        if (input.FootingThickness <= input.ConcreteCover + input.BarDiameter)
            throw new ArgumentException("Footing thickness must be larger than cover plus bar diameter.", nameof(input));
        if (input.RebarYieldStrength <= 0)
            throw new ArgumentException("Rebar yield strength must be positive.", nameof(input));
    }
}

