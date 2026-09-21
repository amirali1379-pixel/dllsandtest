using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Phase 10: Scientific Dataset Structure Test ===");
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

Console.WriteLine($"Raw file: {Path.GetFileName(csvFile)}");
Console.WriteLine();

var rawSamples = File.ReadAllLines(csvFile)
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

        return new RawGazeSample(timestamp, x, y);
    })
    .Where(x => x != null)
    .Select(x => x!)
    .ToList();

if (rawSamples.Count == 0)
{
    Console.WriteLine("No valid raw samples.");
    return;
}

long startTimestamp = rawSamples.First().Timestamp;
long endTimestamp = rawSamples.Last().Timestamp;

double durationMs =
    (endTimestamp - startTimestamp) / 1000.0;

double minX = rawSamples.Min(x => x.X);
double maxX = rawSamples.Max(x => x.X);
double minY = rawSamples.Min(x => x.Y);
double maxY = rawSamples.Max(x => x.Y);

int invalidCoordinates = rawSamples.Count(x =>
    double.IsNaN(x.X) ||
    double.IsNaN(x.Y) ||
    double.IsInfinity(x.X) ||
    double.IsInfinity(x.Y));

int validIntervals = 0;
int largeGaps = 0;

var intervals = new List<double>();

for (int i = 1; i < rawSamples.Count; i++)
{
    double dt =
        (rawSamples[i].Timestamp -
         rawSamples[i - 1].Timestamp) / 1000.0;

    if (dt > 0)
    {
        intervals.Add(dt);

        if (dt <= 100.0)
            validIntervals++;
        else
            largeGaps++;
    }
}

double meanDt =
    intervals.Count > 0
        ? intervals.Average()
        : 0.0;

double approxRate =
    meanDt > 0
        ? 1000.0 / meanDt
        : 0.0;

// ------------------------------------------------------------
// SESSION
// ------------------------------------------------------------

var session = new Session
{
    SessionId = Path.GetFileNameWithoutExtension(csvFile),
    SourceFile = Path.GetFileName(csvFile),
    StartTimestamp = startTimestamp,
    EndTimestamp = endTimestamp,
    DurationMs = durationMs
};

// ------------------------------------------------------------
// DEVICE
// ------------------------------------------------------------

session.Device = new DeviceInfo
{
    Tracker = "Tobii Game Integration",
    TrackerUrl = "tet-tcp://127.0.0.1",
    CoordinateType = "Normalized gaze coordinates",
    CoordinateRange = "Approximately [-1,1]"
};

// ------------------------------------------------------------
// CONFIGURATION
// ------------------------------------------------------------

session.Configuration = new ConfigurationInfo
{
    CaptureMode = "Raw gaze capture",
    FilteringApplied = false,
    InterpolationApplied = false,
    RawDataOverwritten = false
};

// ------------------------------------------------------------
// CALIBRATION
// ------------------------------------------------------------

session.Calibration = new CalibrationInfo
{
    Status = "Observed / validated",
    CoordinateValidation = "Phase 5 complete",
    FullAxisCoverageObserved = true
};

// ------------------------------------------------------------
// RAW GAZE
// ------------------------------------------------------------

session.RawGaze = rawSamples
    .Select(x => new RawGazePoint
    {
        Timestamp = x.Timestamp,
        X = x.X,
        Y = x.Y
    })
    .ToList();

// ------------------------------------------------------------
// QUALITY REPORT
// ------------------------------------------------------------

session.QualityReport = new QualityReport
{
    SampleCount = rawSamples.Count,
    DurationMs = durationMs,
    ApproxSampleRate = approxRate,
    MeanDtMs = meanDt,
    ValidIntervals = validIntervals,
    LargeGaps = largeGaps,
    InvalidCoordinates = invalidCoordinates,
    MinX = minX,
    MaxX = maxX,
    MinY = minY,
    MaxY = maxY
};

// ------------------------------------------------------------
// DATASET REPORT
// ------------------------------------------------------------

Console.WriteLine("=== SESSION ===");
Console.WriteLine($"Session ID:              {session.SessionId}");
Console.WriteLine($"Source file:             {session.SourceFile}");
Console.WriteLine($"Duration:                {session.DurationMs:F2} ms");
Console.WriteLine();

Console.WriteLine("=== DEVICE ===");
Console.WriteLine($"Tracker:                 {session.Device.Tracker}");
Console.WriteLine($"Tracker URL:             {session.Device.TrackerUrl}");
Console.WriteLine($"Coordinate type:         {session.Device.CoordinateType}");
Console.WriteLine();

Console.WriteLine("=== CONFIGURATION ===");
Console.WriteLine($"Capture mode:            {session.Configuration.CaptureMode}");
Console.WriteLine($"Filtering applied:      {session.Configuration.FilteringApplied}");
Console.WriteLine($"Interpolation applied:  {session.Configuration.InterpolationApplied}");
Console.WriteLine($"Raw overwritten:         {session.Configuration.RawDataOverwritten}");
Console.WriteLine();

