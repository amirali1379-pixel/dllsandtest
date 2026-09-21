using System.Globalization;
using System.Text;

namespace SentryGazePhase16ControlledTrialTest
{
    internal static class Program
    {
        private static readonly double[] Directions =
        {
            0.0,
            45.0,
            90.0,
            135.0,
            180.0,
            225.0,
            270.0,
            315.0
        };

        private static readonly double[] Distances =
        {
            2.0,
            5.0,
            10.0
        };

        private static void Main()
        {
            Console.WriteLine("=== Phase 16: Controlled Data Collection ===");
            Console.WriteLine();

            string outputDirectory =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "controlled_trials");

            Directory.CreateDirectory(outputDirectory);

            List<ControlledTrial> trials =
                GenerateTrials();

            Console.WriteLine(
                $"Generated trials:       {trials.Count}");

            Console.WriteLine(
                $"Directions:             {Directions.Length}");

            Console.WriteLine(
                $"Distances:              {Distances.Length}");

            Console.WriteLine();

            PrintTrialPreview(trials);

            string metadataPath =
                Path.Combine(
                    outputDirectory,
                    "phase16_trial_metadata.csv");

            ExportTrialMetadata(
                metadataPath,
                trials);

            string sessionPath =
                Path.Combine(
                    outputDirectory,
                    "phase16_session.csv");

            ExportSessionSummary(
                sessionPath,
                trials);

            Console.WriteLine();

            Console.WriteLine(
                $"Metadata written:       {metadataPath}");

            Console.WriteLine(
                $"Session written:        {sessionPath}");

            Console.WriteLine();

            Console.WriteLine("=== CONTROL VALIDATION ===");

            bool trialGeneration =
                trials.Count ==
                Directions.Length * Distances.Length;

            bool directionControl =
                ValidateDirections(trials);

            bool distanceControl =
                ValidateDistances(trials);

            bool metadataIntegrity =
                ValidateMetadata(trials);

            bool exportIntegrity =
                File.Exists(metadataPath) &&
                File.Exists(sessionPath);

            Console.WriteLine(
                $"Trial generation:       {PassFail(trialGeneration)}");

            Console.WriteLine(
                $"Direction control:      {PassFail(directionControl)}");

            Console.WriteLine(
                $"Distance control:       {PassFail(distanceControl)}");

            Console.WriteLine(
                $"Trial metadata:         {PassFail(metadataIntegrity)}");

            Console.WriteLine(
                $"Trial export:           {PassFail(exportIntegrity)}");

            Console.WriteLine();

            Console.WriteLine("=== RAW DATA SAFETY ===");

            Console.WriteLine(
                "Existing gaze CSV modified: NO");

            Console.WriteLine(
                "Existing gaze CSV overwritten: NO");

            Console.WriteLine(
                "Filtering applied: NO");

            Console.WriteLine(
                "Interpolation applied: NO");

            Console.WriteLine();

            bool allPassed =
                trialGeneration &&
                directionControl &&
                distanceControl &&
                metadataIntegrity &&
                exportIntegrity;

            Console.WriteLine("=== PHASE 16 STATUS ===");

            Console.WriteLine(
                $"Controlled trial generation: {PassFail(trialGeneration)}");

            Console.WriteLine(
                $"Direction control:            {PassFail(directionControl)}");

            Console.WriteLine(
                $"Distance control:             {PassFail(distanceControl)}");

            Console.WriteLine(
                $"Metadata integrity:           {PassFail(metadataIntegrity)}");

            Console.WriteLine(
                $"Trial export:                 {PassFail(exportIntegrity)}");

            Console.WriteLine();

            Console.WriteLine(
                $"PHASE 16: {(allPassed ? "PASS" : "FAIL")}");

            Console.WriteLine();

            Console.WriteLine(
                "Sensitivity estimation: NOT PERFORMED");

            Console.WriteLine(
                "Reason: movement outcome data is not yet attached.");
        }

        private static List<ControlledTrial> GenerateTrials()
        {
            var trials =
                new List<ControlledTrial>();

            int trialNumber = 1;

            foreach (double distance in Distances)
            {
                foreach (double direction in Directions)
                {
                    double radians =
                        direction *
                        Math.PI /
                        180.0;

                    double targetX =
                        Math.Cos(radians) *
                        distance;

                    double targetY =
                        Math.Sin(radians) *
                        distance;

                    ControlledTrial trial =
                        new ControlledTrial(
                            trialNumber,
                            direction,
                            distance,
                            0.0,
                            0.0,
                            targetX,
                            targetY,
                            "CENTER",
                            "PENDING",
                            0.0,
                            0.0,
                            0.0,
                            0.0,
                            0.0,
                            0.0,
                            false);

                    trials.Add(trial);

                    trialNumber++;
                }
            }

            return trials;
        }

