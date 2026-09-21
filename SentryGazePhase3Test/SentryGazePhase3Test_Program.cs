// ============================================================
// PHASE 3 — API DATA PATH TEST
// ============================================================
// هدف این تست:
// مقایسه مستقیم TryGetLatestGazePoint() در برابر GetGazePoints()
// تا مشخص شود آیا با فراخوانی فقط TryGetLatestGazePoint() داده‌ای
// drop می‌شود یا نه.
//
// این پروژه هیچ فایل موجودی را تغییر نمی‌دهد (طبق قانون ۶ روزمپ).
// آن را در یک پروژه جدید و جدا (مثلاً SentryGazePhase3Test) اجرا کنید.
//
// یافته تأییدشده از خود DLL (نه حدس):
//   public static bool TryGetLatestGazePoint(out GazePoint gazePoint);
//   public static List<GazePoint> GetGazePoints();
//   struct GazePoint { long TimeStampMicroSeconds; double X; double Y; }
//
// یعنی GetGazePoints() تمام نمونه‌های بافر شده از آخرین Update()
// را برمی‌گرداند، در حالی که TryGetLatestGazePoint() فقط آخرین
// (جدیدترین) نمونه را می‌دهد و بقیه را دور می‌ریزد.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Tobii.GameIntegration.Net;

Console.WriteLine("=== Phase 3: API Data Path Test ===");
Console.WriteLine();

TobiiGameIntegrationApi.SetApplicationName("SentryGazePhase3Test");
TobiiGameIntegrationApi.Update();

string trackerUrl = "tet-tcp://127.0.0.1";
Console.WriteLine($"Tracker URL: {trackerUrl}");
TobiiGameIntegrationApi.TrackTracker(trackerUrl);

Console.WriteLine("در حال جمع‌آوری... حدود ۲۰ ثانیه صبر کنید سپس Ctrl+C بزنید.");
Console.WriteLine();

// خروجی کامل: هر نمونه‌ای که GetGazePoints() برگردانده (منبع صحت)
string fullCsv = $"phase3_full_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
using StreamWriter fullWriter = new StreamWriter(fullCsv, false);
fullWriter.WriteLine("TimestampMicroSeconds,X,Y,UpdateCycle");
fullWriter.Flush();

long updateCycle = 0;

long totalFromGetGazePoints = 0;      // مجموع نمونه‌هایی که GetGazePoints() در کل اجرا برگردانده
long totalFromTryGetLatest = 0;       // مجموع نمونه‌هایی که اگر فقط TryGetLatestGazePoint() صدا زده می‌شد ثبت می‌شد
long updateCyclesWithMultiplePoints = 0; // چند بار در یک Update() بیش از ۱ نمونه بافر شده بود
long maxPointsInSingleUpdate = 0;

HashSet<long> seenTimestampsViaLatest = new HashSet<long>();
long? lastLatestTimestamp = null;

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Stopping...");
    fullWriter.Flush();
    PrintResults();
    Environment.Exit(0);
};

while (true)
{
    TobiiGameIntegrationApi.Update();
    updateCycle++;

    // --- مسیر ۱: همان چیزی که کد فعلی استفاده می‌کند ---
    bool ok = TobiiGameIntegrationApi.TryGetLatestGazePoint(out GazePoint latest);
    if (ok)
    {
        // فقط اگر timestamp واقعاً جدید بود بشمار (جلوگیری از شمارش تکراری)
        if (lastLatestTimestamp == null || latest.TimeStampMicroSeconds != lastLatestTimestamp.Value)
        {
            totalFromTryGetLatest++;
            lastLatestTimestamp = latest.TimeStampMicroSeconds;
        }
    }

    // --- مسیر ۲: همه نمونه‌های بافرشده از آخرین Update() ---
    List<GazePoint> buffered = TobiiGameIntegrationApi.GetGazePoints();

    if (buffered != null && buffered.Count > 0)
    {
        if (buffered.Count > 1)
            updateCyclesWithMultiplePoints++;

        if (buffered.Count > maxPointsInSingleUpdate)
            maxPointsInSingleUpdate = buffered.Count;

        foreach (var gp in buffered)
        {
            totalFromGetGazePoints++;

            fullWriter.WriteLine(
                $"{gp.TimeStampMicroSeconds}," +
                $"{gp.X:F6}," +
                $"{gp.Y:F6}," +
                $"{updateCycle}");
        }
        fullWriter.Flush();
    }

    Thread.Sleep(5);
}

void PrintResults()
{
    Console.WriteLine();
    Console.WriteLine("=== PHASE 3 RESULTS ===");
    Console.WriteLine($"Update() cycles run:                 {updateCycle}");
    Console.WriteLine($"Samples via GetGazePoints() (کامل):   {totalFromGetGazePoints}");
    Console.WriteLine($"Samples via TryGetLatestGazePoint():  {totalFromTryGetLatest}");

    long dropped = totalFromGetGazePoints - totalFromTryGetLatest;

    Console.WriteLine($"تخمین نمونه‌های drop‌شده:              {dropped}");

    if (totalFromGetGazePoints > 0)
    {
        double dropPct = dropped * 100.0 / totalFromGetGazePoints;
        Console.WriteLine($"درصد drop:                            {dropPct:F2}%");
    }

    Console.WriteLine($"Update cycle هایی با >1 نمونه بافرشده: {updateCyclesWithMultiplePoints}");
    Console.WriteLine($"بیشترین تعداد نمونه در یک Update():    {maxPointsInSingleUpdate}");
    Console.WriteLine();
    Console.WriteLine($"فایل کامل نوشته شد: {Path.GetFullPath(fullCsv)}");
    Console.WriteLine();
    Console.WriteLine("=== PHASE 3 TEST COMPLETE ===");
}
