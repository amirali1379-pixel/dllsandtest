using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Phase 14: Evaluation / Benchmark ===");
Console.WriteLine();

string folder = Directory.GetCurrentDirectory();

string[] csvFiles = Directory
    .GetFiles(folder, "gaze_*.csv")
    .OrderBy(File.GetLastWriteTime)
    .ToArray();

if (csvFiles.Length == 0)
{
    Console.WriteLine("ERROR: No gaze_*.csv files found.");
    Console.WriteLine("Copy one or more raw gaze CSV files into this folder.");
    return;
}

Console.WriteLine($"CSV files found: {csvFiles.Length}");
Console.WriteLine();

var results = new List<BenchmarkResult>();

foreach (string csvFile in csvFiles)
{
    Console.WriteLine("----------------------------------------");
    Console.WriteLine($"Evaluating: {Path.GetFileName(csvFile)}");

    List<GazeSample> samples = LoadSamples(csvFile);

    if (samples.Count < 2)
    {
        Console.WriteLine("Status: INVALID - insufficient samples.");
        continue;
    }

    var intervals = new List<double>();
    var movements = new List<Movement>();

    for (int i = 1; i < samples.Count; i++)
    {
        double dtMs =
            (samples[i].Timestamp - samples[i - 1].Timestamp) / 1000.0;

        if (dtMs <= 0 || !double.IsFinite(dtMs))
            continue;

        intervals.Add(dtMs);

        double dx = samples[i].X - samples[i - 1].X;
        double dy = samples[i].Y - samples[i - 1].Y;

        double distance = Math.Sqrt(dx * dx + dy * dy);

        double velocity =
            distance / (dtMs / 1000.0);

        movements.Add(
            new Movement(
                dx,
                dy,
                distance,
                velocity,
                dtMs));
    }

    double durationMs =
        (samples[^1].Timestamp - samples[0].Timestamp) / 1000.0;

    double sampleRate =
        durationMs > 0
            ? samples.Count / (durationMs / 1000.0)
            : 0;

    double meanDt =
        intervals.Count > 0
            ? intervals.Average()
            : 0;

    double maxDt =
        intervals.Count > 0
            ? intervals.Max()
            : 0;

    int largeGaps =
        intervals.Count(dt => dt > 100.0);

    double minX = samples.Min(s => s.X);
    double maxX = samples.Max(s => s.X);
    double minY = samples.Min(s => s.Y);
    double maxY = samples.Max(s => s.Y);

    int invalidCoordinates =
        samples.Count(s =>
            !double.IsFinite(s.X) ||
            !double.IsFinite(s.Y));

    double totalDistance =
        movements.Sum(m => m.Distance);

    double meanVelocity =
        movements.Count > 0
            ? movements.Average(m => m.Velocity)
            : 0;

    double peakVelocity =
        movements.Count > 0
            ? movements.Max(m => m.Velocity)
            : 0;

    int lowMovementCount =
        movements.Count(m => m.Distance <= 0.010);

    double lowMovementRatio =
        movements.Count > 0
            ? lowMovementCount * 100.0 / movements.Count
            : 0;

    double velocityVariance =
        movements.Count > 0
            ? movements.Average(
                m => Math.Pow(m.Velocity - meanVelocity, 2))
            : 0;

    double velocitySd =
        Math.Sqrt(velocityVariance);

    bool fullAxisCoverage =
        minX <= -0.85 &&
        maxX >= 0.85 &&
        minY <= -0.85 &&
        maxY >= 0.85;

    int fixationCount =
        CountFixations(
            samples,
            0.010,
            100.0);

    bool validDataset =
        samples.Count >= 2 &&
        intervals.Count > 0 &&
        invalidCoordinates == 0 &&
        movements.Count > 0;

    Console.WriteLine($"Samples:             {samples.Count}");
    Console.WriteLine($"Duration:            {durationMs / 1000.0:F3} sec");
    Console.WriteLine($"Sample rate:         {sampleRate:F2} Hz");
    Console.WriteLine($"Mean dt:             {meanDt:F2} ms");
    Console.WriteLine($"Max dt:              {maxDt:F2} ms");
    Console.WriteLine($"Large gaps >100 ms:  {largeGaps}");
    Console.WriteLine($"Invalid coordinates: {invalidCoordinates}");
    Console.WriteLine($"Full axis coverage:  {fullAxisCoverage}");
    Console.WriteLine($"Total distance:      {totalDistance:F6}");
    Console.WriteLine($"Mean velocity:       {meanVelocity:F6}");
    Console.WriteLine($"Peak velocity:       {peakVelocity:F6}");
    Console.WriteLine($"Velocity SD:         {velocitySd:F6}");
    Console.WriteLine($"Low movement ratio:  {lowMovementRatio:F2}%");
    Console.WriteLine($"Fixations:           {fixationCount}");
    Console.WriteLine($"Status:              {(validDataset ? "VALID" : "REVIEW")}");
    Console.WriteLine();

    results.Add(
        new BenchmarkResult(
            Path.GetFileName(csvFile),
            samples.Count,
            durationMs,
            sampleRate,
            meanDt,
            maxDt,
            largeGaps,
            invalidCoordinates,
            minX,
            maxX,
            minY,
            maxY,
            totalDistance,
            meanVelocity,
            peakVelocity,
            velocitySd,
            lowMovementRatio,
            fixationCount,
            validDataset));
}