        private static void PrintTrialPreview(
            List<ControlledTrial> trials)
        {
            Console.WriteLine("=== TRIAL PREVIEW ===");
            Console.WriteLine();

            foreach (ControlledTrial trial in
                     trials.Take(12))
            {
                Console.WriteLine(
                    $"Trial {trial.TrialNumber,2} | " +
                    $"Direction {trial.Direction,6:F1}° | " +
                    $"Distance {trial.Distance,5:F1} | " +
                    $"Target ({trial.TargetX,7:F3}, {trial.TargetY,7:F3})");
            }

            if (trials.Count > 12)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"... {trials.Count - 12} additional trials");
            }
        }

        private static bool ValidateDirections(
            List<ControlledTrial> trials)
        {
            foreach (double direction in Directions)
            {
                int count =
                    trials.Count(
                        x => NearlyEqual(
                            x.Direction,
                            direction));

                if (count != Distances.Length)
                    return false;
            }

            return true;
        }

        private static bool ValidateDistances(
            List<ControlledTrial> trials)
        {
            foreach (double distance in Distances)
            {
                int count =
                    trials.Count(
                        x => NearlyEqual(
                            x.Distance,
                            distance));

                if (count != Directions.Length)
                    return false;
            }

            return true;
        }

        private static bool ValidateMetadata(
            List<ControlledTrial> trials)
        {
            if (trials.Count == 0)
                return false;

            HashSet<int> trialNumbers =
                new();

            foreach (ControlledTrial trial in trials)
            {
                if (!trialNumbers.Add(
                        trial.TrialNumber))
                {
                    return false;
                }

                if (trial.Direction < 0 ||
                    trial.Direction >= 360)
                {
                    return false;
                }

                if (trial.Distance <= 0)
                    return false;

                double expectedX =
                    Math.Cos(
                        trial.Direction *
                        Math.PI /
                        180.0) *
                    trial.Distance;

                double expectedY =
                    Math.Sin(
                        trial.Direction *
                        Math.PI /
                        180.0) *
                    trial.Distance;

                if (!NearlyEqual(
                        trial.TargetX,
                        expectedX))
                {
                    return false;
                }

                if (!NearlyEqual(
                        trial.TargetY,
                        expectedY))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ExportTrialMetadata(
            string path,
            List<ControlledTrial> trials)
        {
            using StreamWriter writer =
                new(path, false, Encoding.UTF8);

            writer.WriteLine(
                "TrialNumber,DirectionDeg,Distance,StartX,StartY,TargetX,TargetY,StartState,Outcome,MovementDurationMs,PathDistance,MeanVelocity,PeakVelocity,FinalError,CorrectionDistance,Hit");

            foreach (ControlledTrial trial in trials)
            {
                writer.WriteLine(
                    string.Join(
                        ",",
                        trial.TrialNumber.ToString(
                            CultureInfo.InvariantCulture),
                        trial.Direction.ToString(
                            "F3",
                            CultureInfo.InvariantCulture),
                        trial.Distance.ToString(
                            "F3",
                            CultureInfo.InvariantCulture),
                        trial.StartX.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.StartY.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.TargetX.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.TargetY.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.StartState,
                        trial.Outcome,
                        trial.MovementDurationMs.ToString(
                            "F3",
                            CultureInfo.InvariantCulture),
                        trial.PathDistance.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.MeanVelocity.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.PeakVelocity.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.FinalError.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.CorrectionDistance.ToString(
                            "F6",
                            CultureInfo.InvariantCulture),
                        trial.Hit
                            ? "true"
                            : "false"));
            }
        }

        private static void ExportSessionSummary(
            string path,
            List<ControlledTrial> trials)
        {
            using StreamWriter writer =
                new(path, false, Encoding.UTF8);

            writer.WriteLine(
                "Phase,Metric,Value");

            writer.WriteLine(
                "16,TrialCount," +
                trials.Count.ToString(
                    CultureInfo.InvariantCulture));

            writer.WriteLine(
                "16,DirectionCount," +
                Directions.Length.ToString(
                    CultureInfo.InvariantCulture));

            writer.WriteLine(
                "16,DistanceCount," +
                Distances.Length.ToString(
                    CultureInfo.InvariantCulture));

            writer.WriteLine(
                "16,ControlledCombinationCount," +
                trials.Count.ToString(
                    CultureInfo.InvariantCulture));

            writer.WriteLine(
                "16,SensitivityEstimation,NOT_PERFORMED");
        }

        private static bool NearlyEqual(
            double a,
            double b)
        {
            return Math.Abs(a - b) < 0.000001;
        }

        private static string PassFail(
            bool value)
        {
            return value
                ? "PASS"
                : "FAIL";
        }

        private readonly record struct ControlledTrial(
            int TrialNumber,
            double Direction,
            double Distance,
            double StartX,
            double StartY,
            double TargetX,
            double TargetY,
            string StartState,
            string Outcome,
            double MovementDurationMs,
            double PathDistance,
            double MeanVelocity,
            double PeakVelocity,
            double FinalError,
            double CorrectionDistance,
            bool Hit);
    }
}