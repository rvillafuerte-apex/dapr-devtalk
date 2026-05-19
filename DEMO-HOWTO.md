# Dapr DevTalk Demo

A 2-service .NET 8 demo showcasing Dapr building blocks for a 1-hour introductory talk.

## Architecture

```
[Browser/curl]
     │
     ▼
┌─────────────┐   Dapr Service Invocation   ┌──────────────────┐
│  ApiGateway │ ──────────────────────────► │  WeatherService  │
│  :5000      │                             │  :5001           │
└─────────────┘                             └──────────────────┘
     │                                               ▲
     │        Dapr Pub/Sub (Redis)                   │
     └───────────── "weather-requested" ─────────────┘
     │
     │        Dapr Conversation API (AI)
     └───────────── GitHub Models ──────► LLM (gpt-4o-mini)
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## Setup

```bash
# Initialize Dapr (installs Redis + Zipkin locally)
dapr init

# Restore dependencies
dotnet restore DaprDemo.sln
```

## Running the Demo

Open **two terminals**:

**Terminal 1 — WeatherService**
```bash
dapr run --app-id weather-service --app-port 5001 --dapr-http-port 3501 --resources-path ./components -- dotnet run --project WeatherService
```

**Terminal 2 — ApiGateway**
```bash
dapr run --app-id api-gateway --app-port 5000 --dapr-http-port 3500 --resources-path ./components -- dotnet run --project ApiGateway
```

## Demo Endpoints

| Endpoint | What it shows |
|---|---|
| `GET http://localhost:5000/weather` | Service invocation |
| `GET http://localhost:5000/weather/publish` | Pub/Sub |
| `GET http://localhost:5000/weather/summarize` | AI (Conversation API) |
| `GET http://localhost:5000/secrets` | Secrets building block |
| `http://localhost:9411` | Zipkin distributed tracing |

## For the AI Demo (GitHub Copilot Enterprise)

Uses **GitHub Models** — no OpenAI account needed, just your GitHub PAT.

**Step 1** — Create a GitHub Personal Access Token (no special scopes required):
> github.com → Settings → Developer settings → Personal access tokens

**Step 2** — Set the env variable before running:
```bash
set GITHUB_TOKEN=github_pat_xxxx...
```

The `components/conversation.yaml` is already configured to use `https://models.inference.ai.azure.com` with `gpt-4o-mini`.

> 💡 **Swap to direct OpenAI anytime** by removing the `endpoint` line and setting `GITHUB_TOKEN` to your `sk-...` key — zero C# changes needed.
