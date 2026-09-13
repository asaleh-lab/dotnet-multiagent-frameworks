using BayThree;
using Microsoft.Extensions.AI;

/// <summary>
/// LangGraph is a Python/JS runtime. This script keeps the same three
/// workflow shapes (chain, routing, parallel) as explicit C# steps
/// plus Microsoft.Extensions.AI chat and structured output.
/// </summary>
var chat = OpenAiEnv.CreateChatClient();

var intakes = new[]
{
    "polo is due an oil change, interval light is on",
    "grinding from the front when I brake, golf 1.4",
};

Console.WriteLine("=== Chain ===");
foreach (var intake in intakes)
{
    var result = await RunChainAsync(chat, intake);
    Console.WriteLine($"Intake: {intake}");
    Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    Console.WriteLine($"Tidy: {result.Tidy}");
    Console.WriteLine($"Job: {result.JobId}");
    Console.WriteLine($"Card: {result.Card}");
    Console.WriteLine();
}

Console.WriteLine("=== Route ===");
foreach (var intake in intakes)
{
    var result = await RunRouteAsync(chat, intake);
    Console.WriteLine($"Intake: {intake}");
    Console.WriteLine($"Bay: {result.Bay}");
    Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    Console.WriteLine($"Note: {result.Note}");
    Console.WriteLine();
}

Console.WriteLine("=== Parallel ===");
foreach (var jobId in new[] { "oil-filter", "discs-front-golf" })
{
    var result = await RunParallelAsync(jobId);
    Console.WriteLine($"Job: {jobId}");
    Console.WriteLine($"Path: {string.Join(" -> ", result.Path)}");
    Console.WriteLine($"Labour: {result.Labour}");
    Console.WriteLine($"Parts: {result.Parts}");
    Console.WriteLine($"Card: {result.Card}");
    Console.WriteLine();
}

static async Task<ChainState> RunChainAsync(IChatClient chat, string intake)
{
    var state = new ChainState { Intake = intake };
    await TidyIntakeAsync(chat, state);
    await MatchJobAsync(chat, state);
    WriteCard(state);
    return state;
}

static async Task TidyIntakeAsync(IChatClient chat, ChainState state)
{
    var response = await chat.GetResponseAsync(
        "One short sentence for the board at Bay Three. Keep the car and the fault. " +
        $"Intake: {state.Intake}",
        OpenAiEnv.ChatOptions());
    state.Tidy = OpenAiEnv.Text(response);
    state.Path.Add("tidy_intake");
}

static async Task MatchJobAsync(IChatClient chat, ChainState state)
{
    var pick = await chat.GetResponseAsync<JobPick>(
        "Pick one job from the Bay Three service book.\n" +
        $"{Book.JobLines()}\n" +
        $"Intake: {state.Tidy}",
        OpenAiEnv.ChatOptions());
    state.JobId = pick.Result?.JobId ?? throw new InvalidOperationException("No job pick.");
    state.Path.Add("match_job");
}

static void WriteCard(ChainState state)
{
    var job = Book.RequireJob(state.JobId);
    state.Card = $"{job.Name}. {Book.LabourLine(job)}. {Book.PartsLine(job)}. {job.Notes}";
    state.Path.Add("write_card");
}

static async Task<RouteState> RunRouteAsync(IChatClient chat, string intake)
{
    var state = new RouteState { Intake = intake };
    await StampBayAsync(chat, state);
    if (state.Bay == "repair")
    {
        RepairBay(state);
    }
    else
    {
        ServiceBay(state);
    }

    return state;
}

static async Task StampBayAsync(IChatClient chat, RouteState state)
{
    var choice = await chat.GetResponseAsync<BayRoute>(
        "You send cars at Bay Three to one bay. " +
        $"Intake: {state.Intake}",
        OpenAiEnv.ChatOptions());
    state.Bay = choice.Result?.Bay ?? "service";
    state.Path.Add("stamp_bay");
}

static void ServiceBay(RouteState state)
{
    state.Note = "Service bay. We will look in the book for oil, checks, wipers, and plugs.";
    state.Path.Add("service_bay");
}

static void RepairBay(RouteState state)
{
    state.Note = "Repair bay. We will look in the book for brakes, battery, hoses, and punctures.";
    state.Path.Add("repair_bay");
}

static async Task<BookState> RunParallelAsync(string jobId)
{
    var state = new BookState { JobId = jobId };
    var labour = Task.Run(() => LookupLabour(state));
    var parts = Task.Run(() => LookupParts(state));
    await Task.WhenAll(labour, parts);
    JoinCard(state);
    return state;
}

static void LookupLabour(BookState state)
{
    var job = Book.RequireJob(state.JobId);
    state.Labour = Book.LabourLine(job);
    lock (state.Path)
    {
        state.Path.Add("lookup_labour");
    }
}

static void LookupParts(BookState state)
{
    var job = Book.RequireJob(state.JobId);
    state.Parts = Book.PartsLine(job);
    lock (state.Path)
    {
        state.Path.Add("lookup_parts");
    }
}

static void JoinCard(BookState state)
{
    var job = Book.RequireJob(state.JobId);
    state.Card = $"{job.Name}. {state.Labour}. {state.Parts}. {job.Notes}";
    state.Path.Add("join_card");
}

sealed class ChainState
{
    public string Intake { get; set; } = "";
    public string Tidy { get; set; } = "";
    public string JobId { get; set; } = "";
    public string Card { get; set; } = "";
    public List<string> Path { get; } = [];
}

sealed class RouteState
{
    public string Intake { get; set; } = "";
    public string Bay { get; set; } = "";
    public string Note { get; set; } = "";
    public List<string> Path { get; } = [];
}

sealed class BookState
{
    public string JobId { get; set; } = "";
    public string Labour { get; set; } = "";
    public string Parts { get; set; } = "";
    public string Card { get; set; } = "";
    public List<string> Path { get; } = [];
}
