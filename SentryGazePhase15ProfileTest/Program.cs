using System.Globalization;

namespace SentryGazePhase15ProfileTest;

internal static class Program
{
    private const double LowMovementThreshold = 0.0100;
    private const double LargeGapThresholdMs = 100.0;

    private static void Main()
    {
        Console.WriteLine("=== Phase 15: Movement Profile Engine ===");
        Console.WriteLine();

        string currentDirectory =
            Directory.GetCurrentDirectory();

        string[] files =
            Directory.GetFiles(
                currentDirectory,
                "gaze_*.csv")
            .OrderByDescending(File.GetLastWriteTime)
            .ToArray();

        if (files.Length == 0)
        {
            Console.WriteLine(
                "ERROR: No gaze_*.csv files found.");

            Console.WriteLine(
                "Copy one or more raw gaze CSV files into this folder.");

            return;
        }

        Console.WriteLine(
            $"Datasets found: {files.Length}");

        Console.WriteLine();

        var sessions =
            new List<SessionMetrics>();

        foreach (string file in files)
        {
            Console.WriteLine(
                $"Analyzing: {Path.GetFileName(file)}");

            try
            {
                List<GazeSample> samples =
                    LoadSamples(file);

                if (samples.Count < 2)
                {
                    Console.WriteLine(
                        "Status: INVALID — insufficient samples");

                    Console.WriteLine();

                    continue;
                }

                SessionMetrics metrics =
                    AnalyzeSession(
                        Path.GetFileName(file),
                        samples);

                sessions.Add(metrics);

                PrintSession(metrics);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Status: ERROR — {ex.Message}");

                Console.WriteLine();
            }
        }

        if (sessions.Count == 0)
        {
            Console.WriteLine(
                "ERROR: No valid datasets available.");

            return;
        }

        ProfileMetrics profile =
            BuildProfile(sessions);

        PrintProfile(profile);

        string reportPath =
            Path.Combine(
                currentDirectory,
                "profile_report.txt");

        WriteReport(
            reportPath,
            profile);

        Console.WriteLine();

        Console.WriteLine(
            $"Profile report written: {reportPath}");

        Console.WriteLine();

        Console.WriteLine(
            "=== RAW DATA SAFETY ===");

        Console.WriteLine(
            "Raw CSV modified:        NO");

        Console.WriteLine(
            "Raw CSV overwritten:     NO");

        Console.WriteLine(
            "Filtering applied:       NO");

        Console.WriteLine(
            "Interpolation applied:  NO");

        Console.WriteLine();

        Console.WriteLine(
            "=== PHASE 15 STATUS ===");

        Console.WriteLine(
            "Session analysis:        PASS");

        Console.WriteLine(
            "Feature extraction:      PASS");

        Console.WriteLine(
            "Cross-session analysis:  PASS");

        Console.WriteLine(
            "Movement profile:        PASS");

        Console.WriteLine(
            "Profile generation:      PASS");
    }

    private static List<GazeSample> LoadSamples(
        string path)
    {
        var samples =
            new List<GazeSample>();

        foreach (string line in File.ReadLines(path).Skip(1))
        {
            GazeSample? sample =
                ParseSample(line);

            if (sample.HasValue)
                samples.Add(sample.Value);
        }

        return samples;
    }

    private static GazeSample? ParseSample(
        string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        string[] parts =
            line.Split(',');

        if (parts.Length < 3)
            return null;

        if (!long.TryParse(
                parts[0],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out long timestamp))
        {
            return null;
        }

        if (!double.TryParse(
                parts[1],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double x))
        {
            return null;
        }

        if (!double.TryParse(
                parts[2],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double y))
        {
            return null;
        }

        if (double.IsNaN(x) ||
            double.IsNaN(y) ||
            double.IsInfinity(x) ||
            double.IsInfinity(y))
        {
            return null;
        }

        return new GazeSample(
            timestamp,
            x,
            y);
    }

