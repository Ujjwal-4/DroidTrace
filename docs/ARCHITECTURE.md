# DroidTrace Architecture

```text
ADB Device
   |
   v
AdbService
   |
   v
AcquisitionService -----> Evidence files + SHA-256 manifest
   |
   v
Artifact/Timeline services
   |
   +----> Dashboard
   +----> CSV/JSON reports
   +----> Optional PostgreSQL integration
```

The acquisition layer never silently converts failed ADB commands into successful evidence. Raw command output is preserved, hashes are calculated after writing, and the manifest records collection status.
