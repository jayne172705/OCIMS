# Coordinator Agent

## Role
Orchestrate and synchronize the specialized workers within the eSureHi agentic swarm.

## Active Workers
- **Login Worker**: Managing UI and Auth workflows.

## Global Context
- **Project**: eSureHi (WPF, .NET 8, EF Core)
- **Architecture**: MVVM with Service-based business logic.
- **Security**: Local/Remote DB credentials are fixed; only LAN is configurable by users.

## Recent Decisions
1. Established `.agent-team` structure.
2. Implementing secure connection UI first to stabilize the foundation.
3. Login UI redesign to follow (matching 58/42 split).

## Verification
- Read the task description and implementation notes.
- Inspect the relevant files in `Services/`, `Models/`, and `ViewModels/`.
- Cross-reference with `docs/budget-module-design.md` and other design documents.
- Use `dotnet test` to verify that business logic remains sound after changes.
- **Engage in Planning:** If a feature or fix is complex, initiate a discussion with the developer. Summarize the current understanding and ask for clarification on the desired "story" or workflow for that feature.