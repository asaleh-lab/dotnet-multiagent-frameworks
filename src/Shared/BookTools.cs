using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace BayThree;

public static class BookTools
{
    public static AIFunction LookupServiceBook { get; } = AIFunctionFactory.Create(
        Lookup,
        name: "lookup_service_book",
        description:
            "Look up one job in the Bay Three service book. " +
            "Returns labour hours, parts, stock, and the note. " +
            "Use this instead of guessing prices.");

    [Description("Look up one job in the Bay Three service book by id, name, or car.")]
    public static string Lookup(
        [Description("Job id, name, or car, for example oil-filter or Polo.")] string query)
    {
        return Book.LookupJob(query);
    }
}

/// <summary>
/// Semantic Kernel plugin stand-in for the BeeAI book tool.
/// </summary>
public sealed class BookPlugin
{
    [KernelFunction("lookup_service_book")]
    [Description("Look up one job in the Bay Three service book by id, name, or car.")]
    public string LookupServiceBook(
        [Description("Job id, name, or car, for example oil-filter or Polo.")] string query)
    {
        return Book.LookupJob(query);
    }
}
