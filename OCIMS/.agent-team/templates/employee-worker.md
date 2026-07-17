# Employee Management & Self-Service Worker

## Objective
Maintain and enhance the Employee and Department management modules, with a primary focus on the employee self-service experience and the "Employee Shell" environment.

## Focus
- **Models:** `Employee.cs`, `Department.cs`
- **Shell & Navigation:** `EmployeeShellViewModel.cs`, `EmployeeDashboardViewModel.cs`
- **Self-Service Portals:**
    - `MyProfileViewModel.cs`
    - `MyBenefitsViewModel.cs`
    - `MyClaimsViewModel.cs`
    - `MyPoliciesViewModel.cs`
    - `MyPremiumsViewModel.cs`
- **Admin Management:** `EmployeesViewModel.cs`, `EmployeeFormViewModel.cs`, `DepartmentsViewModel.cs`
- **Integration Points:** Employee-initiated claim requests and Cedula status tracking.

## Critical Rules (DO NOT BREAK)
- **Restricted Access:** Ensure `EmployeeShell` only exposes features relevant to the logged-in employee. It must NOT provide access to administrative or other employees' data.
- **Privacy:** Adhere to municipal data privacy standards for all employee records.
- **Entity Centrality:** Employees are the root entity; maintain strict integrity of links to Policies, Claims, and Cedulas.
- **Department Constraint:** A department cannot be deactivated if it contains active employees.
- **MVVM Preservation:** Maintain the existing pattern of ViewModels communicating with Services for all data operations.
- **Admin Safety:** Do not modify Admin-only modules unless explicitly required for employee-facing functionality.

## Workflow Summary
1. **Employee Login/Self-Service:** Manage the transition from login to the `EmployeeShell` for non-admin users.
2. **Profile & Status:** Ensure employees can view their own profile, premium status, and benefits usage accurately.
3. **Claims Requests:** Manage the flow for employees to initiate claim requests and track their progress through the system.
4. **Onboarding & HR Management:** Maintain the administrative side of employee and department records.

## Verification
- Analyze the relevant ViewModels and Services before making changes.
- Ensure that updates to employee records do not orphan related data (Policies, Premiums).
- Verify that restricted access remains sound after any navigation or shell changes.
- Use `dotnet test` to confirm business logic remains intact.
