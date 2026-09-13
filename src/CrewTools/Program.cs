using BayThree;
using Microsoft.Extensions.AI;

/// <summary>
/// Look up the service book with a tool on the agent, then on the task.
/// CrewAI-pattern stand-in. The tool is a Microsoft.Extensions.AI function,
/// not a CrewAI BaseTool.
/// </summary>
var inputs = new Dictionary<string, string>
{
    ["intake"] = Book.Intake,
    ["jobs"] = Book.JobLines(),
};

Console.WriteLine("=== Tool on the agent ===");
Console.WriteLine($"Intake: {Book.Intake}");
Console.WriteLine($"Card: {(await MakeCrew("agent").KickoffAsync(inputs)).Text}");
Console.WriteLine();
Console.WriteLine("=== Tool on the task ===");
Console.WriteLine($"Intake: {Book.Intake}");
Console.WriteLine($"Card: {(await MakeCrew("task").KickoffAsync(inputs)).Text}");

static Crew MakeCrew(string where)
{
    var agents = CrewConfig.LoadAgents();
    var tasks = CrewConfig.LoadTasks("tasks_tools.yaml");
    AITool[] bookTool = [BookTools.LookupServiceBook];
    var advisor = new Agent(CrewConfig.RequireSpec(agents.GetValueOrDefault("advisor"), "advisor"));
    var clerk = new Agent(
        CrewConfig.RequireSpec(agents.GetValueOrDefault("clerk"), "clerk"),
        where == "agent" ? bookTool : []);
    var writer = new Agent(CrewConfig.RequireSpec(agents.GetValueOrDefault("writer"), "writer"));
    var pickJob = new CrewTask("pick_job", CrewConfig.RequireSpec(tasks.GetValueOrDefault("pick_job"), "pick_job"), advisor);
    var copyBook = new CrewTask(
        "copy_book",
        CrewConfig.RequireSpec(tasks.GetValueOrDefault("copy_book"), "copy_book"),
        clerk,
        [pickJob],
        where == "task" ? bookTool : []);
    var writeCard = new CrewTask(
        "write_card",
        CrewConfig.RequireSpec(tasks.GetValueOrDefault("write_card"), "write_card"),
        writer,
        [pickJob, copyBook]);
    return new Crew([advisor, clerk, writer], [pickJob, copyBook, writeCard]);
}
