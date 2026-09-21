// ============================================================
// PHASE 5 — COORDINATE VALIDATION (v2)
// ============================================================
// حالا اول دنبال gaze_*.csv می‌گردد (خروجی SentryGazeCapture اصلاح‌شده،
// که از GetGazePoints استفاده می‌کند و drop ندارد)؛ اگر پیدا نشد،
// به phase3_full_*.csv برمی‌گردد.
//
// هیچ تبدیل مختصاتی انجام نمی‌شود — فقط گزارش.
// ============================================================

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Phase 5: Coordinate Validation ===");
Console.WriteLine();

string folder = Directory.GetCurrentDirectory();

string? csvFile =
    Directory.GetFiles(folder, "gaze_*.csv")
        .Concat(Directory.GetFiles(folder, "phase3_full_*.csv"))
        .OrderByDescending(File.GetLastWriteTime)
        .FirstOrDefault();

if (csvFile == null)
{
    Console.WriteLine("نه gaze_*.csv نه phase3_full_*.csv پیدا نشد.");
    Console.WriteLine("این فایل CSV را کنار exe کپی کنید.");
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
        if (p.Length < 3) return null;

        if (!double.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(p[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
            return null;

        return new { X = x, Y = y };
    })
    .Where(x => x != null)
    .Select(x => x!)
    .ToList();

if (samples.Count == 0)
{
    Console.WriteLine("نمونه‌ای پیدا نشد.");
    return;
}

double minX = samples.Min(s => s.X);
double maxX = samples.Max(s => s.X);
double minY = samples.Min(s => s.Y);
double maxY = samples.Max(s => s.Y);

double meanX = samples.Average(s => s.X);
double meanY = samples.Average(s => s.Y);

int outsideUnitRange = samples.Count(s =>
    s.X < -1.0 || s.X > 1.0 || s.Y < -1.0 || s.Y > 1.0);

Console.WriteLine("=== RANGE ===");
Console.WriteLine($"X: {minX:F6}  ..  {maxX:F6}   (span {maxX - minX:F6})");
Console.WriteLine($"Y: {minY:F6}  ..  {maxY:F6}   (span {maxY - minY:F6})");
Console.WriteLine();

Console.WriteLine("=== CENTER ===");
Console.WriteLine($"Mean X: {meanX:F6}");
Console.WriteLine($"Mean Y: {meanY:F6}");
Console.WriteLine();

Console.WriteLine("=== NORMALIZATION CHECK ===");
Console.WriteLine($"نمونه‌های خارج از [-1,1]: {outsideUnitRange} از {samples.Count} ({outsideUnitRange * 100.0 / samples.Count:F2}%)");
Console.WriteLine();

Console.WriteLine("=== AXIS COVERAGE (برای تأیید جهت محورها) ===");
Console.WriteLine("اگر تست چهار-گوشه انجام شده باشد، این چهار عدد باید نزدیک ±1 باشند:");
Console.WriteLine($"  Min X (چپ‌ترین نگاه):  {minX:F4}   {(minX <= -0.85 ? "OK, نزدیک -1" : "هنوز به -1 نرسیده")}");
Console.WriteLine($"  Max X (راست‌ترین نگاه): {maxX:F4}   {(maxX >= 0.85 ? "OK, نزدیک +1" : "هنوز به +1 نرسیده")}");
Console.WriteLine($"  Min Y: {minY:F4}   {(minY <= -0.85 ? "OK, نزدیک -1" : "هنوز به -1 نرسیده")}");
Console.WriteLine($"  Max Y: {maxY:F4}   {(maxY >= 0.85 ? "OK, نزدیک +1" : "هنوز به +1 نرسیده")}");
Console.WriteLine();

bool fullCoverage = minX <= -0.85 && maxX >= 0.85 && minY <= -0.85 && maxY >= 0.85;

if (fullCoverage)
{
    Console.WriteLine("=> پوشش کامل چهار گوشه دیده شد. Phase 5 از نظر Range/Normalization قطعی است.");
}
else
{
    Console.WriteLine("=> پوشش هنوز کامل نیست — یعنی این ضبط شامل نگاه به هر ۴ گوشه نبوده.");
    Console.WriteLine("   (این فقط یعنی تست کامل انجام نشده، نه اینکه چیزی خراب باشد.)");
}

Console.WriteLine();
Console.WriteLine("=== PHASE 5 REPORT COMPLETE (فقط گزارش — هیچ تبدیلی انجام نشد) ===");
