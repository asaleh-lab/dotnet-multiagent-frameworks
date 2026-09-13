using BayThree;

/// <summary>
/// CrewAI-pattern stand-in: advisor, book clerk, and card writer. No tools yet.
/// This is not the Python CrewAI runtime. Sequential kickoff is explicit C#
/// over Microsoft.Extensions.AI.
/// </summary>
var agents = CrewConfig.LoadAgents();
var tasks = CrewConfig.LoadTasks();

var advisor = new Agent(CrewConfig.RequireSpec(agents.GetValueOrDefault("advisor"), "advisor"));
var clerk = new Agent(CrewConfig.RequireSpec(agents.GetValueOrDefault("clerk"), "clerk"));
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
    [pickJob, copyBook]);

var crew = new Crew([advisor, clerk, writer], [pickJob, copyBook, writeCard]);
var result = await crew.KickoffAsync(new Dictionary<string, string>
{
    ["intake"] = Book.Intake,
    ["book"] = Book.BookText(),
});
Console.WriteLine($"Intake: {Book.Intake}");
Console.WriteLine($"Card: {result.Text}");
