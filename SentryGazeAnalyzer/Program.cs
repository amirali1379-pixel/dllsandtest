// ============================================================
// SENTRY GAZE QUALITY ANALYZER
// ============================================================
// دو فیکس نسبت به نسخه‌ی قبلی:
//   ۱. خط‌های ناقص/خراب CSV دیگر باعث کرش نمی‌شوند (فقط رد می‌شوند)
//   ۲. مسیر پوشه دیگر فقط هاردکد نیست: اول پوشه‌ی جاری را می‌گردد،
//      اگر گاز CSV پیدا نشد، به مسیر قدیمی هم به‌عنوان fallback
//      سر می‌زند (سازگاری با نحوه‌ی قبلی اجرا حفظ شده).
// ============================================================

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Sentry Gaze Quality Analyzer ===");
Console.WriteLine();

string legacyFolder = @"C:\Users\Mo\Desktop\ashghal\SentryGazeCapture";
string currentFolder = Directory.GetCurrentDirectory();

string dataFolder =
    Directory.GetFiles(currentFolder, "gaze_*.csv").Any()
        ? currentFolder
        : legacyFolder;

string? csvFile = Directory.Exists(dataFolder)
    ? Directory
        .GetFiles(dataFolder, "gaze_*.csv")
        .OrderByDescending(File.GetLastWriteTime)
        .FirstOrDefault()
    : null;

if (csvFile == null)
{
    Console.WriteLine($"No gaze CSV found in either:");
    Console.WriteLine($"  {currentFolder}");
    Console.WriteLine($"  {legacyFolder}");
    return;
}

Console.WriteLine($"File: {Path.GetFileName(csvFile)}");

int skippedLines = 0;

