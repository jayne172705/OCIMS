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

Installer database step:
- The installer shows prefilled, editable textboxes for IMS_DB / GGMS_DB / CRS_DB.
- Network (default, office LAN): 192.168.0.47 / root / network@2026
  IMS_DB=ims_db, GGMS_DB=ggms_db, CRS_DB=crs_db.
- Online (Hostinger cloud, IMS_DB only): 194.59.164.58 / u621755393_ims / u621755393_ims_user / Ims@2026.
  GGMS/CRS stay on the LAN preset when Online is picked.
- Switching Network/Online in the installer re-fills the textboxes (still editable),
  exactly matching the presets inside the app (login gear icon / Settings).
- Silent install examples:
  powershell -ExecutionPolicy Bypass -File Install-eSureHi.ps1 -Silent -Mode Network -LaunchAfterInstall
  powershell -ExecutionPolicy Bypass -File Install-eSureHi.ps1 -Silent -Mode Online -LaunchAfterInstall

Default included configs (prefilled Network, in sync with app):
[eSureHiConfig.txt]
Server=192.168.0.47
Port=3306
Database=ims_db
User=root
Password=network@2026
[GgmsConfig.txt]
Server=192.168.0.47
Port=3306
Database=ggms_db
User=root
Password=network@2026
[CrsConfig.txt]
Server=192.168.0.47
Port=3306
Database=crs_db
User=root
Password=network@2026

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
