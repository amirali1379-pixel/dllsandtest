using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

ApplicationConfiguration.Initialize();
Application.Run(new MainForm());

public sealed class MainForm : Form
{
    private readonly Label fileLabel = new();
    private readonly Label statusLabel = new();
    private readonly Label samplesLabel = new();
    private readonly Label durationLabel = new();
    private readonly Label rateLabel = new();
    private readonly Label gapsLabel = new();
    private readonly Label rangeLabel = new();
    private readonly Label velocityLabel = new();
    private readonly Label fixationLabel = new();
    private readonly Label safetyLabel = new();

    private readonly ListBox filesList = new();

    public MainForm()
    {
        Text = "Sentry Gaze — Production UI";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadCsvFiles();
    }

    private void BuildUi()
    {
        var title = new Label
        {
            Text = "SENTRY GAZE",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(25, 20)
        };

        var subtitle = new Label
        {
            Text = "Scientific Gaze Analysis",
            Font = new Font("Segoe UI", 11),
            AutoSize = true,
            Location = new Point(28, 62)
        };

        filesList.Location = new Point(25, 110);
        filesList.Size = new Size(300, 480);
        filesList.SelectedIndexChanged += (_, _) => LoadSelectedFile();

        var panel = new Panel
        {
            Location = new Point(350, 110),
            Size = new Size(600, 480),
            BorderStyle = BorderStyle.FixedSingle
        };

        AddLabel(panel, fileLabel, "File:", 25, 25, true);
        AddLabel(panel, statusLabel, "Status:", 25, 65, true);

        AddLabel(panel, samplesLabel, "Samples:", 25, 120);
        AddLabel(panel, durationLabel, "Duration:", 25, 155);
        AddLabel(panel, rateLabel, "Sample rate:", 25, 190);
        AddLabel(panel, gapsLabel, "Large gaps:", 25, 225);
        AddLabel(panel, rangeLabel, "Coordinate range:", 25, 260);
        AddLabel(panel, velocityLabel, "Velocity:", 25, 320);
        AddLabel(panel, fixationLabel, "Fixations:", 25, 355);
        AddLabel(panel, safetyLabel, "Raw data safety:", 25, 420, true);

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(filesList);
        Controls.Add(panel);
    }

    private static void AddLabel(
        Control parent,
        Label label,
        string text,
        int x,
        int y,
        bool bold = false)
    {
        label.Text = text;
        label.Font = new Font(
            "Segoe UI",
            10,
            bold ? FontStyle.Bold : FontStyle.Regular);

        label.AutoSize = true;
        label.Location = new Point(x, y);

        parent.Controls.Add(label);
    }

    private void LoadCsvFiles()
    {
        string folder = Directory.GetCurrentDirectory();

        string[] files = Directory
            .GetFiles(folder, "gaze_*.csv")
            .OrderByDescending(File.GetLastWriteTime)
            .ToArray();

        filesList.Items.Clear();

        foreach (string file in files)
            filesList.Items.Add(Path.GetFileName(file));

        if (files.Length == 0)
        {
            statusLabel.Text = "Status: NO CSV FOUND";
            statusLabel.ForeColor = Color.DarkRed;
            return;
        }

        filesList.SelectedIndex = 0;
    }

    private void LoadSelectedFile()
    {
        if (filesList.SelectedItem is not string fileName)
            return;

        string path =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                fileName);