if (results.Count == 0)
{
    Console.WriteLine("No valid datasets were available.");
    return;
}

// ============================================================
// BENCHMARK SUMMARY
// ============================================================

Console.WriteLine("========================================");
Console.WriteLine("=== BENCHMARK SUMMARY ===");
Console.WriteLine("========================================");

Console.WriteLine();

Console.WriteLine($"Valid datasets:       {results.Count}");
Console.WriteLine(
    $"Average sample rate:  {results.Average(r => r.SampleRate):F2} Hz");

Console.WriteLine(
    $"Average duration:     {results.Average(r => r.DurationMs) / 1000.0:F3} sec");

Console.WriteLine(
    $"Average mean dt:      {results.Average(r => r.MeanDt):F2} ms");

Console.WriteLine(
    $"Average max gap:      {results.Average(r => r.MaxDt):F2} ms");

Console.WriteLine(
    $"Total large gaps:     {results.Sum(r => r.LargeGaps)}");

Console.WriteLine(
    $"Average velocity:     {results.Average(r => r.MeanVelocity):F6}");

Console.WriteLine(
    $"Average peak velocity:{results.Average(r => r.PeakVelocity):F6}");

Console.WriteLine(
    $"Average velocity SD:  {results.Average(r => r.VelocitySd):F6}");

Console.WriteLine(
    $"Average low movement: {results.Average(r => r.LowMovementRatio):F2}%");

Console.WriteLine(
    $"Average fixations:    {results.Average(r => r.FixationCount):F2}");

Console.WriteLine();

// ============================================================
// CONSISTENCY CHECK
// ============================================================

double sampleRateMean =
    results.Average(r => r.SampleRate);

double sampleRateMin =
    results.Min(r => r.SampleRate);

double sampleRateMax =
    results.Max(r => r.SampleRate);

double sampleRateSpread =
    sampleRateMax - sampleRateMin;

double durationMean =
    results.Average(r => r.DurationMs);

double durationMin =
    results.Min(r => r.DurationMs);

double durationMax =
    results.Max(r => r.DurationMs);

double durationSpread =
    durationMax - durationMin;

Console.WriteLine("=== CONSISTENCY ===");

Console.WriteLine(
    $"Sample rate range:   {sampleRateMin:F2} .. {sampleRateMax:F2} Hz");

Console.WriteLine(
    $"Sample rate spread:  {sampleRateSpread:F2} Hz");

Console.WriteLine(
    $"Duration range:      {durationMin / 1000.0:F3} .. {durationMax / 1000.0:F3} sec");

Console.WriteLine(
    $"Duration spread:     {durationSpread / 1000.0:F3} sec");

bool sampleRateConsistent =
    sampleRateMean > 0 &&
    sampleRateSpread <= sampleRateMean * 0.25;

bool coordinateConsistent =
    results.All(r =>
        r.InvalidCoordinates == 0);

bool processingValid =
    results.All(r =>
        r.ValidDataset);