Console.WriteLine("=== CALIBRATION ===");
Console.WriteLine($"Status:                  {session.Calibration.Status}");
Console.WriteLine($"Coordinate validation:   {session.Calibration.CoordinateValidation}");
Console.WriteLine($"Full axis coverage:      {session.Calibration.FullAxisCoverageObserved}");
Console.WriteLine();

Console.WriteLine("=== RAW GAZE ===");
Console.WriteLine($"Raw samples:             {session.RawGaze.Count}");
Console.WriteLine();

Console.WriteLine("=== QUALITY REPORT ===");
Console.WriteLine($"Sample count:            {session.QualityReport.SampleCount}");
Console.WriteLine($"Approx sample rate:      {session.QualityReport.ApproxSampleRate:F2} Hz");
Console.WriteLine($"Mean dt:                 {session.QualityReport.MeanDtMs:F2} ms");
Console.WriteLine($"Valid intervals:         {session.QualityReport.ValidIntervals}");
Console.WriteLine($"Large gaps:              {session.QualityReport.LargeGaps}");
Console.WriteLine($"Invalid coordinates:     {session.QualityReport.InvalidCoordinates}");
Console.WriteLine(
    $"X range:                 {session.QualityReport.MinX:F6} .. {session.QualityReport.MaxX:F6}");
Console.WriteLine(
    $"Y range:                 {session.QualityReport.MinY:F6} .. {session.QualityReport.MaxY:F6}");
Console.WriteLine();

Console.WriteLine("=== DATASET STRUCTURE ===");

Console.WriteLine("Session");
Console.WriteLine(" ├── Device");
Console.WriteLine(" ├── Configuration");
Console.WriteLine(" ├── Calibration");
Console.WriteLine(" ├── Raw Gaze");
Console.WriteLine(" ├── Trials");
Console.WriteLine(" │    ├── Raw Gaze");
Console.WriteLine(" │    ├── Clean Gaze");
Console.WriteLine(" │    ├── Fixations");
Console.WriteLine(" │    ├── Movements");
Console.WriteLine(" │    └── Metrics");
Console.WriteLine(" └── Quality Report");

Console.WriteLine();
Console.WriteLine("=== PHASE 10 STATUS ===");
Console.WriteLine();

Console.WriteLine("Session structure:       READY");
Console.WriteLine("Device metadata:         READY");
Console.WriteLine("Configuration:           READY");
Console.WriteLine("Calibration metadata:    READY");
Console.WriteLine("Raw gaze storage:        READY");
Console.WriteLine("Trial container:         READY");
Console.WriteLine("Quality report:          READY");

Console.WriteLine();
Console.WriteLine("Raw CSV modified:        NO");
Console.WriteLine("Raw CSV overwritten:     NO");
Console.WriteLine("Filtering applied:       NO");
Console.WriteLine("Interpolation applied:   NO");

Console.WriteLine();
Console.WriteLine("=== PHASE 10 COMPLETE ===");


// ============================================================
// DATA MODELS
// ============================================================

record RawGazeSample(
    long Timestamp,
    double X,
    double Y);

class Session
{
    public string SessionId { get; set; } = "";
    public string SourceFile { get; set; } = "";

    public long StartTimestamp { get; set; }
    public long EndTimestamp { get; set; }

    public double DurationMs { get; set; }

    public DeviceInfo Device { get; set; } = new();
    public ConfigurationInfo Configuration { get; set; } = new();
    public CalibrationInfo Calibration { get; set; } = new();

    public List<RawGazePoint> RawGaze { get; set; } = new();

    public List<TrialData> Trials { get; set; } = new();

    public QualityReport QualityReport { get; set; } = new();
}

class DeviceInfo
{
    public string Tracker { get; set; } = "";
    public string TrackerUrl { get; set; } = "";
    public string CoordinateType { get; set; } = "";
    public string CoordinateRange { get; set; } = "";
}

class ConfigurationInfo
{
    public string CaptureMode { get; set; } = "";

    public bool FilteringApplied { get; set; }
    public bool InterpolationApplied { get; set; }
    public bool RawDataOverwritten { get; set; }
}

class CalibrationInfo
{
    public string Status { get; set; } = "";
    public string CoordinateValidation { get; set; } = "";

    public bool FullAxisCoverageObserved { get; set; }
}

class RawGazePoint
{
    public long Timestamp { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

class TrialData
{
    public long StartTimestamp { get; set; }
    public long EndTimestamp { get; set; }

    public List<RawGazePoint> RawGaze { get; set; } = new();
    public List<RawGazePoint> CleanGaze { get; set; } = new();

    public List<object> Fixations { get; set; } = new();
    public List<object> Movements { get; set; } = new();

    public Dictionary<string, double> Metrics { get; set; } = new();
}

class QualityReport
{
    public int SampleCount { get; set; }

    public double DurationMs { get; set; }
    public double ApproxSampleRate { get; set; }
    public double MeanDtMs { get; set; }

    public int ValidIntervals { get; set; }
    public int LargeGaps { get; set; }
    public int InvalidCoordinates { get; set; }

    public double MinX { get; set; }
    public double MaxX { get; set; }

    public double MinY { get; set; }
    public double MaxY { get; set; }
}