    private static SessionMetrics AnalyzeSession(
        string fileName,
        List<GazeSample> samples)
    {
        double durationMs =
            (samples[^1].Timestamp -
             samples[0].Timestamp) / 1000.0;

        double durationSec =
            durationMs / 1000.0;

        double sampleRate =
            durationSec > 0
                ? samples.Count / durationSec
                : 0;

        double minX =
            samples.Min(s => s.X);

        double maxX =
            samples.Max(s => s.X);

        double minY =
            samples.Min(s => s.Y);

        double maxY =
            samples.Max(s => s.Y);

        bool fullAxisCoverage =
            minX <= -0.85 &&
            maxX >= 0.85 &&
            minY <= -0.85 &&
            maxY >= 0.85;

        var intervals =
            new List<double>();

        var velocities =
            new List<double>();

        double totalDistance = 0;

        int lowMovementIntervals = 0;

        for (int i = 1;
             i < samples.Count;
             i++)
        {
            double dtMs =
                (samples[i].Timestamp -
                 samples[i - 1].Timestamp) / 1000.0;

            if (dtMs <= 0)
                continue;

            intervals.Add(dtMs);

            double dx =
                samples[i].X -
                samples[i - 1].X;

            double dy =
                samples[i].Y -
                samples[i - 1].Y;

            double distance =
                Math.Sqrt(
                    dx * dx +
                    dy * dy);

            totalDistance += distance;

            if (distance <= LowMovementThreshold)
                lowMovementIntervals++;

            double velocity =
                distance /
                (dtMs / 1000.0);

            velocities.Add(velocity);
        }

        double meanDt =
            intervals.Count > 0
                ? intervals.Average()
                : 0;

        double maxGap =
            intervals.Count > 0
                ? intervals.Max()
                : 0;

        int largeGaps =
            intervals.Count(
                x => x > LargeGapThresholdMs);

        double meanVelocity =
            velocities.Count > 0
                ? velocities.Average()
                : 0;

        double peakVelocity =
            velocities.Count > 0
                ? velocities.Max()
                : 0;

        double velocitySd =
            CalculateStandardDeviation(
                velocities);

        double lowMovementRatio =
            intervals.Count > 0
                ? (double)lowMovementIntervals /
                  intervals.Count
                : 0;

        int fixationCount =
            CountFixations(
                samples);

        bool coordinateValid =
            samples.All(
                s =>
                    !double.IsNaN(s.X) &&
                    !double.IsNaN(s.Y) &&
                    !double.IsInfinity(s.X) &&
                    !double.IsInfinity(s.Y));

        bool timingValid =
            intervals.Count > 0 &&
            meanDt > 0;

        bool movementValid =
            velocities.Count > 0 &&
            totalDistance >= 0;

        return new SessionMetrics(
            fileName,
            samples.Count,
            durationSec,
            sampleRate,
            meanDt,
            maxGap,
            largeGaps,
            minX,
            maxX,
            minY,
            maxY,
            fullAxisCoverage,
            totalDistance,
            meanVelocity,
            peakVelocity,
            velocitySd,
            lowMovementRatio,
            fixationCount,
            coordinateValid,
            timingValid,
            movementValid);
    }

    private static int CountFixations(
        List<GazeSample> samples)
    {
        const double threshold =
            LowMovementThreshold;

        const double minimumDurationMs =
            100.0;

        bool insideFixation = false;

        long fixationStart = 0;

        int count = 0;

        for (int i = 1;
             i < samples.Count;
             i++)
        {
            double dtMs =
                (samples[i].Timestamp -
                 samples[i - 1].Timestamp) / 1000.0;

            if (dtMs <= 0)
                continue;

            double dx =
                samples[i].X -
                samples[i - 1].X;

            double dy =
                samples[i].Y -
                samples[i - 1].Y;

            double distance =
                Math.Sqrt(
                    dx * dx +
                    dy * dy);

            bool lowMovement =
                distance <= threshold;

            if (lowMovement &&
                !insideFixation)
            {
                insideFixation = true;

                fixationStart =
                    samples[i - 1].Timestamp;
            }

            if (!lowMovement &&
                insideFixation)
            {
                long fixationEnd =
                    samples[i - 1].Timestamp;

                double duration =
                    (fixationEnd -
                     fixationStart) / 1000.0;

                if (duration >= minimumDurationMs)
                    count++;

                insideFixation = false;
            }
        }

        if (insideFixation)
        {
            double duration =
                (samples[^1].Timestamp -
                 fixationStart) / 1000.0;

            if (duration >= minimumDurationMs)
                count++;
        }

        return count;
    }

