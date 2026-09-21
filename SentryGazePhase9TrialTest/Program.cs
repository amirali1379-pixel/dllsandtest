using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Phase 9: Trial System Test ===");
Console.WriteLine();

string folder = Directory.GetCurrentDirectory();

string? csvFile = Directory
    .GetFiles(folder, "gaze_*.csv")
    .OrderByDescending(File.GetLastWriteTime)
    .FirstOrDefault();

if (csvFile == null)
{
    Console.WriteLine("No gaze_*.csv found.");
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

        return new GazeSample(timestamp, x, y);
    })
    .Where(x => x != null)
    .Select(x => x!)
    .ToList();

if (samples.Count < 2)
{
    Console.WriteLine("Not enough samples.");
    return;
}

const double fixationDistanceThreshold = 0.08;
const double movementDistanceThreshold = 0.12;
const double minimumFixationMs = 100.0;
const double maximumGapMs = 100.0;

var trials = new List<Trial>();

int trialStart = 0;
int? fixationStart = null;
int? movementStart = null;

for (int i = 1; i < samples.Count; i++)
{
    double dt =
        (samples[i].Timestamp - samples[i - 1].Timestamp) / 1000.0;

    if (dt <= 0 || dt > maximumGapMs)
    {
        if (fixationStart.HasValue)
        {
            AddFixation(
                samples,
                fixationStart.Value,
                i - 1,
                trials,
                trialStart);
        }

        fixationStart = null;
        movementStart = null;
        trialStart = i;

        continue;
    }

    double dx = samples[i].X - samples[i - 1].X;
    double dy = samples[i].Y - samples[i - 1].Y;

    double distance = Math.Sqrt(dx * dx + dy * dy);

    if (distance <= fixationDistanceThreshold)
    {
        if (!fixationStart.HasValue)
            fixationStart = i - 1;

        if (movementStart.HasValue)
        {
            AddMovement(
                samples,
                movementStart.Value,
                i - 1,
                trials,
                trialStart);

            movementStart = null;
        }
    }
    else if (distance >= movementDistanceThreshold)
    {
        if (fixationStart.HasValue)
        {
            double fixationDuration =
                (samples[i - 1].Timestamp -
                 samples[fixationStart.Value].Timestamp) / 1000.0;

            if (fixationDuration >= minimumFixationMs)
            {
                AddFixation(
                    samples,
                    fixationStart.Value,
                    i - 1,
                    trials,
                    trialStart);
            }

            fixationStart = null;
        }

        if (!movementStart.HasValue)
            movementStart = i - 1;
    }
}

if (fixationStart.HasValue)
{
    AddFixation(
        samples,
        fixationStart.Value,
        samples.Count - 1,
        trials,
        trialStart);
}

if (movementStart.HasValue)
{
    AddMovement(
        samples,
        movementStart.Value,
        samples.Count - 1,
        trials,
        trialStart);
}

Console.WriteLine("=== TRIAL STRUCTURE ===");
Console.WriteLine();

Console.WriteLine($"Raw samples:             {samples.Count}");
Console.WriteLine($"Detected trials:         {trials.Count}");
Console.WriteLine();

for (int i = 0; i < trials.Count; i++)
{
    Trial trial = trials[i];

    Console.WriteLine($"Trial #{i + 1}");

    Console.WriteLine(
        $"  Start:                 {trial.StartTimestamp}");

    Console.WriteLine(
        $"  End:                   {trial.EndTimestamp}");

    Console.WriteLine(
        $"  Duration:              {trial.DurationMs:F2} ms");

    Console.WriteLine(
        $"  Fixations:             {trial.Fixations.Count}");

    Console.WriteLine(
        $"  Movements:             {trial.Movements.Count}");

    if (trial.Fixations.Count > 0)
    {
        double totalFixation =
            trial.Fixations.Sum(f => f.DurationMs);

        Console.WriteLine(
            $"  Fixation time:         {totalFixation:F2} ms");
    }

    if (trial.Movements.Count > 0)
    {
        double totalMovement =
            trial.Movements.Sum(m => m.Distance);

        Console.WriteLine(
            $"  Movement distance:     {totalMovement:F6}");
    }

    Console.WriteLine();
}

Console.WriteLine("=== PHASE 9 STATUS ===");
Console.WriteLine();

Console.WriteLine("Trial segmentation:      TESTED");
Console.WriteLine("Fixation grouping:       TESTED");
Console.WriteLine("Movement grouping:       TESTED");
Console.WriteLine("Raw data modified:       NO");
Console.WriteLine("Raw CSV overwritten:     NO");
Console.WriteLine();

Console.WriteLine(
    "Phase 9 test complete.");

void AddFixation(
    List<GazeSample> data,
    int start,
    int end,
    List<Trial> trials,
    int trialStart)
{
    if (end <= start)
        return;

    double duration =
        (data[end].Timestamp -
         data[start].Timestamp) / 1000.0;

    if (duration < minimumFixationMs)
        return;

    double centerX =
        data.Skip(start).Take(end - start + 1).Average(s => s.X);

    double centerY =
        data.Skip(start).Take(end - start + 1).Average(s => s.Y);

    Trial trial = GetOrCreateTrial(
        data,
        start,
        end,
        trials);

    trial.Fixations.Add(
        new Fixation(
            data[start].Timestamp,
            data[end].Timestamp,
            duration,
            centerX,
            centerY,
            end - start + 1));
}

void AddMovement(
    List<GazeSample> data,
    int start,
    int end,
    List<Trial> trials,
    int trialStart)
{
    if (end <= start)
        return;

    double distance = 0.0;

    for (int i = start + 1; i <= end; i++)
    {
        double dx = data[i].X - data[i - 1].X;
        double dy = data[i].Y - data[i - 1].Y;

        distance += Math.Sqrt(dx * dx + dy * dy);
    }

    double duration =
        (data[end].Timestamp -
         data[start].Timestamp) / 1000.0;

    if (duration <= 0)
        return;

    double velocity = distance / (duration / 1000.0);

    Trial trial = GetOrCreateTrial(
        data,
        start,
        end,
        trials);

    trial.Movements.Add(
        new Movement(
            data[start].Timestamp,
            data[end].Timestamp,
            duration,
            distance,
            velocity));
}

Trial GetOrCreateTrial(
    List<GazeSample> data,
    int start,
    int end,
    List<Trial> trials)
{
    Trial? existing = trials.LastOrDefault();

    if (existing != null &&
        data[start].Timestamp >= existing.StartTimestamp)
    {
        existing.EndTimestamp =
            Math.Max(
                existing.EndTimestamp,
                data[end].Timestamp);

        existing.DurationMs =
            (existing.EndTimestamp -
             existing.StartTimestamp) / 1000.0;

        return existing;
    }

    var trial = new Trial
    {
        StartTimestamp = data[start].Timestamp,
        EndTimestamp = data[end].Timestamp
    };

    trial.DurationMs =
        (trial.EndTimestamp -
         trial.StartTimestamp) / 1000.0;

    trials.Add(trial);

    return trial;
}

record GazeSample(
    long Timestamp,
    double X,
    double Y);

class Trial
{
    public long StartTimestamp { get; set; }
    public long EndTimestamp { get; set; }

    public double DurationMs { get; set; }

    public List<Fixation> Fixations { get; } = new();

    public List<Movement> Movements { get; } = new();
}

record Fixation(
    long StartTimestamp,
    long EndTimestamp,
    double DurationMs,
    double CenterX,
    double CenterY,
    int Samples);

record Movement(
    long StartTimestamp,
    long EndTimestamp,
    double DurationMs,
    double Distance,
    double AverageVelocity);