# Login & Authentication Worker

## Objective
Maintain and enhance the Login UI, authentication workflows, and initial admin setup (bootstrap) process.

## Focus
- `Views/LoginWindow.xaml`
- `ViewModels/LoginViewModel.cs`
- `Services/AuthService.cs`
- `Services/InitialAdminSetupService.cs`

## Critical Rules (DO NOT BREAK)
- **UI Design Lock:** The 58/42 dual-panel layout is FINAL and LOCKED.
    - **Left Panel (58%):** Background `#0A1830`. Dedicated to branding and community info.
    - **Right Panel (42%):** White background. Flat design (no cards). Labels MUST stay above inputs.
- **Audit Requirement:** Any requested changes to the UI must first be approved by the **UI/UX Auditor Agent**.
- **Security First:** Never log raw passwords. Use `PasswordChanged` event.
- **Bootstrap Integrity:** `InitialAdminSetupService` is critical for first-run.
- **Connection Awareness:** Must handle connection failures and allow switching via `ConnectionSettingsDialog`.
- **Authentication:** Use `BCrypt.Net`.

## Workflow Summary
1. **Initialization:** Load branding and check DB state.
2. **Detection:** Check if Bootstrap Mode or Login Mode is required.
3. **Execution:** Login or Create Initial Admin.
4. **Feedback:** Provide user feedback via status messages.

## Verification
- Read the task description and implementation notes.
- Inspect the relevant files in `Services/`, `Models/`, and `ViewModels/`.
- Cross-reference with `docs/budget-module-design.md` and other design documents.
- Use `dotnet test` to verify that business logic remains sound after changes.
- **Engage in Planning:** If a feature or fix is complex, initiate a discussion with the developer. Summarize the current understanding and ask for clarification on the desired "story" or workflow for that feature.