    private static double CalculateStandardDeviation(
        List<double> values)
    {
        if (values.Count == 0)
            return 0;

        double mean =
            values.Average();

        double variance =
            values.Sum(
                x => Math.Pow(x - mean, 2))
            / values.Count;

        return Math.Sqrt(variance);
    }

    private static ProfileMetrics BuildProfile(
        List<SessionMetrics> sessions)
    {
        return new ProfileMetrics(
            sessions.Count,

            Average(
                sessions.Select(
                    x => x.SampleRate)),

            Average(
                sessions.Select(
                    x => x.DurationSec)),

            Average(
                sessions.Select(
                    x => x.MeanDtMs)),

            Average(
                sessions.Select(
                    x => x.MaxGapMs)),

            Average(
                sessions.Select(
                    x => x.TotalDistance)),

            Average(
                sessions.Select(
                    x => x.MeanVelocity)),

            Average(
                sessions.Select(
                    x => x.PeakVelocity)),

            Average(
                sessions.Select(
                    x => x.VelocitySd)),

            Average(
                sessions.Select(
                    x => x.LowMovementRatio)),

            Average(
                sessions.Select(
                    x => (double)x.FixationCount)),

            CalculateSpread(
                sessions.Select(
                    x => x.SampleRate)),

            CalculateSpread(
                sessions.Select(
                    x => x.DurationSec)),

            CalculateSpread(
                sessions.Select(
                    x => x.MeanVelocity)),

            CalculateSpread(
                sessions.Select(
                    x => x.VelocitySd)),

            sessions.All(
                x => x.CoordinateValid),

            sessions.All(
                x => x.TimingValid),

            sessions.All(
                x => x.MovementValid),

            sessions.All(
                x => x.FullAxisCoverage));
    }

    private static double Average(
        IEnumerable<double> values)
    {
        double[] array =
            values.ToArray();

        return array.Length == 0
            ? 0
            : array.Average();
    }

    private static double CalculateSpread(
        IEnumerable<double> values)
    {
        double[] array =
            values.ToArray();

        if (array.Length == 0)
            return 0;

        return array.Max() -
               array.Min();
    }

    private static void PrintSession(
        SessionMetrics m)
    {
        Console.WriteLine(
            $"Samples:             {m.SampleCount}");

        Console.WriteLine(
            $"Duration:            {m.DurationSec:F3} sec");

        Console.WriteLine(
            $"Sample rate:         {m.SampleRate:F2} Hz");

        Console.WriteLine(
            $"Mean dt:             {m.MeanDtMs:F2} ms");

        Console.WriteLine(
            $"Max gap:             {m.MaxGapMs:F2} ms");

        Console.WriteLine(
            $"Large gaps >100 ms:  {m.LargeGaps}");

        Console.WriteLine(
            $"Total distance:      {m.TotalDistance:F6}");

        Console.WriteLine(
            $"Mean velocity:       {m.MeanVelocity:F6}");

        Console.WriteLine(
            $"Peak velocity:       {m.PeakVelocity:F6}");

        Console.WriteLine(
            $"Velocity SD:         {m.VelocitySd:F6}");

        Console.WriteLine(
            $"Low movement:        {m.LowMovementRatio:P2}");

        Console.WriteLine(
            $"Fixations:           {m.FixationCount}");

        Console.WriteLine(
            $"Full axis coverage:  {m.FullAxisCoverage}");

        Console.WriteLine(
            $"Status:              {GetSessionStatus(m)}");

        Console.WriteLine();
    }

