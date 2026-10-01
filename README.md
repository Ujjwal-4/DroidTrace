# DroidTrace

**Android Digital Forensic Acquisition & Artifact Analysis**

DroidTrace is a Windows/.NET 8 desktop forensic acquisition utility built around Android Debug Bridge (ADB). It refactors the original ADB Data Extractor concept into a case-oriented workflow with evidence manifests, SHA-256 integrity hashes, normalized artifact handling, timeline extraction, and export.

## Highlights

- ADB device discovery and authorization state
- Case-based acquisition sessions
- Device properties, CPU/memory, battery and installed-package collection
- Contacts, SMS and call-log provider queries where the connected ADB context permits access
- Raw artifact preservation
- SHA-256 hash per acquired artifact and a manifest-content SHA-256 digest
- Timestamped SMS/call timeline generation
- CSV/JSON timeline export
- PostgreSQL connectivity kept optional rather than required for acquisition
- Configuration stored outside source code; no database password is hardcoded
- Single dashboard-style WinForms UI

## Requirements

- Windows 10/11
- .NET 8 SDK or the published self-contained build
- Android SDK Platform Tools (`adb`) on PATH, or configure its full path in Settings
- Android device with USB debugging enabled and the examiner authorized on the device
- PostgreSQL is optional and currently used as an integration point rather than a requirement for acquisition

## Run

```powershell
dotnet restore DroidTrace.sln
dotnet build DroidTrace.sln --configuration Release
dotnet test DroidTrace.sln --configuration Release
dotnet run --project src/DroidTrace/DroidTrace.csproj
```

On first launch, DroidTrace creates `droidtrace.settings.json` next to the application. Evidence is written under `Evidence/<CASE>_<timestamp>/`.

## Evidence structure

```text
Evidence/
└── CASE-20261001_192400/
    ├── device_info.txt
    ├── cpu_memory.txt
    ├── battery.txt
    ├── packages.txt
    ├── contacts.txt
    ├── sms.txt
    ├── calls.txt
    └── evidence-manifest.json
```

## Forensic note

ADB access is constrained by the Android device, OS version, authorization state and application/provider permissions. DroidTrace does **not** bypass Android security controls or claim that a provider query succeeded merely because the command was issued. Failed/denied collections are recorded as unsuccessful artifacts.

Use only on devices and data for which you have lawful authorization. Preserve original evidence and follow your organization's acquisition and chain-of-custody procedures.

## Project layout

```text
DroidTrace/
├── src/DroidTrace/
│   ├── Models/
│   ├── Services/
│   └── UI/
├── tests/DroidTrace.Tests/
├── docs/
├── .github/workflows/
└── DroidTrace.sln
```

## Based on

The project was refactored from the public `NotRealShanks/adb-data-extractor` repository. The upstream application is a .NET 8 Windows Forms ADB/PostgreSQL extractor for contacts, messages, calls, CPU and device information. This version changes the architecture, naming, UI workflow, evidence handling and security configuration rather than preserving the original form-centric implementation.


## v1.2 UI refinement
- Responsive sidebar layout that keeps branding, navigation, and authorization notice separated.
- Responsive acquisition toolbar using a table layout instead of fixed pixel positions.
- Artifact table now uses fill-weighted columns and horizontal scrolling when required.
- Evidence path is separated from the artifact title and ellipsized safely.
- Dashboard cards and activity log resize cleanly with the main window.
- Application opens maximized on supported Windows displays.
