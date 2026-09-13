using BayThree;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// BeeAI is a Python framework. This script keeps the same skill
/// (agent, book tool, and memory) on Semantic Kernel: a chat agent,
/// a kernel plugin for the book, and ChatHistory as memory.
/// </summary>
var kernel = OpenAiEnv.CreateKernel();
kernel.ImportPluginFromType<BookPlugin>();
var chat = kernel.GetRequiredService<IChatCompletionService>();
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    Temperature = 0,
};

var history = new ChatHistory(
    "You write job cards for the lift at Bay Three. " +
    "Always call lookup_service_book before you quote hours or parts. " +
    "Do not invent euros. If a part is not on the rack, say order in.");

var first = "Write one short job card for this intake. " + Book.Intake;
history.AddUserMessage(first);
var card = await chat.GetChatMessageContentAsync(history, settings, kernel);
history.Add(card);

Console.WriteLine($"Intake: {Book.Intake}");
Console.WriteLine($"Card: {card.Content?.Trim()}");
Console.WriteLine();

var follow = "What labour hours did we put on that card?";
history.AddUserMessage(follow);
var remembered = await chat.GetChatMessageContentAsync(history, settings, kernel);
history.Add(remembered);

Console.WriteLine($"Follow-up: {follow}");
Console.WriteLine($"Reply: {remembered.Content?.Trim()}");
Console.WriteLine($"Memory: {history.Count} messages");
