using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using OpenAI;

namespace BayThree;

public static class OpenAiEnv
{
    public static string RequireApiKey()
    {
        LoadDotEnv();
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim() ?? "";
        if (string.IsNullOrEmpty(apiKey) || apiKey == "sk-your-key-here")
        {
            Console.Error.WriteLine(
                "Missing OPENAI_API_KEY. Copy .env.example to .env and put your key in.");
            Environment.Exit(1);
        }

        return apiKey;
    }

    public static string Model
    {
        get
        {
            LoadDotEnv();
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL")?.Trim();
            return string.IsNullOrEmpty(model) ? "gpt-4o-mini" : model;
        }
    }

    public static IChatClient CreateChatClient(bool withTools = false)
    {
        var apiKey = RequireApiKey();
        IChatClient client = new OpenAIClient(new ApiKeyCredential(apiKey))
            .GetChatClient(Model)
            .AsIChatClient();
        if (withTools)
        {
            client = client.AsBuilder().UseFunctionInvocation().Build();
        }

        return client;
    }

    public static Kernel CreateKernel()
    {
        var apiKey = RequireApiKey();
        return Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(Model, apiKey)
            .Build();
    }

    public static ChatOptions ChatOptions(IEnumerable<AITool>? tools = null)
    {
        var options = new ChatOptions { Temperature = 0f };
        if (tools is not null)
        {
            foreach (var tool in tools)
            {
                options.Tools ??= [];
                options.Tools.Add(tool);
            }
        }

        return options;
    }

    public static string Text(ChatResponse response)
    {
        return (response.Text ?? "").Trim();
    }

    private static bool _loaded;

    private static void LoadDotEnv()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var path = Path.Combine(RepoRoot.Find(), ".env");
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var split = line.Split('=', 2);
            var key = split[0].Trim();
            var value = split[1].Trim().Trim('"');
            if (!string.IsNullOrEmpty(key) && Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
