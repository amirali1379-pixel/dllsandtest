using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tobii.GameIntegration.Net;

Console.WriteLine("=== Sentry Gaze Capture Timing Diagnostic ===");
Console.WriteLine();

TobiiGameIntegrationApi.SetApplicationName("SentryGazeCaptureTiming");

TobiiGameIntegrationApi.Update();

string trackerUrl = "tet-tcp://127.0.0.1";

Console.WriteLine($"Tracker URL: {trackerUrl}");

TobiiGameIntegrationApi.TrackTracker(trackerUrl);

Console.WriteLine("TrackTracker called.");
Console.WriteLine();
Console.WriteLine("Collecting timing data...");
Console.WriteLine("Run for about 20 seconds, then press Ctrl+C.");
Console.WriteLine();

var intervals = new List<double>();

long? previousTimestamp = null;

int sampleCount = 0;

Console.CancelKeyPress += (sender, e) =>
{
e.Cancel = true;

Console.WriteLine();
Console.WriteLine("Stopping...");
Console.WriteLine();

PrintResults();
Environment.Exit(0);

};

while (true)
{
TobiiGameIntegrationApi.Update();

GazePoint gaze;

bool ok =
    TobiiGameIntegrationApi.TryGetLatestGazePoint(out gaze);

if (ok)
{
    sampleCount++;

    long timestamp =
        gaze.TimeStampMicroSeconds;

    if (previousTimestamp.HasValue)
    {
        long deltaMicroseconds =
            timestamp - previousTimestamp.Value;

        double deltaMilliseconds =
            deltaMicroseconds / 1000.0;

        if (deltaMilliseconds > 0)
        {
            intervals.Add(deltaMilliseconds);

            Console.WriteLine(
                $"Sample={sampleCount,4} | " +
                $"?t={deltaMilliseconds,8:F2} ms | " +
                $"X={gaze.X,8:F4} | " +
                $"Y={gaze.Y,8:F4}");
        }
    }

    previousTimestamp = timestamp;
}

Thread.Sleep(5);

}

void PrintResults()
{
if (intervals.Count == 0)
{
Console.WriteLine("No timing intervals collected.");
return;
}

double average =
    intervals.Average();

double minimum =
    intervals.Min();

double maximum =
    intervals.Max();

int over100 =
    intervals.Count(x => x > 100.0);

int over200 =
    intervals.Count(x => x > 200.0);

int over500 =
    intervals.Count(x => x > 500.0);

Console.WriteLine("=== TIMING RESULTS ===");

Console.WriteLine(
    $"Samples:                 {sampleCount}");

Console.WriteLine(
    $"Intervals:               {intervals.Count}");

Console.WriteLine(
    $"Average ?t:              {average:F2} ms");

Console.WriteLine(
    $"Minimum ?t:              {minimum:F2} ms");

Console.WriteLine(
    $"Maximum ?t:              {maximum:F2} ms");

Console.WriteLine(
    $"Gaps >100 ms:            {over100}");

Console.WriteLine(
    $"Gaps >200 ms:            {over200}");

Console.WriteLine(
    $"Gaps >500 ms:            {over500}");

Console.WriteLine();
Console.WriteLine("=== TOP 10 LARGEST GAPS ===");

foreach (double gap in intervals
    .OrderByDescending(x => x)
    .Take(10))
{
    Console.WriteLine(
        $"Gap: {gap:F2} ms");
}

Console.WriteLine();
Console.WriteLine("=== DIAGNOSTIC COMPLETE ===");

}