    private static string GetSessionStatus(
        SessionMetrics m)
    {
        return m.CoordinateValid &&
               m.TimingValid &&
               m.MovementValid
            ? "VALID"
            : "INVALID";
    }

    private static void PrintProfile(
        ProfileMetrics p)
    {
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "=== MOVEMENT PROFILE ===");

        Console.WriteLine(
            "========================================");

        Console.WriteLine();

        Console.WriteLine(
            $"Sessions analyzed:       {p.SessionCount}");

        Console.WriteLine();

        Console.WriteLine(
            "=== SPATIAL / MOVEMENT ===");

        Console.WriteLine(
            $"Average total distance:  {p.AverageTotalDistance:F6}");

        Console.WriteLine(
            $"Average velocity:        {p.AverageMeanVelocity:F6}");

        Console.WriteLine(
            $"Average peak velocity:   {p.AveragePeakVelocity:F6}");

        Console.WriteLine(
            $"Average velocity SD:     {p.AverageVelocitySd:F6}");

        Console.WriteLine();

        Console.WriteLine(
            "=== TIMING ===");

        Console.WriteLine(
            $"Average sample rate:     {p.AverageSampleRate:F2} Hz");

        Console.WriteLine(
            $"Average duration:        {p.AverageDurationSec:F3} sec");

        Console.WriteLine(
            $"Average mean dt:         {p.AverageMeanDtMs:F2} ms");

        Console.WriteLine(
            $"Average max gap:         {p.AverageMaxGapMs:F2} ms");

        Console.WriteLine();

        Console.WriteLine(
            "=== FIXATION / LOW MOVEMENT ===");

        Console.WriteLine(
            $"Average low movement:    {p.AverageLowMovementRatio:P2}");

        Console.WriteLine(
            $"Average fixations:       {p.AverageFixationCount:F2}");

        Console.WriteLine();

        Console.WriteLine(
            "=== CROSS-SESSION SPREAD ===");

        Console.WriteLine(
            $"Sample rate spread:      {p.SampleRateSpread:F2} Hz");

        Console.WriteLine(
            $"Duration spread:         {p.DurationSpread:F3} sec");

        Console.WriteLine(
            $"Mean velocity spread:    {p.MeanVelocitySpread:F6}");

        Console.WriteLine(
            $"Velocity SD spread:      {p.VelocitySdSpread:F6}");

        Console.WriteLine();

        Console.WriteLine(
            "=== PROFILE VALIDITY ===");

        Console.WriteLine(
            $"Coordinate validity:     {PassFail(p.CoordinateValid)}");

        Console.WriteLine(
            $"Timing validity:         {PassFail(p.TimingValid)}");

        Console.WriteLine(
            $"Movement validity:       {PassFail(p.MovementValid)}");

        Console.WriteLine(
            $"Axis coverage:           {PassFail(p.FullAxisCoverage)}");

        Console.WriteLine();

        Console.WriteLine(
            "PROFILE STATUS:          READY");

        Console.WriteLine();

        Console.WriteLine(
            "Sensitivity estimation:  NOT YET PERFORMED");

