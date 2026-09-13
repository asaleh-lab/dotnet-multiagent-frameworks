using BayThree;
using Microsoft.Extensions.AI;

/// <summary>
/// LangGraph Send and evaluator-optimizer loops are not a .NET runtime.
/// This script keeps the same plan-then-fan-out and draft-score-rewrite
/// shapes in explicit C# with Microsoft.Extensions.AI.
/// </summary>
const int MaxRounds = 2;
var chat = OpenAiEnv.CreateChatClient();

Console.WriteLine("=== Orchestrator ===");
foreach (var intake in new[]
{
    "polo is due an oil change, interval light is on",
    "golf 1.4, annual check, no extras",
})
{
    var result = await RunOrchestratorAsync(chat, intake);
    Console.WriteLine($"Intake: {intake}");
    Console.WriteLine($"Job: {result.JobId}");
    Console.WriteLine($"Workers: {string.Join(", ", result.Workers)}");
    Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    Console.WriteLine($"Card: {result.Card}");
    Console.WriteLine();
}

Console.WriteLine("=== Evaluator-optimizer ===");
foreach (var jobId in new[] { "oil-filter", "discs-front-golf" })
{
    Console.WriteLine($"Job: {jobId}\n");
    var path = new List<string>();
    var state = new EvalState { JobId = jobId };
    while (true)
    {
        await DraftCardAsync(chat, state);
        path.AddRange(state.LastPath);
        Console.WriteLine($"Draft (round {state.Round}):\n{state.Card}\n");
        Evaluate(state);
        path.AddRange(state.LastPath);
        Console.WriteLine($"Misses: {(state.Misses.Count == 0 ? "none" : string.Join(", ", state.Misses))}");
        Console.WriteLine($"Ready: {state.Ok}\n");
        if (state.Ok || state.Round >= MaxRounds)
        {
            break;
        }
    }

    Console.WriteLine($"Path: {string.Join(" -> ", path)}");
    Console.WriteLine();
}

static async Task<OrchState> RunOrchestratorAsync(IChatClient chat, string intake)
{
    var state = new OrchState { Intake = intake };
    await PlanAsync(chat, state);
    await Task.WhenAll(FanOut(state));
    Assemble(state);
    return state;
}

static async Task PlanAsync(IChatClient chat, OrchState state)
{
    var choice = await chat.GetResponseAsync<WorkPlan>(
        "You are the service advisor at Bay Three. Pick one job. " +
        "Send labour. Send parts only if that job has parts.\n" +
        $"{Book.JobLines(withParts: true)}\n" +
        $"Intake: {state.Intake}",
        OpenAiEnv.ChatOptions());
    var plan = choice.Result ?? throw new InvalidOperationException("No plan.");
    state.JobId = plan.JobId;
    state.Workers = plan.Workers;
    state.Path.Add("plan");
}

static IEnumerable<Task> FanOut(OrchState state)
{
    var work = new List<Task>();
    if (state.Workers.Contains("labour"))
    {
        work.Add(Task.Run(() => LookupLabour(state)));
    }

    if (state.Workers.Contains("parts"))
    {
        work.Add(Task.Run(() => LookupParts(state)));
    }

    return work;
}

static void LookupLabour(OrchState state)
{
    var job = Book.RequireJob(state.JobId);
    state.Labour = Book.LabourLine(job);
    lock (state.Path)
    {
        state.Path.Add("lookup_labour");
    }
}

static void LookupParts(OrchState state)
{
    var job = Book.RequireJob(state.JobId);
    state.Parts = Book.PartsLine(job);
    lock (state.Path)
    {
        state.Path.Add("lookup_parts");
    }
}

static void Assemble(OrchState state)
{
    var job = Book.RequireJob(state.JobId);
    var labour = string.IsNullOrEmpty(state.Labour) ? Book.LabourLine(job) : state.Labour;
    var parts = string.IsNullOrEmpty(state.Parts) ? "No parts on this job." : state.Parts;
    state.Card = $"{job.Name}. {labour}. {parts}. {job.Notes}";
    state.Path.Add("assemble");
}

static async Task DraftCardAsync(IChatClient chat, EvalState state)
{
    state.Round += 1;
    var job = Book.RequireJob(state.JobId);
    string prompt;
    if (state.Misses.Count > 0)
    {
        prompt =
            "Rewrite this Bay Three job card. Fix every miss. " +
            "Use only these book lines.\n" +
            $"Job: {job.Name}\n" +
            $"Labour: {Book.LabourLine(job)}\n" +
            $"Parts: {Book.PartsLine(job)}\n" +
            $"Notes: {job.Notes}\n" +
            $"Draft: {state.Card}\n" +
            $"Misses: {string.Join("; ", state.Misses)}";
    }
    else
    {
        prompt =
            "Write a short job card for a neighbourhood garage. " +
            "Do not use hours, euros, or stock. " +
            $"Job: {job.Name}";
    }

    var reply = await chat.GetResponseAsync(prompt, OpenAiEnv.ChatOptions());
    state.Card = OpenAiEnv.Text(reply);
    state.LastPath = ["draft_card"];
}

static void Evaluate(EvalState state)
{
    var job = Book.RequireJob(state.JobId);
    var card = state.Card.ToLowerInvariant();
    var misses = new List<string>();
    if (!card.Contains($"{job.LabourHours} h") && !card.Contains($"{job.LabourHours}h"))
    {
        misses.Add("labour hours from the book");
    }

    if (job.Parts.Any(part => !part.InStock) && !card.Contains("order"))
    {
        misses.Add("say order in for parts not on the rack");
    }

    var named = job.Parts.Any(part =>
    {
        var first = part.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is not null && card.Contains(first.ToLowerInvariant());
    });
    if (job.Parts.Count > 0 && !named)
    {
        misses.Add("name a part from the book");
    }

    state.Misses = misses;
    state.Ok = misses.Count == 0;
    state.LastPath = ["evaluate"];
}

sealed class OrchState
{
    public string Intake { get; set; } = "";
    public string JobId { get; set; } = "";
    public List<string> Workers { get; set; } = [];
    public string Labour { get; set; } = "";
    public string Parts { get; set; } = "";
    public string Card { get; set; } = "";
    public List<string> Path { get; } = [];
}

sealed class EvalState
{
    public string JobId { get; set; } = "";
    public string Card { get; set; } = "";
    public List<string> Misses { get; set; } = [];
    public bool Ok { get; set; }
    public int Round { get; set; }
    public List<string> LastPath { get; set; } = [];
}