var samples = File.ReadAllLines(csvFile)
    .Skip(1)
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(line =>
    {
        string[] p = line.Split(',');

        if (p.Length < 3)
        {
            skippedLines++;
            return null;
        }

        if (!long.TryParse(p[0], NumberStyles.Any, CultureInfo.InvariantCulture, out long time) ||
            !double.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(p[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
        {
            skippedLines++;
            return null;
        }

        return new GazeSample { Time = time, X = x, Y = y };
    })
    .Where(x => x != null)
    .Select(x => x!)
    .ToList();

if (skippedLines > 0)
{
    Console.WriteLine($"Warning: {skippedLines} malformed line(s) skipped.");
}

if (samples.Count < 2)
{
    Console.WriteLine("Not enough samples.");
    return;
}

// ------------------------------------------------------------
// Configuration
// ------------------------------------------------------------

const double gapThresholdMs = 100.0;
const double duplicateEpsilon = 0.000001;

// ------------------------------------------------------------
// Basic timing
// ------------------------------------------------------------

double duration =
    (samples[^1].Time - samples[0].Time) / 1_000_000.0;

var intervals = samples
    .Zip(samples.Skip(1), (a, b) => new IntervalData
    {
        From = a,
        To = b,
        DeltaMs = (b.Time - a.Time) / 1000.0
    })
    .ToList();

double averageInterval =
    intervals.Count > 0
        ? intervals.Average(x => x.DeltaMs)
        : 0;

double sampleRate =
    duration > 0
        ? (samples.Count - 1) / duration
        : 0;

// ------------------------------------------------------------
// Quality checks
// ------------------------------------------------------------

int invalidTimestampCount =
    intervals.Count(x => x.DeltaMs <= 0);

int largeGapCount =
    intervals.Count(x => x.DeltaMs > gapThresholdMs);

int duplicateCount = 0;

for (int i = 1; i < samples.Count; i++)
{
    bool samePosition =
        Math.Abs(samples[i].X - samples[i - 1].X) <= duplicateEpsilon &&
        Math.Abs(samples[i].Y - samples[i - 1].Y) <= duplicateEpsilon;

    if (samePosition)
        duplicateCount++;
}

int invalidCoordinateCount =
    samples.Count(x =>
        double.IsNaN(x.X) ||
        double.IsNaN(x.Y) ||
        double.IsInfinity(x.X) ||
        double.IsInfinity(x.Y));

// ------------------------------------------------------------
// Timing statistics
// ------------------------------------------------------------

double minimumInterval =
    intervals.Count > 0
        ? intervals.Min(x => x.DeltaMs)
        : 0;

double maximumInterval =
    intervals.Count > 0
        ? intervals.Max(x => x.DeltaMs)
        : 0;

int normalIntervals =
    intervals.Count(x =>
        x.DeltaMs > 0 &&
        x.DeltaMs <= gapThresholdMs);

double validIntervalPercentage =
    intervals.Count > 0
        ? normalIntervals * 100.0 / intervals.Count
        : 0;

// ------------------------------------------------------------
// Coordinate statistics
// ------------------------------------------------------------

double minX = samples.Min(x => x.X);
double maxX = samples.Max(x => x.X);

double minY = samples.Min(x => x.Y);
double maxY = samples.Max(x => x.Y);

// ------------------------------------------------------------
// Quality score
// ------------------------------------------------------------

double score = 100.0;

if (invalidTimestampCount > 0)
    score -= Math.Min(30, invalidTimestampCount * 5);

if (largeGapCount > 0)
    score -= Math.Min(30, largeGapCount * 5);

if (invalidCoordinateCount > 0)
    score -= Math.Min(30, invalidCoordinateCount * 5);

if (duplicateCount > samples.Count * 0.10)
    score -= 10;

score = Math.Max(0, score);

string qualityStatus =
    score >= 90
        ? "GOOD"
        : score >= 70
            ? "WARNING"
            : "POOR";

// ------------------------------------------------------------
// Output
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== BASIC DATA ===");

Console.WriteLine(
    $"Samples:                 {samples.Count}");

Console.WriteLine(
    $"Duration:                {duration:F3} sec");

Console.WriteLine(
    $"Approx sample rate:      {sampleRate:F2} Hz");

// ------------------------------------------------------------
// Timing
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== TIMING QUALITY ===");

Console.WriteLine(
    $"Average dt:              {averageInterval:F2} ms");

Console.WriteLine(
    $"Minimum dt:              {minimumInterval:F2} ms");

Console.WriteLine(
    $"Maximum dt:              {maximumInterval:F2} ms");

Console.WriteLine(
    $"Normal intervals:        {normalIntervals}");

Console.WriteLine(
    $"Large gaps >100 ms:      {largeGapCount}");

Console.WriteLine(
    $"Valid interval ratio:    {validIntervalPercentage:F2}%");

// ------------------------------------------------------------
// Coordinate quality
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== COORDINATE QUALITY ===");

Console.WriteLine(
    $"Invalid coordinates:     {invalidCoordinateCount}");

Console.WriteLine(
    $"Duplicate positions:     {duplicateCount}");

Console.WriteLine(
    $"X Range:                 {minX:F6} .. {maxX:F6}");

Console.WriteLine(
    $"Y Range:                 {minY:F6} .. {maxY:F6}");

// ------------------------------------------------------------
// Quality score
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== OVERALL QUALITY ===");

Console.WriteLine(
    $"Quality score:           {score:F1}/100");

Console.WriteLine(
    $"Status:                  {qualityStatus}");

if (qualityStatus == "GOOD")
{
    Console.WriteLine(
        "The captured dataset passed the current basic quality checks.");
}
else if (qualityStatus == "WARNING")
{
    Console.WriteLine(
        "The dataset contains quality issues that should be reviewed.");
}
else
{
    Console.WriteLine(
        "The dataset has significant quality problems.");
}

// ------------------------------------------------------------
// Detailed warnings
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== QUALITY WARNINGS ===");

bool warningFound = false;

if (invalidTimestampCount > 0)
{
    Console.WriteLine(
        $"- {invalidTimestampCount} invalid timing intervals detected.");
    warningFound = true;
}

if (largeGapCount > 0)
{
    Console.WriteLine(
        $"- {largeGapCount} large timing gaps detected.");
    warningFound = true;
}

if (invalidCoordinateCount > 0)
{
    Console.WriteLine(
        $"- {invalidCoordinateCount} invalid coordinate samples detected.");
    warningFound = true;
}

if (duplicateCount > samples.Count * 0.10)
{
    Console.WriteLine(
        "- A high percentage of consecutive samples have identical coordinates.");
    warningFound = true;
}

if (!warningFound)
{
    Console.WriteLine(
        "No major quality warnings detected.");
}

// ------------------------------------------------------------
// Large gaps
// ------------------------------------------------------------

if (largeGapCount > 0)
{
    Console.WriteLine();
    Console.WriteLine("=== LARGE TIMING GAPS ===");

    foreach (var gap in intervals
        .Where(x => x.DeltaMs > gapThresholdMs)
        .OrderByDescending(x => x.DeltaMs)
        .Take(10))
    {
        Console.WriteLine(
            $"Gap: {gap.DeltaMs,8:F2} ms | " +
            $"From X={gap.From.X:F4}, Y={gap.From.Y:F4} | " +
            $"To X={gap.To.X:F4}, Y={gap.To.Y:F4}");
    }
}

// ------------------------------------------------------------
// Final
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== QUALITY ANALYSIS COMPLETE ===");

class GazeSample
{
    public long Time { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

class IntervalData
{
    public GazeSample From { get; set; } = null!;
    public GazeSample To { get; set; } = null!;
    public double DeltaMs { get; set; }
}
