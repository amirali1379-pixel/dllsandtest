// ============================================================
// SENTRY GAZE CAPTURE — اصلی
// ============================================================
// نسخه‌ی اصلاح‌شده بعد از Phase 3.
//
// تغییر اصلی نسبت به نسخه‌ی قبلی:
//   قبلاً: TryGetLatestGazePoint()  → فقط جدیدترین نمونه، بقیه drop می‌شد
//   الان:  GetGazePoints()          → همه‌ی نمونه‌های بافرشده از آخرین
//                                      Update() گرفته می‌شود، هیچ‌چیز drop نمی‌شود
//
// این تغییر با تست واقعی (SentryGazePhase3Test) تأیید شده:
// در یک اجرا حدود ۸٪ نمونه با روش قدیمی گم می‌شد.
//
// فرمت CSV دقیقاً طبق مستند پروژه حفظ شده:
//   TimestampMicroSeconds,X,Y
//
// طبق قانون‌های روزمپ:
//   - Raw CSV هیچ‌وقت overwrite نمی‌شود (اسم فایل timestamp‌دار است)
//   - مشکلات کیفیت داده (duplicate / negative delta) شمارش و گزارش
//     می‌شوند، نه اینکه بی‌سروصدا کنار گذاشته شوند (قانون ۸)
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Tobii.GameIntegration.Net;

Console.WriteLine("=== Sentry Gaze Capture ===");
Console.WriteLine();

TobiiGameIntegrationApi.SetApplicationName("SentryGazeCapture");
TobiiGameIntegrationApi.Update();

string trackerUrl = "tet-tcp://127.0.0.1";
Console.WriteLine($"Tracker URL: {trackerUrl}");
TobiiGameIntegrationApi.TrackTracker(trackerUrl);

string csvFile = $"gaze_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
using StreamWriter writer = new StreamWriter(csvFile, false);
writer.WriteLine("TimestampMicroSeconds,X,Y");
writer.Flush();

Console.WriteLine($"CSV: {Path.GetFullPath(csvFile)}");
Console.WriteLine();
Console.WriteLine("Collecting... Ctrl+C برای پایان.");
Console.WriteLine();

int sampleCount = 0;
int duplicateTimestampCount = 0;
int negativeOrZeroDeltaCount = 0;
long? previousTimestamp = null;

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Stopping...");
    writer.Flush();
    writer.Dispose();
    PrintSummary();
    Environment.Exit(0);
};

while (true)
{
    TobiiGameIntegrationApi.Update();

    List<GazePoint> buffered = TobiiGameIntegrationApi.GetGazePoints();

    if (buffered != null && buffered.Count > 0)
    {
        foreach (var gp in buffered)
        {
            if (previousTimestamp.HasValue)
            {
                long delta = gp.TimeStampMicroSeconds - previousTimestamp.Value;

                if (delta == 0)
                    duplicateTimestampCount++;
                else if (delta < 0)
                    negativeOrZeroDeltaCount++;
            }

            writer.WriteLine(
                $"{gp.TimeStampMicroSeconds}," +
                $"{gp.X:F6}," +
                $"{gp.Y:F6}");

            previousTimestamp = gp.TimeStampMicroSeconds;
            sampleCount++;
        }

        writer.Flush();
    }

    Thread.Sleep(5);
}

void PrintSummary()
{
    Console.WriteLine();
    Console.WriteLine("=== CAPTURE SUMMARY ===");
    Console.WriteLine($"Samples written:              {sampleCount}");
    Console.WriteLine($"Duplicate timestamps:         {duplicateTimestampCount}");
    Console.WriteLine($"Negative/out-of-order deltas: {negativeOrZeroDeltaCount}");
    Console.WriteLine($"File: {Path.GetFullPath(csvFile)}");
    Console.WriteLine();
    Console.WriteLine("=== CAPTURE COMPLETE ===");
}
