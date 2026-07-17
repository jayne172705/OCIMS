eSureHi Sulop Setup Simulation

Installer type:
Self-contained Windows x64 installer.

Prerequisites on the target PC:
- Windows 10 x64 or later
- No local database server required
- Internet is optional for daily use; Hostinger MySQL is used when available for sync

Local database:
- ims.db is created beside eSureHi.exe on first launch.
- The app reads/writes SQLite locally and syncs with Hostinger when online.

Default included cloud sync config:
Server=your-db-host
Port=3306
Database=your_database_name
User=your_database_user
Password=your_database_password

Included external database config files:
- eSureHiConfig.txt
- GgmsConfig.txt
- CrsConfig.txt

After install:
- Desktop shortcut: eSureHi
- Start Menu folder: eSureHi
- Installed folder: C:\Program Files\eSureHi

Upgrade behavior:
- Existing eSureHiConfig.txt, GgmsConfig.txt, and CrsConfig.txt are preserved.
- To replace saved connection settings with the included defaults, run:
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File Install-eSureHi.ps1 -ResetConfig

For the Sulop simulation, confirm the database server/name/user/password before login.
The installer does not install MySQL, configure ports, or create a Windows database service.
