using BayThree;
using Microsoft.Extensions.AI;

/// <summary>
/// AG2 (AutoGen) is a Python runtime. This script keeps the same
/// conversation patterns, two agents then a group, on
/// Microsoft.Extensions.AI with an explicit speaker loop.
/// </summary>
var chat = OpenAiEnv.CreateChatClient();
var toolsChat = OpenAiEnv.CreateChatClient(withTools: true);

Console.WriteLine("=== Two-agent chat ===");
Console.WriteLine($"Intake: {Book.Intake}");
Console.WriteLine($"Card: {await TwoAgentCardAsync(toolsChat)}");
Console.WriteLine();
Console.WriteLine("=== Group chat ===");
Console.WriteLine($"Intake: {Book.Intake}");
Console.WriteLine($"Card: {await GroupCardAsync(chat, toolsChat)}");

static string JobMessage()
{
    return
        "Write one short job card for this Bay Three intake. " +
        $"{Book.Intake} " +
        "Look up the service book. Do not invent euros. " +
        "Say TERMINATE when the card is done.";
}

static bool IsDone(string text) => text.Contains("TERMINATE", StringComparison.Ordinal);

static string StripTerminate(string text)
{
    return text.Replace("TERMINATE", " ", StringComparison.Ordinal).Trim();
}

static async Task<string> RunClerkTurnAsync(IChatClient chat, List<ChatMessage> messages)
{
    var options = OpenAiEnv.ChatOptions([BookTools.LookupServiceBook]);
    for (var hop = 0; hop < 4; hop++)
    {
        var reply = await chat.GetResponseAsync(messages, options);
        messages.AddMessages(reply);
        var calls = reply.Messages.SelectMany(message => message.Contents.OfType<FunctionCallContent>()).ToList();
        if (calls.Count == 0)
        {
            return OpenAiEnv.Text(reply);
        }
    }

    return messages.Count == 0 ? "" : (messages[^1].Text ?? "").Trim();
}

static async Task<string> TwoAgentCardAsync(IChatClient chat)
{
    var messages = new List<ChatMessage>
    {
        new(
            ChatRole.System,
            "You keep the Bay Three service book. " +
            "Call lookup_service_book before you quote hours or parts. " +
            "Write one short job card. Then say TERMINATE."),
        new(ChatRole.User, JobMessage()),
    };
    var last = "";
    for (var turn = 0; turn < 6; turn++)
    {
        last = await RunClerkTurnAsync(chat, messages);
        if (IsDone(last))
        {
            break;
        }

        messages.Add(new ChatMessage(ChatRole.User, "Finish the job card, then say TERMINATE."));
    }

    return StripTerminate(last);
}

static string GroupPrompt(string name) => name switch
{
    "advisor" =>
        "You sit at the Bay Three hatch. Pick a job id for the intake. " +
        "Ask the clerk to look it up. Do not invent euros.",
    "clerk" =>
        "You keep the service book. Call lookup_service_book. " +
        "Copy labour hours, parts, and stock. Do not invent euros.",
    _ =>
        "You write one short job card from the advisor and the clerk. " +
        "If a part is not on the rack, say order in. Then say TERMINATE.",
};

static async Task<string> GroupCardAsync(IChatClient chat, IChatClient toolsChat)
{
    var transcript = new List<(string Name, string Content)>
    {
        ("hatch", JobMessage()),
    };
    var last = "";
    var next = "advisor";

    for (var round = 0; round < 8; round++)
    {
        var history = string.Join("\n", transcript.Select(line => $"{line.Name}: {line.Content}"));
        if (round > 0)
        {
            var pick = await chat.GetResponseAsync<SpeakerPick>(
                "Pick the next speaker for a Bay Three job card chat. " +
                "advisor picks a job id. clerk looks up the book. writer writes the card. " +
                $"History:\n{history}",
                OpenAiEnv.ChatOptions());
            next = pick.Result?.Next ?? "writer";
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GroupPrompt(next)),
            new(ChatRole.User, history),
        };

        if (next == "clerk")
        {
            last = await RunClerkTurnAsync(toolsChat, messages);
        }
        else
        {
            var reply = await chat.GetResponseAsync(messages, OpenAiEnv.ChatOptions());
            last = OpenAiEnv.Text(reply);
        }

        transcript.Add((next, last));
        if (IsDone(last))
        {
            break;
        }
    }

    return StripTerminate(last);
}