        try
        {
            var samples = File
                .ReadLines(path)
                .Skip(1)
                .Select(ParseSample)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            if (samples.Count < 2)
            {
                statusLabel.Text = "Status: INVALID DATA";
                statusLabel.ForeColor = Color.DarkRed;
                return;
            }

            double durationMs =
                (samples[^1].Timestamp -
                 samples[0].Timestamp) / 1000.0;

            double sampleRate =
                samples.Count /
                (durationMs / 1000.0);

            var intervals =
                samples
                    .Zip(
                        samples.Skip(1),
                        (a, b) =>
                            (b.Timestamp - a.Timestamp) / 1000.0)
                    .Where(x => x > 0)
                    .ToList();

            int largeGaps =
                intervals.Count(x => x > 100);

            double minX = samples.Min(x => x.X);
            double maxX = samples.Max(x => x.X);
            double minY = samples.Min(x => x.Y);
            double maxY = samples.Max(x => x.Y);

            double totalDistance = 0;
            double peakVelocity = 0;
            double velocitySum = 0;
            int velocityCount = 0;

            for (int i = 1; i < samples.Count; i++)
            {
                double dt =
                    (samples[i].Timestamp -
                     samples[i - 1].Timestamp) / 1000.0;

                if (dt <= 0)
                    continue;

                double dx =
                    samples[i].X -
                    samples[i - 1].X;

                double dy =
                    samples[i].Y -
                    samples[i - 1].Y;

                double distance =
                    Math.Sqrt(dx * dx + dy * dy);

                double velocity =
                    distance / (dt / 1000.0);

                totalDistance += distance;
                velocitySum += velocity;
                velocityCount++;

                if (velocity > peakVelocity)
                    peakVelocity = velocity;
            }

            double meanVelocity =
                velocityCount > 0
                    ? velocitySum / velocityCount
                    : 0;

            int fixations =
                CountFixations(
                    samples,
                    0.010,
                    100);

            bool fullCoverage =
                minX <= -0.85 &&
                maxX >= 0.85 &&
                minY <= -0.85 &&
                maxY >= 0.85;

            fileLabel.Text =
                $"File: {fileName}";

            statusLabel.Text =
                "Status: READY";

            statusLabel.ForeColor =
                Color.DarkGreen;

            samplesLabel.Text =
                $"Samples: {samples.Count}";

            durationLabel.Text =
                $"Duration: {durationMs / 1000.0:F3} sec";

            rateLabel.Text =
                $"Sample rate: {sampleRate:F2} Hz";

            gapsLabel.Text =
                $"Large gaps >100 ms: {largeGaps}";

            rangeLabel.Text =
                $"Coordinate range: X {minX:F4} .. {maxX:F4}   |   Y {minY:F4} .. {maxY:F4}";

            velocityLabel.Text =
                $"Velocity: mean {meanVelocity:F4}   |   peak {peakVelocity:F4}   |   distance {totalDistance:F4}";

            fixationLabel.Text =
                $"Fixations: {fixations}   |   Full axis coverage: {fullCoverage}";

            safetyLabel.Text =
                "Raw data safety: READ-ONLY\n" +
                "CSV modified: NO\n" +
                "CSV overwritten: NO\n" +
                "Filtering: NO\n" +
                "Interpolation: NO";
        }
        catch (Exception ex)
        {
            statusLabel.Text =
                $"Status: ERROR — {ex.Message}";

            statusLabel.ForeColor =
                Color.DarkRed;
        }
    }

    private static GazeSample? ParseSample(string line)
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
            return null;

        if (!double.TryParse(
                parts[1],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double x))
            return null;

        if (!double.TryParse(
                parts[2],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double y))
            return null;

        return new GazeSample(
            timestamp,
            x,
            y);
    }

    private static int CountFixations(
        System.Collections.Generic.List<GazeSample> samples,
        double threshold,
        double minimumDurationMs)
    {
        int count = 0;
        int start = -1;

        for (int i = 1; i < samples.Count; i++)
        {
            double dx =
                samples[i].X -
                samples[i - 1].X;

            double dy =
                samples[i].Y -
                samples[i - 1].Y;

            double distance =
                Math.Sqrt(dx * dx + dy * dy);

            bool lowMovement =
                distance <= threshold;

            if (lowMovement && start < 0)
                start = i - 1;

            bool end =
                !lowMovement ||
                i == samples.Count - 1;

            if (!end || start < 0)
                continue;

            int endIndex =
                lowMovement &&
                i == samples.Count - 1
                    ? i
                    : i - 1;

            if (endIndex >= start)
            {
                double duration =
                    (samples[endIndex].Timestamp -
                     samples[start].Timestamp) / 1000.0;

                if (duration >= minimumDurationMs)
                    count++;
            }

            start = -1;
        }

        return count;
    }

    private readonly record struct GazeSample(
        long Timestamp,
        double X,
        double Y);
}