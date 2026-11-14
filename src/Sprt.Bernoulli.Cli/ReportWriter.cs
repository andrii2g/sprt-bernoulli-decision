using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sprt.Bernoulli.Simulation;

namespace Sprt.Bernoulli.Cli;

internal static class ReportWriter
{
    public static void Write(ExperimentResult result, string directory)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new SeedConverter());
        string json = JsonSerializer.Serialize(result, options);
        using Stream template = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Sprt.Bernoulli.Cli.ReportTemplate.html")
            ?? throw new InvalidOperationException("The report template is missing.");
        using var reader = new StreamReader(template);
        string html = reader.ReadToEnd().Replace("/*__EXPERIMENT__*/null", json, StringComparison.Ordinal);

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "results.json"), json);
        File.WriteAllText(Path.Combine(directory, "report.html"), html);
        var csv = new StringBuilder("scenario,p,method,trials,meanSamples,medianSamples,p95Samples,favorH0,favorH1,inconclusive,falsePositive,falseNegative\n");
        foreach (var scenario in result.Scenarios)
        {
            foreach (var (name, summary) in new[] { ("SPRT", scenario.Sequential), ("Fixed", scenario.FixedSample) })
            {
                string fp = summary.FalsePositive?.Rate.ToString("G17", CultureInfo.InvariantCulture) ?? "";
                string fn = summary.FalseNegative?.Rate.ToString("G17", CultureInfo.InvariantCulture) ?? "";
                csv.AppendLine(FormattableString.Invariant(
                    $"{scenario.Name},{scenario.TrueProbability:G17},{name},{result.Options.Trials},{summary.Samples.Mean:G17},{summary.Samples.Median:G17},{summary.Samples.P95},{summary.FavorH0.Rate:G17},{summary.FavorH1.Rate:G17},{summary.Inconclusive.Rate:G17},{fp},{fn}"));
            }
        }
        File.WriteAllText(Path.Combine(directory, "summary.csv"), csv.ToString());
    }

    // JavaScript numbers cannot represent every 64-bit seed exactly.
    private sealed class SeedConverter : JsonConverter<ulong>
    {
        public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
