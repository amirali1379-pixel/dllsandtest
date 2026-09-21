using System.Globalization;

Console.WriteLine("=== Phase 6: Gaze Noise Analysis ===");
Console.WriteLine();

string dataFolder = @"C:\Users\Mo\Desktop\ashghal\SentryGazeCapture";

string? csvFile = Directory
    .GetFiles(dataFolder, "gaze_*.csv")
    .OrderByDescending(File.GetLastWriteTime)
    .FirstOrDefault();

if (csvFile == null)
{
    Console.WriteLine("No gaze CSV found.");
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
                out long time))
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

        return new GazeSample
        {
            Time = time,
            X = x,
            Y = y
        };
    })
    .Where(x => x != null)
    .Select(x => x!)
    .ToList();

if (samples.Count < 2)
{
    Console.WriteLine("Not enough samples.");
    return;
}

// ------------------------------------------------------------
// PHASE 6 — RAW MOVEMENT / NOISE ANALYSIS
// ------------------------------------------------------------

const double gapThresholdMs = 100.0;
const double lowMovementThreshold = 0.01;

var intervals = new List<Movement>();

for (int i = 1; i < samples.Count; i++)
{
    GazeSample a = samples[i - 1];
    GazeSample b = samples[i];

    double dtMs =
        (b.Time - a.Time) / 1000.0;

    if (dtMs <= 0)
        continue;

    double dx = b.X - a.X;
    double dy = b.Y - a.Y;

    double distance =
        Math.Sqrt(dx * dx + dy * dy);

    double velocity =
        distance / (dtMs / 1000.0);

    if (dtMs <= gapThresholdMs)
    {
        intervals.Add(new Movement
        {
            DtMs = dtMs,
            Dx = dx,
            Dy = dy,
            Distance = distance,
            Velocity = velocity
        });
    }
}

if (intervals.Count == 0)
{
    Console.WriteLine("No valid intervals.");
    return;
}

// ------------------------------------------------------------
// BASIC MOVEMENT
// ------------------------------------------------------------

double meanDx =
    intervals.Average(x => x.Dx);

double meanDy =
    intervals.Average(x => x.Dy);

double meanDistance =
    intervals.Average(x => x.Distance);

double meanVelocity =
    intervals.Average(x => x.Velocity);

double maxDistance =
    intervals.Max(x => x.Distance);

double maxVelocity =
    intervals.Max(x => x.Velocity);

// ------------------------------------------------------------
// LOW-MOVEMENT / STATIONARY REGION
// ------------------------------------------------------------

var lowMovement =
    intervals
        .Where(x => x.Distance <= lowMovementThreshold)
        .ToList();

double lowMovementRatio =
    lowMovement.Count * 100.0 / intervals.Count;

double lowMovementMean =
    lowMovement.Count > 0
        ? lowMovement.Average(x => x.Distance)
        : 0;

// ------------------------------------------------------------
// DX / DY NOISE
// ------------------------------------------------------------

double meanAbsDx =
    intervals.Average(x => Math.Abs(x.Dx));

double meanAbsDy =
    intervals.Average(x => Math.Abs(x.Dy));

double rmsDx =
    Math.Sqrt(
        intervals.Average(x => x.Dx * x.Dx));

double rmsDy =
    Math.Sqrt(
        intervals.Average(x => x.Dy * x.Dy));

// ------------------------------------------------------------
// VELOCITY DISTRIBUTION
// ------------------------------------------------------------

var velocities =
    intervals
        .Select(x => x.Velocity)
        .OrderBy(x => x)
        .ToList();

double p50 = Percentile(velocities, 0.50);
double p90 = Percentile(velocities, 0.90);
double p95 = Percentile(velocities, 0.95);
double p99 = Percentile(velocities, 0.99);

// ------------------------------------------------------------
// OUTPUT
// ------------------------------------------------------------

Console.WriteLine("=== SAMPLE DATA ===");
Console.WriteLine($"Samples:                 {samples.Count}");
Console.WriteLine($"Valid intervals:         {intervals.Count}");
Console.WriteLine();

Console.WriteLine("=== MOVEMENT ===");
Console.WriteLine($"Mean ΔX:                 {meanDx:F8}");
Console.WriteLine($"Mean ΔY:                 {meanDy:F8}");
Console.WriteLine($"Mean distance:           {meanDistance:F8}");
Console.WriteLine($"Mean velocity:           {meanVelocity:F8}");
Console.WriteLine($"Maximum distance:        {maxDistance:F8}");
Console.WriteLine($"Maximum velocity:        {maxVelocity:F8}");
Console.WriteLine();

Console.WriteLine("=== LOW-MOVEMENT REGION ===");
Console.WriteLine($"Threshold:               {lowMovementThreshold:F4}");
Console.WriteLine($"Low-movement intervals:  {lowMovement.Count}");
Console.WriteLine($"Low-movement ratio:      {lowMovementRatio:F2}%");
Console.WriteLine($"Mean low movement:       {lowMovementMean:F8}");
Console.WriteLine();

Console.WriteLine("=== POSITION JITTER ===");
Console.WriteLine($"Mean |ΔX|:               {meanAbsDx:F8}");
Console.WriteLine($"Mean |ΔY|:               {meanAbsDy:F8}");
Console.WriteLine($"RMS ΔX:                  {rmsDx:F8}");
Console.WriteLine($"RMS ΔY:                  {rmsDy:F8}");
Console.WriteLine();

Console.WriteLine("=== VELOCITY DISTRIBUTION ===");
Console.WriteLine($"P50 velocity:            {p50:F8}");
Console.WriteLine($"P90 velocity:            {p90:F8}");
Console.WriteLine($"P95 velocity:            {p95:F8}");
Console.WriteLine($"P99 velocity:            {p99:F8}");
Console.WriteLine();

Console.WriteLine("=== PHASE 6 COMPLETE ===");
Console.WriteLine("Raw data was not modified.");
Console.WriteLine("No filtering or interpolation was applied.");

static double Percentile(
    List<double> values,
    double percentile)
{
    if (values.Count == 0)
        return 0;

    if (values.Count == 1)
        return values[0];

    double position =
        (values.Count - 1) * percentile;

    int lower =
        (int)Math.Floor(position);

    int upper =
        (int)Math.Ceiling(position);

    if (lower == upper)
        return values[lower];

    double fraction =
        position - lower;

    return values[lower]
        + (values[upper] - values[lower]) * fraction;
}

class GazeSample
{
    public long Time { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

class Movement
{
    public double DtMs { get; set; }
    public double Dx { get; set; }
    public double Dy { get; set; }
    public double Distance { get; set; }
    public double Velocity { get; set; }
}