# dotnet-multiagent-frameworks

This repo demonstrates how to run the same garage job as a graph workflow, then later as a crew, an AG2-style chat, and a BeeAI-style agent. The example we will use is Bay Three, a one-lift neighbourhood garage. In our case we need a job card from the service book.

LangGraph, CrewAI, BeeAI, and AG2 are not .NET runtimes. We keep the same roles and the same job. The graph scripts are an explicit C# stand-in for LangGraph. The crew scripts are a CrewAI-pattern stand-in on Microsoft.Extensions.AI. The BeeAI script uses Semantic Kernel for the agent, the book plugin, and chat history. The AG2 script uses the same conversation patterns, two agents then a group, without the AutoGen runtime.

This article is a practical implementation of the concepts in Agentic AI with LangGraph, CrewAI, AutoGen and BeeAI.

## Setup

```powershell
dotnet restore
copy .env.example .env
```

Put your OpenAI API key in `.env`.

## Send a car through three graph shapes

```powershell
dotnet run --project src/LangGraphPatterns
```

## Orchestrate the book, then score the card

```powershell
dotnet run --project src/LangGraphOrchestrate
```

## Send the same intake through a crew

```powershell
dotnet run --project src/CrewBasics
```

## Fill a typed job card

```powershell
dotnet run --project src/CrewStructured
```

## Hang the book tool on the agent, then on the task

```powershell
dotnet run --project src/CrewTools
```

## Run the crew with the tool and the card together

```powershell
dotnet run --project src/CrewApplied
```

## Look up the book with a kernel agent, then ask what we wrote

```powershell
dotnet run --project src/BeeaiWorkflow
```

## Write the card in a two-agent chat, then in a group

```powershell
dotnet run --project src/Ag2Chat
```

## Run the same job on three frameworks

```powershell
dotnet run --project src/CrewApplied
dotnet run --project src/Ag2Chat
dotnet run --project src/BeeaiWorkflow
```

Same intake, same service book. Only the framework stand-in changes. The crew run is a CrewAI-pattern stand-in on Microsoft.Extensions.AI. The chat run is an AG2 conversation pattern on explicit C# turns. The last run is the BeeAI skill on Semantic Kernel. You should see oil-filter on the Polo, 0.5 h, 45 EUR, 5W-30 and the OC47 filter in the rack. Wording can differ. The hours, euros, and stock should not.
