using System.ComponentModel;
using System.Text.Json.Serialization;

namespace BayThree;

public sealed record PartLine(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("eur")] double Eur,
    [property: JsonPropertyName("stock")] string Stock);

public sealed record JobCard(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("car")] string Car,
    [property: JsonPropertyName("bay")] string Bay,
    [property: JsonPropertyName("labour_hours")] double LabourHours,
    [property: JsonPropertyName("labour_eur")] double LabourEur,
    [property: JsonPropertyName("parts")] List<PartLine> Parts,
    [property: JsonPropertyName("notes")] string Notes);

public sealed record JobPick(
    [property: JsonPropertyName("job_id")]
    [property: Description("One job id from the Bay Three service book.")]
    string JobId);

public sealed record BayRoute(
    [property: JsonPropertyName("bay")]
    [property: Description("service: oil, checks, wipers, plugs. repair: brakes, battery, hoses, punctures.")]
    string Bay);

public sealed record WorkPlan(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("workers")]
    [property: Description("labour always. parts only if the job has parts in the book.")]
    List<string> Workers);

public sealed record SpeakerPick(
    [property: JsonPropertyName("next")] string Next);
