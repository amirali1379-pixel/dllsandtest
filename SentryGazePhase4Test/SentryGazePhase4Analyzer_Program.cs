// ============================================================
// PHASE 4 — SAMPLING CHARACTERIZATION
// ============================================================
// این آنالایزر روی CSVـهایی کار می‌کند که از GetGazePoints() تولید
// شده‌اند (یعنی فایل‌های phase3_full_*.csv که هیچ drop ندارند)،
// نه روی فایل‌های قدیمی gaze_*.csv که با TryGetLatestGazePoint()
// نوشته شده بودند و طبق Phase 3 حدود ۸٪ نمونه کم دارند.
//
// خروجی دقیقاً طبق فرمت خواسته‌شده در روزمپ Phase 4:
//   Nominal rate / Effective rate / Median Δt / P95 Δt / P99 Δt /
//   Max gap / Estimated dropped samples
// ============================================================

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Phase 4: Sampling Characterization ===");
Console.WriteLine();

string dataFolder = Directory.GetCurrentDirectory();

string? csvFile = Directory
    .GetFiles(dataFolder, "phase3_full_*.csv")
    .OrderByDescending(File.GetLastWriteTime)
    .FirstOrDefault();

if (csvFile == null)
{
    Console.WriteLine("هیچ فایل phase3_full_*.csv پیدا نشد.");
    Console.WriteLine("این تحلیلگر را کنار خروجی SentryGazePhase3Test اجرا کنید.");
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
        if (p.Length < 3) return null;

        return new GazeSample
        {
            Time = long.Parse(p[0], CultureInfo.InvariantCulture),
            X = double.Parse(p[1], CultureInfo.InvariantCulture),
            Y = double.Parse(p[2], CultureInfo.InvariantCulture)
        };
    })
    .Where(x => x != null)
    .Select(x => x!)
    .ToList();

if (samples.Count < 2)
{
    Console.WriteLine("نمونه کافی نیست.");
    return;
}

double duration = (samples[^1].Time - samples[0].Time) / 1_000_000.0;

var deltas = samples
    .Zip(samples.Skip(1), (a, b) => (b.Time - a.Time) / 1000.0)
    .ToList();

double effectiveRate = duration > 0 ? (samples.Count - 1) / duration : 0;

int duplicateTimestamps = deltas.Count(d => d == 0);
int negativeDeltas = deltas.Count(d => d < 0);

var sortedDeltas = deltas.Where(d => d > 0).OrderBy(d => d).ToList();

double medianDt = Percentile(sortedDeltas, 0.50);
double p95Dt = Percentile(sortedDeltas, 0.95);
double p99Dt = Percentile(sortedDeltas, 0.99);
double maxGap = sortedDeltas.Count > 0 ? sortedDeltas[^1] : 0;

Console.WriteLine("=== SAMPLING CHARACTERIZATION ===");
Console.WriteLine($"Samples:                {samples.Count}");
Console.WriteLine($"Duration:                {duration:F3} sec");
Console.WriteLine();
Console.WriteLine($"Nominal rate:            N/A (API این مقدار را افشا نمی‌کند؛ باید از مشخصات فنی تراکر/ModelName خوانده شود)");
Console.WriteLine($"Effective rate:          {effectiveRate:F2} Hz");
Console.WriteLine($"Median Δt:               {medianDt:F2} ms");
Console.WriteLine($"P95 Δt:                  {p95Dt:F2} ms");
Console.WriteLine($"P99 Δt:                  {p99Dt:F2} ms");
Console.WriteLine($"Max gap:                 {maxGap:F2} ms");
Console.WriteLine();
Console.WriteLine($"Duplicate timestamps (Δt=0): {duplicateTimestamps}");
Console.WriteLine($"Negative/out-of-order Δt:    {negativeDeltas}");
Console.WriteLine();
Console.WriteLine("توجه: چون این فایل از GetGazePoints() ساخته شده (نه TryGetLatestGazePoint)،");
Console.WriteLine("«Estimated dropped samples» در این منبع باید نزدیک صفر باشد؛ عدد drop واقعی");
Console.WriteLine("(نسبت به معماری فعلی) همان چیزی است که خود SentryGazePhase3Test گزارش داد.");

Console.WriteLine();
Console.WriteLine("=== PHASE 4 ANALYSIS COMPLETE ===");

static double Percentile(List<double> values, double percentile)
{
    if (values.Count == 0) return 0;
    if (values.Count == 1) return values[0];

    double position = (values.Count - 1) * percentile;
    int lower = (int)Math.Floor(position);
    int upper = (int)Math.Ceiling(position);

    if (lower == upper) return values[lower];

    double fraction = position - lower;
    return values[lower] + (values[upper] - values[lower]) * fraction;
}

class GazeSample
{
    public long Time { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}
