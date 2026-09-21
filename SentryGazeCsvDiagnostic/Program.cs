using System;
using System.IO;
using System.Threading;
using Tobii.GameIntegration.Net;

Console.WriteLine("=== Sentry Gaze CSV Diagnostic ===");
Console.WriteLine();

TobiiGameIntegrationApi.SetApplicationName("SentryGazeCsvDiagnostic");
TobiiGameIntegrationApi.Update();

string trackerUrl = "tet-tcp://127.0.0.1";

TobiiGameIntegrationApi.TrackTracker(trackerUrl);

string csvFile =
$"diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

using StreamWriter writer =
new StreamWriter(csvFile, false);

writer.WriteLine("TimestampMicroSeconds,X,Y");
writer.Flush();

Console.WriteLine($"CSV: {Path.GetFullPath(csvFile)}");
Console.WriteLine();
Console.WriteLine("Collecting...");
Console.WriteLine("Run for 20 seconds, then press Ctrl+C.");
Console.WriteLine();

int samples = 0;

while (true)
{
TobiiGameIntegrationApi.Update();

GazePoint gaze;

bool ok =
    TobiiGameIntegrationApi.TryGetLatestGazePoint(out gaze);

if (ok)
{
    writer.WriteLine(
        $"{gaze.TimeStampMicroSeconds}," +
        $"{gaze.X:F6}," +
        $"{gaze.Y:F6}");

    writer.Flush();

    samples++;
}

Thread.Sleep(5);

}
