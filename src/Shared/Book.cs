using System.Text.Json;

namespace BayThree;

public sealed record Part(string Sku, string Name, double Eur, bool InStock);

public sealed record Job(
    string Id,
    string Name,
    string Bay,
    IReadOnlyList<string> Fits,
    double LabourHours,
    IReadOnlyList<Part> Parts,
    string Notes);

public sealed record ServiceBook(
    string Garage,
    double LabourEurPerHour,
    IReadOnlyList<Job> Jobs);

public static class Book
{
    public const string Intake = "polo is due an oil change, interval light is on";

    public static readonly IReadOnlyList<string> JobIds =
    [
        "oil-filter",
        "annual-check",
        "wipers-front",
        "plugs-polo",
        "pads-front-golf",
        "pads-rear-golf",
        "discs-front-golf",
        "battery-60ah",
        "coolant-hose-golf",
        "puncture",
    ];

    public static ServiceBook Catalog { get; } = Load();

    public static IReadOnlyDictionary<string, Job> Jobs { get; } =
        Catalog.Jobs.ToDictionary(job => job.Id, StringComparer.Ordinal);

    public static double Rate => Catalog.LabourEurPerHour;

    public static string JobLines(bool withParts = false)
    {
        return string.Join(
            "\n",
            Catalog.Jobs.Select(job =>
            {
                var fits = string.Join(", ", job.Fits);
                var line = $"{job.Id}: {job.Name} ({fits}), {job.Bay} bay";
                if (withParts)
                {
                    line += $", {job.Parts.Count} parts";
                }

                return line;
            }));
    }

    public static string BookText()
    {
        var lines = new List<string> { $"Labour rate: {Rate} EUR per hour", "" };
        foreach (var job in Catalog.Jobs)
        {
            var fits = string.Join(", ", job.Fits);
            lines.Add($"{job.Id}: {job.Name} ({fits}), {job.Bay} bay");
            lines.Add($"Labour: {job.LabourHours} h");
            if (job.Parts.Count == 0)
            {
                lines.Add("Parts: none");
            }
            else
            {
                foreach (var part in job.Parts)
                {
                    var where = part.InStock ? "in the rack" : "order in";
                    lines.Add($"Part: {part.Name} {part.Eur} EUR ({where})");
                }
            }

            lines.Add(job.Notes);
            lines.Add("");
        }

        return string.Join("\n", lines).Trim();
    }

    public static string LookupJob(string query)
    {
        var q = query.ToLowerInvariant().Trim();
        if (Jobs.TryGetValue(q, out var exact))
        {
            return JobBlock(exact);
        }

        var hits = new List<string>();
        foreach (var job in Catalog.Jobs)
        {
            var blob = $"{job.Id} {job.Name} {string.Join(" ", job.Fits)}".ToLowerInvariant();
            var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => word.Length > 2);
            if (blob.Contains(q) || words.Any(word => blob.Contains(word)))
            {
                hits.Add(JobBlock(job));
            }
        }

        return hits.Count == 0
            ? $"No job matched {JsonSerializer.Serialize(query)}."
            : string.Join("\n\n", hits);
    }

    public static string LabourLine(Job job)
    {
        var hours = job.LabourHours;
        return $"{hours} h x {Rate} EUR = {Math.Round(hours * Rate)} EUR";
    }

    public static string PartsLine(Job job)
    {
        if (job.Parts.Count == 0)
        {
            return "No parts on this job.";
        }

        return string.Join(
            "; ",
            job.Parts.Select(part =>
            {
                var where = part.InStock ? "in the rack" : "order in";
                return $"{part.Name} {part.Eur} EUR ({where})";
            }));
    }

    public static Job RequireJob(string jobId)
    {
        if (!Jobs.TryGetValue(jobId, out var job))
        {
            throw new InvalidOperationException($"Unknown job {jobId}");
        }

        return job;
    }

    private static string JobBlock(Job job)
    {
        return string.Join(
            "\n",
            [
                $"{job.Id}: {job.Name} ({string.Join(", ", job.Fits)}), {job.Bay} bay",
                $"Labour: {LabourLine(job)}",
                $"Parts: {PartsLine(job)}",
                job.Notes,
            ]);
    }

    private static ServiceBook Load()
    {
        var path = Path.Combine(RepoRoot.Find(), "data", "service_book.json");
        using var stream = File.OpenRead(path);
        var raw = JsonSerializer.Deserialize<RawBook>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Empty service book.");
        return new ServiceBook(
            raw.Garage,
            raw.LabourEurPerHour,
            raw.Jobs.Select(job => new Job(
                job.Id,
                job.Name,
                job.Bay,
                job.Fits,
                job.LabourHours,
                job.Parts.Select(part => new Part(part.Sku, part.Name, part.Eur, part.InStock)).ToArray(),
                job.Notes)).ToArray());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private sealed record RawBook(
        string Garage,
        double LabourEurPerHour,
        List<RawJob> Jobs);

    private sealed record RawJob(
        string Id,
        string Name,
        string Bay,
        List<string> Fits,
        double LabourHours,
        List<RawPart> Parts,
        string Notes);

    private sealed record RawPart(string Sku, string Name, double Eur, bool InStock);
}
