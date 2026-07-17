# eSureHi - Municipal Insurance Management System

## 🤖 Agentic Swarm Architecture

This project is managed by an integrated agentic swarm designed to maintain workflow integrity and architectural consistency.

### Swarm Registry

| Agent | Role | Status |
| :--- | :--- | :--- |
| **Orchestrator** | Global coordination and task routing. | [Active] |
| **Login Worker** | UI/Auth, Bootstrap logic, and Connection Settings. | [Active] |
| **Kagawad Rodel** | Workflow & Logic Reviewer (Municipal Common Sense). | [Active] |

### Core Workflows

1. **Secure Connection Management**
   - **Local/Remote**: Locked/Master presets (Admin-only modification).
   - **Network (LAN)**: User-configurable for municipal network setups.
   - **Implementation**: Managed via `SettingsViewModel` (Master) and `ConnectionSettingsViewModel` (Client).

2. **Login & Identity**
   - **Dual-Panel UI**: 58/42 split (Branding vs. Input).
   - **Bootstrap**: Automatic detection of initial system state.

3. **Recycle Strategy**
   - Reuse existing `DatabaseConfiguration` and `SharedDatabaseConfiguration` logic.
   - Maintain `SharedConnections.cs` for Remote GGMS and CRS integrations.

### Guidelines for Agents
- **Security**: No hardcoded passwords. Use `BCrypt.Net`. Secure `eSureHiConfig.txt`.
- **UI**: Adhere to the side-tabbed navigation in Admin Settings.
- **Tone**: Professional municipal standards, with Kagawad Rodel providing "human-centric" logic audits.

---

## 🏗️ System Architecture & Logic Map

The following is an exhaustive map of the eSureHi system, detailing the core modules, their associated files, and the underlying business logic. This serves as the definitive reference for all agents operating within the swarm.

### 1. Identity, Security & Shell Routing
*   **Components**: `Services/AuthService.cs`, `ViewModels/Shared/LoginViewModel.cs`, `ViewModels/Admin/AdminShellViewModel.cs`, `ViewModels/Admin/EmployeeShellViewModel.cs`.
*   **Core Logic**: 
    *   **Bootstrap**: `InitialAdminSetupService` (via `AuthService`) detects an empty `SystemUsers` table and prompts the creation of the default `admin` account.
    *   **Authentication**: Uses `BCrypt.Net` for password hashing. Never logs raw passwords.
    *   **Routing**: Upon successful login, the system evaluates the user's role and boots either the `AdminShell` (full access) or the `EmployeeShell` (self-service portal).

### 2. Employee & Department Management
*   **Components**: `Models/Employee.cs`, `Models/Department.cs`, `ViewModels/Admin/EmployeesViewModel.cs`, `ViewModels/Admin/EmployeeFormViewModel.cs`, `ViewModels/Admin/DepartmentsViewModel.cs`.
*   **Core Logic**:
    *   Manages the municipal workforce.
    *   Employees are assigned to Departments. A Department cannot be deactivated if it has active employees.
    *   Serves as the central hub: Employees are the root entity to which Policies, Claims, and Cedulas are attached.

### 3. Beneficiaries, Cedula & CRS Integration
*   **Components**: `Models/Beneficiary.cs`, `Models/Cedula.cs`, `Services/CrsImportService.cs`, `ViewModels/Admin/CedulaManagementViewModel.cs`, `ViewModels/Admin/BeneficiaryStagingViewModel.cs`.
*   **Core Logic**:
    *   **CRS Sync**: Imports citizen data from the remote Civil Registry System (CRS) database. To handle large datasets (40,000+ records), data is pulled into a `BeneficiaryStaging` area before being confirmed.
    *   **Cedula Tracking**: Issues and tracks Community Tax Certificates (Cedula) for employees and beneficiaries, linking them to their profiles for validation during claims.

