using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BayThree;

/// <summary>
/// CrewAI-pattern stand-in for .NET. CrewAI has no first-class .NET runtime.
/// These objects keep the same YAML roles, sequential tasks, and optional tools.
/// They are not CrewAI.
/// </summary>
public sealed class AgentSpec
{
    public string Role { get; set; } = "";
    public string Goal { get; set; } = "";
    public string Backstory { get; set; } = "";
    public bool AllowDelegation { get; set; }
    public int MaxIter { get; set; } = 5;
}

public sealed class TaskSpec
{
    public string Description { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
}

public sealed class Agent
{
    public Agent(AgentSpec spec, IReadOnlyList<AITool>? tools = null)
    {
        Spec = spec;
        Tools = tools ?? [];
    }

    public AgentSpec Spec { get; }
    public IReadOnlyList<AITool> Tools { get; }
}

public sealed class CrewTask
{
    public CrewTask(
        string name,
        TaskSpec spec,
        Agent agent,
        IReadOnlyList<CrewTask>? context = null,
        IReadOnlyList<AITool>? tools = null,
        Type? outputType = null)
    {
        Name = name;
        Spec = spec;
        Agent = agent;
        Context = context ?? [];
        Tools = tools ?? [];
        OutputType = outputType;
    }

    public string Name { get; }
    public TaskSpec Spec { get; }
    public Agent Agent { get; }
    public IReadOnlyList<CrewTask> Context { get; }
    public IReadOnlyList<AITool> Tools { get; }
    public Type? OutputType { get; }
    public string Output { get; set; } = "";
    public object? Parsed { get; set; }
}

public sealed class CrewResult
{
    public required string Text { get; init; }
    public object? Parsed { get; init; }
}

public sealed class Crew
{
    public Crew(IReadOnlyList<Agent> agents, IReadOnlyList<CrewTask> tasks)
    {
        Agents = agents;
        Tasks = tasks;
    }

    public IReadOnlyList<Agent> Agents { get; }
    public IReadOnlyList<CrewTask> Tasks { get; }

    public async Task<CrewResult> KickoffAsync(IReadOnlyDictionary<string, string> inputs)
    {
        var lastText = "";
        object? lastParsed = null;
        foreach (var task in Tasks)
        {
            var result = await RunTaskAsync(task, inputs);
            task.Output = result.Text;
            task.Parsed = result.Parsed;
            lastText = result.Text;
            lastParsed = result.Parsed;
        }

        return new CrewResult { Text = lastText, Parsed = lastParsed };
    }

    private static async Task<CrewResult> RunTaskAsync(
        CrewTask task,
        IReadOnlyDictionary<string, string> inputs)
    {
        var description = FillTemplate(task.Spec.Description, inputs);
        var context = string.Join(
            "\n\n",
            task.Context.Select(prior => $"From {prior.Name}:\n{prior.Output}"));
        var system =
            $"You are a {task.Agent.Spec.Role}. {task.Agent.Spec.Goal}\n" +
            task.Agent.Spec.Backstory;
        var user =
            $"{description}\n\nExpected output: {task.Spec.ExpectedOutput}" +
            (string.IsNullOrEmpty(context) ? "" : $"\n\nPrevious work:\n{context}");
        var tools = task.Tools.Count > 0 ? task.Tools : task.Agent.Tools;
        var maxIter = task.Agent.Spec.MaxIter > 0 ? task.Agent.Spec.MaxIter : 5;

        if (task.OutputType is not null)
        {
            var client = OpenAiEnv.CreateChatClient();
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, system),
                new(ChatRole.User, tools.Count > 0 ? $"{user}\n\nTool notes from this crew:\n{context}" : user),
            };
            var parsed = await GetStructuredAsync(client, messages, task.OutputType);
            return new CrewResult
            {
                Text = JsonSerializer.Serialize(parsed, parsed.GetType(), PrettyJson),
                Parsed = parsed,
            };
        }

        if (tools.Count == 0)
        {
            var client = OpenAiEnv.CreateChatClient();
            var response = await client.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, system),
                    new ChatMessage(ChatRole.User, user),
                ],
                OpenAiEnv.ChatOptions());
            return new CrewResult { Text = OpenAiEnv.Text(response) };
        }

        var text = await RunWithToolsAsync(system, user, tools, maxIter);
        return new CrewResult { Text = text };
    }

    private static async Task<object> GetStructuredAsync(
        IChatClient client,
        IList<ChatMessage> messages,
        Type outputType)
    {
        if (outputType == typeof(JobCard))
        {
            var response = await client.GetResponseAsync<JobCard>(messages, OpenAiEnv.ChatOptions());
            return response.Result ?? throw new InvalidOperationException("Empty job card.");
        }

        throw new InvalidOperationException($"Unsupported structured type {outputType.Name}.");
    }

    private static async Task<string> RunWithToolsAsync(
        string system,
        string user,
        IReadOnlyList<AITool> tools,
        int maxIter)
    {
        var client = OpenAiEnv.CreateChatClient(withTools: true);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, user),
        };
        var options = OpenAiEnv.ChatOptions(tools);
        ChatResponse? last = null;
        for (var i = 0; i < maxIter; i++)
        {
            last = await client.GetResponseAsync(messages, options);
            messages.AddMessages(last);
            if (last.Messages.All(message => message.Contents.OfType<FunctionCallContent>().Any() is false))
            {
                return OpenAiEnv.Text(last);
            }
        }

        return last is null ? "" : OpenAiEnv.Text(last);
    }

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static string FillTemplate(string template, IReadOnlyDictionary<string, string> inputs)
    {
        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value;
            return inputs.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}

public static class CrewConfig
{
    public static IReadOnlyDictionary<string, AgentSpec> LoadAgents()
    {
        return Deserialize<Dictionary<string, AgentSpec>>("agents.yaml");
    }

    public static IReadOnlyDictionary<string, TaskSpec> LoadTasks(string file = "tasks.yaml")
    {
        return Deserialize<Dictionary<string, TaskSpec>>(file);
    }

    public static T RequireSpec<T>(T? value, string name)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"Missing YAML spec: {name}");
        }

        return value;
    }

    private static T Deserialize<T>(string file)
    {
        var path = Path.Combine(RepoRoot.Find(), "src", "config", file);
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<T>(yaml)
            ?? throw new InvalidOperationException($"Empty YAML: {file}");
    }
}