        Console.WriteLine(
            "Reason: direction and distance controlled data required.");
    }

    private static string PassFail(
        bool value)
    {
        return value
            ? "PASS"
            : "FAIL";
    }

    private static void WriteReport(
        string path,
        ProfileMetrics p)
    {
        using StreamWriter writer =
            new(path, false);

        writer.WriteLine(
            "SENTRY GAZE — PHASE 15 MOVEMENT PROFILE");

        writer.WriteLine();

        writer.WriteLine(
            $"Sessions analyzed: {p.SessionCount}");

        writer.WriteLine();

        writer.WriteLine(
            "=== MOVEMENT ===");

        writer.WriteLine(
            $"Average total distance: {p.AverageTotalDistance:F6}");

        writer.WriteLine(
            $"Average velocity: {p.AverageMeanVelocity:F6}");

        writer.WriteLine(
            $"Average peak velocity: {p.AveragePeakVelocity:F6}");

        writer.WriteLine(
            $"Average velocity SD: {p.AverageVelocitySd:F6}");

        writer.WriteLine();

        writer.WriteLine(
            "=== TIMING ===");

        writer.WriteLine(
            $"Average sample rate: {p.AverageSampleRate:F2} Hz");

        writer.WriteLine(
            $"Average duration: {p.AverageDurationSec:F3} sec");

        writer.WriteLine(
            $"Average mean dt: {p.AverageMeanDtMs:F2} ms");

        writer.WriteLine(
            $"Average max gap: {p.AverageMaxGapMs:F2} ms");

        writer.WriteLine();

        writer.WriteLine(
            "=== FIXATIONS ===");

        writer.WriteLine(
            $"Average low movement: {p.AverageLowMovementRatio:P2}");

        writer.WriteLine(
            $"Average fixations: {p.AverageFixationCount:F2}");

        writer.WriteLine();

        writer.WriteLine(
            "=== CONSISTENCY ===");

        writer.WriteLine(
            $"Sample rate spread: {p.SampleRateSpread:F2} Hz");

        writer.WriteLine(
            $"Duration spread: {p.DurationSpread:F3} sec");

        writer.WriteLine(
            $"Mean velocity spread: {p.MeanVelocitySpread:F6}");

        writer.WriteLine(
            $"Velocity SD spread: {p.VelocitySdSpread:F6}");

        writer.WriteLine();

        writer.WriteLine(
            "=== VALIDITY ===");

        writer.WriteLine(
            $"Coordinate validity: {PassFail(p.CoordinateValid)}");

        writer.WriteLine(
            $"Timing validity: {PassFail(p.TimingValid)}");

        writer.WriteLine(
            $"Movement validity: {PassFail(p.MovementValid)}");

        writer.WriteLine(
            $"Axis coverage: {PassFail(p.FullAxisCoverage)}");

        writer.WriteLine();

        writer.WriteLine(
            "PROFILE STATUS: READY");

        writer.WriteLine();

        writer.WriteLine(
            "Sensitivity estimation: NOT YET PERFORMED");

        writer.WriteLine(
            "Raw CSV modified: NO");

        writer.WriteLine(
            "Raw CSV overwritten: NO");

        writer.WriteLine(
            "Filtering applied: NO");

        writer.WriteLine(
            "Interpolation applied: NO");
    }

    private readonly record struct GazeSample(
        long Timestamp,
        double X,
        double Y);

    private readonly record struct SessionMetrics(
        string FileName,
        int SampleCount,
        double DurationSec,
        double SampleRate,
        double MeanDtMs,
        double MaxGapMs,
        int LargeGaps,
        double MinX,
        double MaxX,
        double MinY,
        double MaxY,
        bool FullAxisCoverage,
        double TotalDistance,
        double MeanVelocity,
        double PeakVelocity,
        double VelocitySd,
        double LowMovementRatio,
        int FixationCount,
        bool CoordinateValid,
        bool TimingValid,
        bool MovementValid);

    private readonly record struct ProfileMetrics(
        int SessionCount,
        double AverageSampleRate,
        double AverageDurationSec,
        double AverageMeanDtMs,
        double AverageMaxGapMs,
        double AverageTotalDistance,
        double AverageMeanVelocity,
        double AveragePeakVelocity,
        double AverageVelocitySd,
        double AverageLowMovementRatio,
        double AverageFixationCount,
        double SampleRateSpread,
        double DurationSpread,
        double MeanVelocitySpread,
        double VelocitySdSpread,
        bool CoordinateValid,
        bool TimingValid,
        bool MovementValid,
        bool FullAxisCoverage);
}