### 4. Insurance Policies & Coverage
*   **Components**: `Models/InsurancePolicy.cs`, `Models/EmployeePolicy.cs`, `ViewModels/Admin/PoliciesViewModel.cs`, `ViewModels/Admin/AssignPolicyViewModel.cs`.
*   **Core Logic**:
    *   **Products**: `InsurancePolicy` defines the template (e.g., "Health Care 2026", "Accident Insurance").
    *   **Contracts**: `EmployeePolicy` is the actual binding contract between an Employee and an Insurance Policy. All subsequent Premiums and Claims are tied to this specific `EmployeePolicy` ID, not just the generic policy.

### 5. Premiums & Billing Engine
*   **Components**: `Models/Premium.cs`, `Models/Contribution.cs`, `ViewModels/Admin/PremiumsViewModel.cs`, `ViewModels/Admin/GenerateScheduleViewModel.cs`, `ViewModels/Admin/RecordPaymentViewModel.cs`.
*   **Core Logic**:
    *   **Schedule Generation**: The system generates monthly `Premium` schedules based on active `EmployeePolicies`.
    *   **Payment Ledger**: When a payment is made, a `Contribution` record is created and linked to the `Premium`. Partial payments are supported; the system calculates balances dynamically (`Premium Amount - Sum(Contributions)`).

### 6. Claims Processing & State Machine
*   **Components**: `Models/Claim.cs`, `Models/ClaimDocument.cs`, `ViewModels/Admin/ClaimsViewModel.cs`, `ViewModels/Admin/ClaimFormViewModel.cs`, `ViewModels/Admin/ClaimDetailViewModel.cs`.
*   **Core Logic**:
    *   **Workflow**: Claims follow a strict state machine: `Draft` -> `Submitted` -> `Under Review` -> `Approved` / `Rejected` -> `Paid`.
    *   **Validation**: An Approved claim must have supporting `ClaimDocuments` attached. Claims are validated against the active status of the underlying `EmployeePolicy`.

### 7. Benefits Tracking
*   **Components**: `Models/Benefit.cs`, `ViewModels/Admin/BenefitsViewModel.cs`, `ViewModels/Admin/UseBenefitViewModel.cs`.
*   **Core Logic**:
    *   Manages non-monetary or fixed-usage perks (e.g., "Free Annual Checkup").
    *   Tracks usage counts. A benefit cannot be used if its maximum allocation has been reached.

### 8. Document Routing (eGoogGov Integration)
*   **Components**: `Models/Document.cs`, `Models/DocumentTransaction.cs`, `Models/DocumentType.cs`, `Services/eGoogGovService.cs`, `ViewModels/Admin/DocumentsViewModel.cs`.
*   **Core Logic**:
    *   A routing engine for physical and digital paperwork.
    *   Documents are logged with `DocumentTransactions` to track their physical location (e.g., "Received at Mayor's Office", "Released to Accounting").

### 9. Synchronization & Data Persistence
*   **Components**: `Data/DatabaseConfiguration.cs`, `Data/SharedConnections.cs`, `Services/OfflineOnlineSyncService.cs`, `Services/BackupService.cs`.
*   **Core Logic**:
    *   **Database Modes**: Tri-state logic (Local offline, LAN Network, Remote online).
    *   **Sync**: A custom Two-Way synchronization engine that compares timestamps (`UpdatedAt`) between the local offline MySQL instance and the Remote Host database to upsert records.
    *   **Backups**: Generates JSON-based backups with Full, Differential, and Incremental strategies.

### 10. Reporting & Auditing
*   **Components**: `Models/AuditLog.cs`, `Services/AuditService.cs`, `Services/ReportExportService.cs`, `ViewModels/Admin/ReportsViewModel.cs`.
*   **Core Logic**:
    *   **Audit Trail**: Every critical action (Login, Claim Approval, Premium Generation) is recorded in the `AuditLogs` for accountability.
    *   **Export**: Generates dynamic reports utilizing QuestPDF for PDF generation and ClosedXML for Excel exports.


## Verification
- Read the task description and implementation notes.
- Inspect the relevant files in `Services/`, `Models/`, and `ViewModels/`.
- Cross-reference with `docs/budget-module-design.md` and other design documents.
- Use `dotnet test` to verify that business logic remains sound after changes.
- **Engage in Planning:** If a feature or fix is complex, initiate a discussion with the developer. Summarize the current understanding and ask for clarification on the desired "story" or workflow for that feature.