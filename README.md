# eSureHi - Office Constituent Insurance Management System

A WPF desktop application for managing insurance policies, claims, premiums, benefits, documents, and cedula records for employees of the Municipality of Sulop.

## Features

- Employee management with photo support
- Insurance policy creation and assignment
- Claims processing workflow
- Premium schedule generation and payment tracking
- Benefits management
- Document management and department transactions
- Cedula management
- Reports with PDF and Excel export
- In-app notifications
- JSON-based backup and restore
- Role-based admin and employee access
- Configurable eSureHi, GGMS, and CRS database connections

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 8 WPF |
| UI | MaterialDesignInXaml |
| Local Database | SQLite (`ims.db`) |
| Cloud Sync | MySQL 8.0 / Hostinger |
| ORM | EF Core 8 with SQLite and Pomelo |
| Authentication | BCrypt.Net-Next |
| PDF Export | QuestPDF |
| Excel Export | ClosedXML |

## Requirements

- Windows 10 x64 or later
- No local database server required
- Hostinger MySQL connection for cloud sync when online
- For source builds: .NET 8 SDK
- For the installer: no separate .NET runtime is required because the installer is self-contained

## Setup

### Option 1 - Installer

Run `release\setup.exe`. The installer creates desktop and Start Menu shortcuts and installs the app to `C:\Program Files\eSureHi`.

Existing connection config files are preserved during upgrades. To overwrite saved settings with the included defaults, run `Install-eSureHi.ps1 -ResetConfig` from the staged installer folder.

### Option 2 - Build From Source

1. Open `OCIMS\eSureHi.sln` in Visual Studio 2022 or use the .NET CLI.
2. Restore NuGet packages.
3. Create or update the cloud sync config files in the `OCIMS` project folder:

```text
eSureHiConfig.txt
GgmsConfig.txt
CrsConfig.txt
```

4. Build and run the app. The local SQLite file `ims.db` is created automatically beside the executable.

## Default Login

| Username | Password | Role |
|---|---|---|
| `admin` | `admin123` | HR Admin |
| `emp1` | `emp123` | Employee |

Change the default password after first login.

## Database Configuration

The app always reads and writes local SQLite first. The local file is named `ims.db` and is stored beside `eSureHi.exe`. Hostinger MySQL is used only for automatic/manual sync and authoritative cross-system operations.

- IMS Cloud: online eSureHi Hostinger database for sync.
- GGMS: budget allocation and `consolidated_transactions`.
- CRS: master list / validated beneficiary source for Beneficiaries.

Main cloud sync settings are stored in `eSureHiConfig.txt` in the application directory. GGMS and CRS connections are stored in `GgmsConfig.txt` and `CrsConfig.txt`. These can also be edited from the login gear icon or from Settings after login.

```text
Server=your-db-host
Port=3306
Database=your_database_name
User=your_database_user
Password=your_database_password
```

## Installer Build

To refresh the installer from the current source:

```powershell
.\installer\Build-Installer.ps1
```

This publishes a self-contained Windows x64 build, rebuilds `release\eSureHi-installer-staging\payload.zip`, refreshes the IExpress `.sed` file, and writes `release\setup.exe`.

The installer does not install MySQL, open ports, or create a Windows database service. It copies the app files; the app creates `ims.db` on first launch.

## Backup System

Backups are JSON-based and do not require `mysqldump`.

| Type | Description |
|---|---|
| Full | Complete database snapshot and required baseline |
| Differential | Changes since the last full backup |
| Incremental | Changes since the last backup of any type |

Backups are stored in the configured backup folder, defaulting to `Backups` in the application directory.
