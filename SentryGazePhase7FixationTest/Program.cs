using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Phase 7: Fixation Detection ===");
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
                NumberStyles.Any,
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

        return new GazeSample(timestamp, x, y);
    })
    .Where(s => s != null)
    .Select(s => s!)
    .ToList();

if (samples.Count < 3)
{
    Console.WriteLine("Not enough samples.");
    return;
}

Console.WriteLine($"Samples: {samples.Count}");
Console.WriteLine();

const double dispersionThreshold = 0.08;
const double minimumFixationDurationMs = 100.0;

var fixations = new List<Fixation>();

int start = 0;

while (start < samples.Count - 1)
{
    int end = start + 1;

    while (end < samples.Count)
    {
        var window = samples
            .Skip(start)
            .Take(end - start + 1)
            .ToList();

        double minX = window.Min(s => s.X);
        double maxX = window.Max(s => s.X);
        double minY = window.Min(s => s.Y);
        double maxY = window.Max(s => s.Y);

        double dispersion =
            (maxX - minX) +
            (maxY - minY);

        if (dispersion > dispersionThreshold)
            break;

        end++;
    }

    int fixationEnd = end - 1;

    if (fixationEnd > start)
    {
        double durationMs =
            (samples[fixationEnd].Timestamp -
             samples[start].Timestamp) / 1000.0;

        if (durationMs >= minimumFixationDurationMs)
        {
            var window = samples
                .Skip(start)
                .Take(fixationEnd - start + 1)
                .ToList();

            fixations.Add(
                new Fixation(
                    start,
                    fixationEnd,
                    samples[start].Timestamp,
                    samples[fixationEnd].Timestamp,
                    window.Average(s => s.X),
                    window.Average(s => s.Y),
                    durationMs
                )
            );

            start = fixationEnd + 1;
            continue;
        }
    }

    start++;
}

Console.WriteLine("=== FIXATION RESULTS ===");
Console.WriteLine();

Console.WriteLine($"Fixations detected: {fixations.Count}");
Console.WriteLine();

if (fixations.Count == 0)
{
    Console.WriteLine("No fixation passed the current thresholds.");
}
else
{
    for (int i = 0; i < fixations.Count; i++)
    {
        var f = fixations[i];

        Console.WriteLine(
            $"Fixation #{i + 1}: " +
            $"Duration={f.DurationMs:F2} ms | " +
            $"Center X={f.CenterX:F5} | " +
            $"Center Y={f.CenterY:F5} | " +
            $"Samples={f.EndIndex - f.StartIndex + 1}");
    }
}

Console.WriteLine();

Console.WriteLine("=== THRESHOLDS ===");
Console.WriteLine($"Dispersion threshold:      {dispersionThreshold:F3}");
Console.WriteLine($"Minimum fixation duration: {minimumFixationDurationMs:F0} ms");

Console.WriteLine();

Console.WriteLine("=== PHASE 7 COMPLETE ===");
Console.WriteLine("Raw CSV was not modified.");
Console.WriteLine("No filtering or interpolation was applied.");

record GazeSample(
    long Timestamp,
    double X,
    double Y);

record Fixation(
    int StartIndex,
    int EndIndex,
    long StartTimestamp,
    long EndTimestamp,
    double CenterX,
    double CenterY,
    double DurationMs);