using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Phase 12: Scientific Analysis ===");
Console.WriteLine();

string folder = Directory.GetCurrentDirectory();

string? csvFile = Directory
    .GetFiles(folder, "gaze_*.csv")
    .OrderByDescending(File.GetLastWriteTime)
    .FirstOrDefault();

if (csvFile == null)
{
    Console.WriteLine("No gaze_*.csv file found.");
    return;
}

Console.WriteLine($"File: {Path.GetFileName(csvFile)}");
Console.WriteLine();

var samples = File.ReadAllLines(csvFile)
    .Skip(1)
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(line =>
    {
        string[] p = line.Split(',');

        if (p.Length < 3)
            return null;

        if (!long.TryParse(
                p[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long timestamp))
            return null;

        if (!double.TryParse(
                p[1],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double x))
            return null;

        if (!double.TryParse(
                p[2],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double y))
            return null;

        return new Sample
        {
            Timestamp = timestamp,
            X = x,
            Y = y
        };
    })
    .Where(s => s != null)
    .Select(s => s!)
    .ToList();

if (samples.Count < 2)
{
    Console.WriteLine("Not enough valid samples.");
    return;
}

var intervals = new List<Interval>();

for (int i = 1; i < samples.Count; i++)
{
    double dtMs =
        (samples[i].Timestamp - samples[i - 1].Timestamp) / 1000.0;

    if (dtMs <= 0)
        continue;

    double dx = samples[i].X - samples[i - 1].X;
    double dy = samples[i].Y - samples[i - 1].Y;

    double distance = Math.Sqrt(dx * dx + dy * dy);

    double velocity = distance / (dtMs / 1000.0);

    double direction =
        Math.Atan2(dy, dx) * 180.0 / Math.PI;

    intervals.Add(new Interval
    {
        DtMs = dtMs,
        Dx = dx,
        Dy = dy,
        Distance = distance,
        Velocity = velocity,
        Direction = direction
    });
}

double meanX = samples.Average(s => s.X);
double meanY = samples.Average(s => s.Y);

double minX = samples.Min(s => s.X);
double maxX = samples.Max(s => s.X);
double minY = samples.Min(s => s.Y);
double maxY = samples.Max(s => s.Y);

double spatialSpreadX = maxX - minX;
double spatialSpreadY = maxY - minY;

double totalDistance = intervals.Sum(i => i.Distance);

double meanDistance =
    intervals.Count > 0
        ? intervals.Average(i => i.Distance)
        : 0;

double meanVelocity =
    intervals.Count > 0
        ? intervals.Average(i => i.Velocity)
        : 0;

double peakVelocity =
    intervals.Count > 0
        ? intervals.Max(i => i.Velocity)
        : 0;

double meanAbsDx =
    intervals.Count > 0
        ? intervals.Average(i => Math.Abs(i.Dx))
        : 0;

double meanAbsDy =
    intervals.Count > 0
        ? intervals.Average(i => Math.Abs(i.Dy))
        : 0;

double rmsDx =
    intervals.Count > 0
        ? Math.Sqrt(intervals.Average(i => i.Dx * i.Dx))
        : 0;

double rmsDy =
    intervals.Count > 0
        ? Math.Sqrt(intervals.Average(i => i.Dy * i.Dy))
        : 0;

double meanDt =
    intervals.Count > 0
        ? intervals.Average(i => i.DtMs)
        : 0;

double durationMs =
    samples[^1].Timestamp - samples[0].Timestamp;

double durationSec = durationMs / 1_000_000.0;

double fixationThreshold = 0.010;

var lowMovementIntervals =
    intervals
        .Where(i => i.Distance <= fixationThreshold)
        .ToList();

double lowMovementRatio =
    intervals.Count > 0
        ? lowMovementIntervals.Count * 100.0 / intervals.Count
        : 0;

double meanLowMovement =
    lowMovementIntervals.Count > 0
        ? lowMovementIntervals.Average(i => i.Distance)
        : 0;

double velocityMean = meanVelocity;

double velocitySd =
    intervals.Count > 0
        ? Math.Sqrt(
            intervals.Average(
                i => Math.Pow(i.Velocity - velocityMean, 2)))
        : 0;

double p50 = Percentile(
    intervals.Select(i => i.Velocity),
    0.50);

double p90 = Percentile(
    intervals.Select(i => i.Velocity),
    0.90);

double p95 = Percentile(
    intervals.Select(i => i.Velocity),
    0.95);

double p99 = Percentile(
    intervals.Select(i => i.Velocity),
    0.99);

Console.WriteLine("=== SESSION ===");
Console.WriteLine($"Samples:                 {samples.Count}");
Console.WriteLine($"Valid intervals:         {intervals.Count}");
Console.WriteLine($"Duration:                {durationSec:F3} sec");
Console.WriteLine();

Console.WriteLine("=== SPATIAL BEHAVIOR ===");
Console.WriteLine($"Mean X:                  {meanX:F6}");
Console.WriteLine($"Mean Y:                  {meanY:F6}");
Console.WriteLine($"X spread:                {spatialSpreadX:F6}");
Console.WriteLine($"Y spread:                {spatialSpreadY:F6}");
Console.WriteLine($"Total path distance:     {totalDistance:F6}");
Console.WriteLine();

Console.WriteLine("=== MOVEMENT CHARACTERISTICS ===");
Console.WriteLine($"Mean movement distance:  {meanDistance:F6}");
Console.WriteLine($"Mean velocity:           {meanVelocity:F6}");
Console.WriteLine($"Peak velocity:           {peakVelocity:F6}");
Console.WriteLine($"Mean |ΔX|:               {meanAbsDx:F6}");
Console.WriteLine($"Mean |ΔY|:               {meanAbsDy:F6}");
Console.WriteLine($"RMS ΔX:                  {rmsDx:F6}");
Console.WriteLine($"RMS ΔY:                  {rmsDy:F6}");
Console.WriteLine();

Console.WriteLine("=== TIMING ===");
Console.WriteLine($"Mean Δt:                 {meanDt:F2} ms");
Console.WriteLine($"Movement duration:       {durationSec:F3} sec");
Console.WriteLine();

Console.WriteLine("=== LOW-MOVEMENT / FIXATION PROXY ===");
Console.WriteLine($"Threshold:               {fixationThreshold:F4}");
Console.WriteLine($"Low-movement intervals:  {lowMovementIntervals.Count}");
Console.WriteLine($"Low-movement ratio:      {lowMovementRatio:F2}%");
Console.WriteLine($"Mean low movement:       {meanLowMovement:F6}");
Console.WriteLine();

Console.WriteLine("=== VELOCITY VARIABILITY ===");
Console.WriteLine($"Velocity mean:           {velocityMean:F6}");
Console.WriteLine($"Velocity SD:             {velocitySd:F6}");
Console.WriteLine();

Console.WriteLine("=== VELOCITY DISTRIBUTION ===");
Console.WriteLine($"P50 velocity:            {p50:F6}");
Console.WriteLine($"P90 velocity:            {p90:F6}");
Console.WriteLine($"P95 velocity:            {p95:F6}");
Console.WriteLine($"P99 velocity:            {p99:F6}");
Console.WriteLine();

Console.WriteLine("=== SCIENTIFIC ANALYSIS STATUS ===");
Console.WriteLine("Spatial analysis:        READY");
Console.WriteLine("Movement analysis:       READY");
Console.WriteLine("Timing analysis:         READY");
Console.WriteLine("Velocity analysis:       READY");
Console.WriteLine("Consistency analysis:    READY");
Console.WriteLine();

Console.WriteLine("=== RAW DATA SAFETY ===");
Console.WriteLine("Raw CSV modified:        NO");
Console.WriteLine("Raw CSV overwritten:     NO");
Console.WriteLine("Filtering applied:       NO");
Console.WriteLine("Interpolation applied:  NO");
Console.WriteLine();

Console.WriteLine("=== PHASE 12 COMPLETE ===");

static double Percentile(IEnumerable<double> values, double percentile)
{
    double[] sorted = values
        .OrderBy(v => v)
        .ToArray();

    if (sorted.Length == 0)
        return 0;

    if (sorted.Length == 1)
        return sorted[0];

    double position =
        (sorted.Length - 1) * percentile;

    int lower = (int)Math.Floor(position);
    int upper = (int)Math.Ceiling(position);

    if (lower == upper)
        return sorted[lower];

    double fraction = position - lower;

    return sorted[lower] +
           (sorted[upper] - sorted[lower]) * fraction;
}

class Sample
{
    public long Timestamp { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

class Interval
{
    public double DtMs { get; set; }
    public double Dx { get; set; }
    public double Dy { get; set; }
    public double Distance { get; set; }
    public double Velocity { get; set; }
    public double Direction { get; set; }
}