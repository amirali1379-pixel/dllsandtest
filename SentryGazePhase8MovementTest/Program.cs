using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

Console.WriteLine("=== Phase 8: Gaze Movement Metrics Test ===");
Console.WriteLine();

string? csvFile = Directory
    .GetFiles(Directory.GetCurrentDirectory(), "gaze_*.csv")
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

var movements = new List<Movement>();

for (int i = 1; i < samples.Count; i++)
{
    GazeSample a = samples[i - 1];
    GazeSample b = samples[i];

    double dtMs = (b.Timestamp - a.Timestamp) / 1000.0;

    if (dtMs <= 0 || dtMs > 100)
        continue;

    double dx = b.X - a.X;
    double dy = b.Y - a.Y;

    double distance = Math.Sqrt(dx * dx + dy * dy);

    double dtSeconds = dtMs / 1000.0;

    double velocity = distance / dtSeconds;

    double directionDegrees =
        Math.Atan2(dy, dx) * 180.0 / Math.PI;

    movements.Add(
        new Movement(
            dtMs,
            dx,
            dy,
            distance,
            velocity,
            directionDegrees));
}

if (movements.Count == 0)
{
    Console.WriteLine("No valid movement intervals.");
    return;
}

double totalDistance =
    movements.Sum(m => m.Distance);

double movementDurationMs =
    movements.Sum(m => m.DtMs);

double averageVelocity =
    totalDistance / (movementDurationMs / 1000.0);

double peakVelocity =
    movements.Max(m => m.Velocity);

Movement largestMovement =
    movements.OrderByDescending(m => m.Distance).First();

double meanAbsDx =
    movements.Average(m => Math.Abs(m.Dx));

double meanAbsDy =
    movements.Average(m => Math.Abs(m.Dy));

double rmsDx =
    Math.Sqrt(movements.Average(m => m.Dx * m.Dx));

double rmsDy =
    Math.Sqrt(movements.Average(m => m.Dy * m.Dy));

double accelerationSum = 0;
int accelerationCount = 0;

for (int i = 1; i < movements.Count; i++)
{
    Movement previous = movements[i - 1];
    Movement current = movements[i];

    double dv = current.Velocity - previous.Velocity;
    double dt = current.DtMs / 1000.0;

    if (dt > 0)
    {
        double acceleration = dv / dt;

        accelerationSum += Math.Abs(acceleration);
        accelerationCount++;
    }
}

double meanAbsAcceleration =
    accelerationCount > 0
        ? accelerationSum / accelerationCount
        : 0;

double directionChangeSum = 0;
int directionChangeCount = 0;

for (int i = 1; i < movements.Count; i++)
{
    double difference =
        Math.Abs(
            movements[i].DirectionDegrees -
            movements[i - 1].DirectionDegrees);

    if (difference > 180)
        difference = 360 - difference;

    directionChangeSum += difference;
    directionChangeCount++;
}

double meanDirectionChange =
    directionChangeCount > 0
        ? directionChangeSum / directionChangeCount
        : 0;

double meanVelocity =
    movements.Average(m => m.Velocity);

double velocityVariance =
    movements.Average(
        m => Math.Pow(m.Velocity - meanVelocity, 2));

double velocityStdDev =
    Math.Sqrt(velocityVariance);

double smoothnessProxy =
    meanAbsAcceleration > 0
        ? 1.0 / (1.0 + meanAbsAcceleration)
        : 1.0;

Console.WriteLine("=== DATA ===");
Console.WriteLine(
    $"Raw samples:             {samples.Count}");

Console.WriteLine(
    $"Valid movement intervals: {movements.Count}");

Console.WriteLine();

Console.WriteLine("=== MOVEMENT ===");

Console.WriteLine(
    $"Total distance:          {totalDistance:F6}");

Console.WriteLine(
    $"Movement duration:       {movementDurationMs / 1000.0:F3} sec");

Console.WriteLine(
    $"Average velocity:        {averageVelocity:F6}");

Console.WriteLine(
    $"Peak velocity:           {peakVelocity:F6}");

Console.WriteLine();

Console.WriteLine("=== LARGEST MOVEMENT ===");

Console.WriteLine(
    $"Distance:                {largestMovement.Distance:F6}");

Console.WriteLine(
    $"ΔX:                      {largestMovement.Dx:F6}");

Console.WriteLine(
    $"ΔY:                      {largestMovement.Dy:F6}");

Console.WriteLine(
    $"Δt:                      {largestMovement.DtMs:F2} ms");

Console.WriteLine(
    $"Velocity:                {largestMovement.Velocity:F6}");

Console.WriteLine(
    $"Direction:               {largestMovement.DirectionDegrees:F2}°");

Console.WriteLine();

Console.WriteLine("=== ACCELERATION ===");

Console.WriteLine(
    $"Mean |acceleration|:     {meanAbsAcceleration:F6}");

Console.WriteLine();

Console.WriteLine("=== DIRECTION ===");

Console.WriteLine(
    $"Mean direction change:   {meanDirectionChange:F4}°");

Console.WriteLine();

Console.WriteLine("=== POSITION MOVEMENT ===");

Console.WriteLine(
    $"Mean |ΔX|:               {meanAbsDx:F6}");

Console.WriteLine(
    $"Mean |ΔY|:               {meanAbsDy:F6}");

Console.WriteLine(
    $"RMS ΔX:                  {rmsDx:F6}");

Console.WriteLine(
    $"RMS ΔY:                  {rmsDy:F6}");

Console.WriteLine();

Console.WriteLine("=== VELOCITY VARIABILITY ===");

Console.WriteLine(
    $"Mean velocity:           {meanVelocity:F6}");

Console.WriteLine(
    $"Velocity SD:             {velocityStdDev:F6}");

Console.WriteLine();

Console.WriteLine("=== SMOOTHNESS PROXY ===");

Console.WriteLine(
    $"Smoothness proxy:        {smoothnessProxy:F8}");

Console.WriteLine();

Console.WriteLine("=== PHASE 8 COMPLETE ===");
Console.WriteLine("Raw data was not modified.");
Console.WriteLine("No filtering or interpolation was applied.");

record GazeSample(
    long Timestamp,
    double X,
    double Y);

record Movement(
    double DtMs,
    double Dx,
    double Dy,
    double Distance,
    double Velocity,
    double DirectionDegrees);