Console.WriteLine(
    $"Sample rate consistency: {(sampleRateConsistent ? "PASS" : "REVIEW")}");

Console.WriteLine(
    $"Coordinate validity:     {(coordinateConsistent ? "PASS" : "FAIL")}");

Console.WriteLine(
    $"Processing validity:     {(processingValid ? "PASS" : "FAIL")}");

Console.WriteLine();

// ============================================================
// PHASE 14 FINAL STATUS
// ============================================================

bool benchmarkPass =
    results.Count > 0 &&
    processingValid &&
    coordinateConsistent;

Console.WriteLine("=== PHASE 14 STATUS ===");

Console.WriteLine(
    $"Dataset evaluation:     {(results.Count > 0 ? "PASS" : "FAIL")}");

Console.WriteLine(
    $"Raw data readability:   {(processingValid ? "PASS" : "FAIL")}");

Console.WriteLine(
    $"Coordinate integrity:   {(coordinateConsistent ? "PASS" : "FAIL")}");

Console.WriteLine(
    $"Cross-dataset benchmark:{(benchmarkPass ? "PASS" : "REVIEW")}");

Console.WriteLine();

Console.WriteLine("=== RAW DATA SAFETY ===");
Console.WriteLine("Raw CSV modified:        NO");
Console.WriteLine("Raw CSV overwritten:     NO");
Console.WriteLine("Filtering applied:       NO");
Console.WriteLine("Interpolation applied:   NO");
Console.WriteLine();

if (benchmarkPass)
{
    Console.WriteLine("=== PHASE 14 EVALUATION / BENCHMARK PASSED ===");
}
else
{
    Console.WriteLine("=== PHASE 14 EVALUATION / BENCHMARK REQUIRES REVIEW ===");
}

// ============================================================
// HELPERS
// ============================================================

static List<GazeSample> LoadSamples(string csvFile)
{
    var samples = new List<GazeSample>();

    foreach (string line in File.ReadLines(csvFile).Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
            continue;

        string[] p = line.Split(',');

        if (p.Length < 3)
            continue;

        if (!long.TryParse(
                p[0],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out long timestamp))
            continue;

        if (!double.TryParse(
                p[1],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double x))
            continue;

        if (!double.TryParse(
                p[2],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double y))
            continue;

        samples.Add(
            new GazeSample(
                timestamp,
                x,
                y));
    }

    return samples;
}

static int CountFixations(
    List<GazeSample> samples,
    double distanceThreshold,
    double minimumDurationMs)
{
    if (samples.Count < 2)
        return 0;

    int fixationCount = 0;
    int fixationStart = -1;

    for (int i = 1; i < samples.Count; i++)
    {
        double dx =
            samples[i].X - samples[i - 1].X;

        double dy =
            samples[i].Y - samples[i - 1].Y;

        double distance =
            Math.Sqrt(dx * dx + dy * dy);

        bool lowMovement =
            distance <= distanceThreshold;

        if (lowMovement && fixationStart < 0)
        {
            fixationStart = i - 1;
        }

        bool endFixation =
            !lowMovement ||
            i == samples.Count - 1;

        if (endFixation && fixationStart >= 0)
        {
            int endIndex =
                lowMovement && i == samples.Count - 1
                    ? i
                    : i - 1;

            if (endIndex >= fixationStart)
            {
                double durationMs =
                    (samples[endIndex].Timestamp -
                     samples[fixationStart].Timestamp) / 1000.0;

                if (durationMs >= minimumDurationMs)
                    fixationCount++;
            }

            fixationStart = -1;
        }
    }

    return fixationCount;
}

record GazeSample(
    long Timestamp,
    double X,
    double Y);

record Movement(
    double Dx,
    double Dy,
    double Distance,
    double Velocity,
    double DtMs);

record BenchmarkResult(
    string FileName,
    int Samples,
    double DurationMs,
    double SampleRate,
    double MeanDt,
    double MaxDt,
    int LargeGaps,
    int InvalidCoordinates,
    double MinX,
    double MaxX,
    double MinY,
    double MaxY,
    double TotalDistance,
    double MeanVelocity,
    double PeakVelocity,
    double VelocitySd,
    double LowMovementRatio,
    int FixationCount,
    bool ValidDataset);