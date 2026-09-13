using System.Text.Json;
using BayThree;

/// <summary>
/// One Bay Three crew: YAML, book tool, and a typed job card.
/// CrewAI-pattern stand-in on Microsoft.Extensions.AI, not CrewAI.
/// </summary>
var agents = CrewConfig.LoadAgents();
var tasks = CrewConfig.LoadTasks("tasks_tools.yaml");

var advisor = new Agent(CrewConfig.RequireSpec(agents.GetValueOrDefault("advisor"), "advisor"));
var clerk = new Agent(
    CrewConfig.RequireSpec(agents.GetValueOrDefault("clerk"), "clerk"),
    [BookTools.LookupServiceBook]);
var writer = new Agent(CrewConfig.RequireSpec(agents.GetValueOrDefault("writer"), "writer"));

var pickJob = new CrewTask("pick_job", CrewConfig.RequireSpec(tasks.GetValueOrDefault("pick_job"), "pick_job"), advisor);
var copyBook = new CrewTask(
    "copy_book",
    CrewConfig.RequireSpec(tasks.GetValueOrDefault("copy_book"), "copy_book"),
    clerk,
    [pickJob]);
var writeCard = new CrewTask(
    "write_card",
    CrewConfig.RequireSpec(tasks.GetValueOrDefault("write_card"), "write_card"),
    writer,
    [pickJob, copyBook],
    outputType: typeof(JobCard));

var crew = new Crew([advisor, clerk, writer], [pickJob, copyBook, writeCard]);
var result = await crew.KickoffAsync(new Dictionary<string, string>
{
    ["intake"] = Book.Intake,
    ["jobs"] = Book.JobLines(),
});
Console.WriteLine($"Intake: {Book.Intake}");
if (result.Parsed is JobCard card)
{
    Console.WriteLine(JsonSerializer.Serialize(card, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    }));
}
else
{
    Console.WriteLine($"Card: {result.Text}");
}
