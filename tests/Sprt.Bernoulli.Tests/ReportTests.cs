using System.Globalization;
using System.Text.Json;
using Sprt.Bernoulli.Cli;
using Sprt.Bernoulli.Simulation;

namespace Sprt.Bernoulli.Tests;

public sealed class ReportTests
{
    [Fact]
    public void ExportsPreserveSeedsAndMatchEmbeddedDataAcrossCultures()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sprt-report-" + Guid.NewGuid().ToString("N"));
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = SimulationRunner.Run(new(new(0.2, 0.8, 0.25, 0.25),
                Trials: 100, CalibrationTrials: 1000, Seed: ulong.MaxValue, TrajectoriesPerScenario: 0));
            ReportWriter.Write(result, directory);
            string json = File.ReadAllText(Path.Combine(directory, "results.json"));
            using var document = JsonDocument.Parse(json);
            Assert.Equal(ulong.MaxValue.ToString(CultureInfo.InvariantCulture),
                document.RootElement.GetProperty("options").GetProperty("seed").GetString());
            Assert.Equal(result.CalibrationSeed.ToString(CultureInfo.InvariantCulture),
                document.RootElement.GetProperty("calibrationSeed").GetString());
            string html = File.ReadAllText(Path.Combine(directory, "report.html"));
            Assert.Contains("const data = " + json + ";", html);
            Assert.DoesNotContain("/*__EXPERIMENT__*/", html);
            Assert.DoesNotContain("<script src=", html);
            Assert.Contains("type=\"range\"", html);
            string[] csv = File.ReadAllLines(Path.Combine(directory, "summary.csv"));
            Assert.Equal(7, csv.Length);
            Assert.All(csv, line => Assert.Equal(12, line.Split(',').Length));
            Assert.Contains("0.2", csv[1]);
            Assert.EndsWith(",", csv[1]); // H0 has no false-negative rate.
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
