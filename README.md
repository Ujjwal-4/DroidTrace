# DroidTrace

**Android Digital Forensic Acquisition & Artifact Analysis**

DroidTrace is an open-source Windows desktop application for authorized
Android forensic acquisition using **Android Debug Bridge (ADB)**. It
provides a case-oriented workflow for collecting device and application
artifacts, preserving acquisition output, calculating SHA-256 integrity
hashes, building a basic forensic timeline, inspecting artifacts, and
exporting analysis results.

> **For authorized forensic use only.** DroidTrace is designed to assist
> legitimate digital investigations, incident response, research, and
> laboratory work. It does not bypass Android security controls, device
> encryption, application sandboxing, or permission boundaries.

------------------------------------------------------------------------

## Features

### Acquisition

-   ADB device discovery and authorization-state detection
-   Case-based acquisition sessions
-   Device identification and system properties
-   CPU / memory information
-   Battery information
-   Installed package inventory
-   Contacts collection where the connected device permits provider
    access
-   SMS collection where the connected device permits provider access
-   Call-log collection where the connected device permits provider
    access
-   Raw acquisition output preserved as evidence files

### Evidence Integrity

-   SHA-256 hash for each acquired artifact
-   Evidence manifest containing acquisition metadata and artifact
    status
-   Manifest SHA-256 integrity digest
-   Acquisition timestamps
-   Explicit success/failure status for individual artifact collections
-   Evidence stored separately by case

### Analysis

-   Forensic timeline generation from supported SMS and call artifacts
-   Timeline filtering by artifact type
-   Keyword search across timeline events
-   Artifact preview and inspection
-   Device information inspector
-   ADB shell interface with predefined command options

### Reporting

-   JSON timeline export
-   CSV timeline export
-   Evidence manifest
-   Case-specific evidence directory

### User Interface

-   Dark forensic-workstation interface
-   Acquisition dashboard
-   Forensic Timeline explorer
-   Device Inspector
-   Audit Activity view
-   Configuration view
-   Responsive artifact tables and evidence preview

### Engineering

-   .NET 8 / Windows Forms
-   Service-oriented application structure
-   Optional PostgreSQL integration
-   Configuration kept outside source code
-   No database password hard-coded in the source
-   Automated tests with xUnit
-   GitHub Actions CI workflow
-   Crash logging for unexpected application failures

------------------------------------------------------------------------

## Architecture

``` text
                         ┌─────────────────────┐
                         │    Android Device   │
                         │   USB + USB Debug   │
                         └──────────┬──────────┘
                                    │
                                   ADB
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │      AdbService     │
                         │ Device discovery     │
                         │ Shell execution      │
                         │ Device properties    │
                         └──────────┬──────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │ Acquisition Service  │
                         │ Case/session control │
                         │ Artifact collection  │
                         └──────────┬──────────┘
                                    │
                  ┌─────────────────┼─────────────────┐
                  ▼                 ▼                 ▼
             Device Info        Contacts         Communications
             CPU/Memory           SMS              Call Logs
             Battery           Packages
                  │                 │                 │
                  └─────────────────┼─────────────────┘
                                    ▼
                         ┌─────────────────────┐
                         │ Evidence Repository  │
                         │ Raw artifact files   │
                         │ SHA-256 hashes       │
                         │ Evidence manifest    │
                         └──────────┬──────────┘
                                    │
                    ┌───────────────┼────────────────┐
                    ▼               ▼                ▼
                 Timeline       Dashboard         Reports
                 Analysis       Inspection       CSV / JSON
```

The project separates **acquisition** from **analysis** so that
collected evidence can be preserved before higher-level processing is
performed.

------------------------------------------------------------------------

## Project Structure

``` text
DroidTrace/
│
├── src/
│   └── DroidTrace/
│       ├── Models/
│       │   └── Models.cs
│       ├── Services/
│       │   └── AppServices.cs
│       ├── UI/
│       │   ├── MainForm.cs
│       │   └── Theme.cs
│       ├── Program.cs
│       ├── DroidTrace.csproj
│       └── app.manifest
│
├── tests/
│   └── DroidTrace.Tests/
│       ├── TimelineTests.cs
│       ├── MainFormTests.cs
│       └── DroidTrace.Tests.csproj
│
├── docs/
│   └── ARCHITECTURE.md
│
├── .github/
│   └── workflows/
│       └── build.yml
│
├── DroidTrace.sln
├── LICENSE
├── README.md
├── SECURITY.md
├── BUILD-VERIFICATION.md
├── publish.ps1
└── .gitignore
```

------------------------------------------------------------------------

## Requirements

### Operating System

-   Windows 10 or Windows 11
-   64-bit Windows is recommended

### Development

-   .NET 8 SDK
-   Visual Studio 2022 or another .NET-compatible IDE is optional
-   Internet access for restoring NuGet packages

### Android

-   Android device
-   USB cable
-   USB debugging enabled
-   Device unlocked during authorization
-   Authorized ADB connection

### Android Platform Tools

DroidTrace requires `adb.exe`.

Download the official Android SDK Platform Tools from Google:

https://developer.android.com/tools/releases/platform-tools

After extracting Platform Tools, either:

1.  Add the directory containing `adb.exe` to the Windows `PATH`, or
2.  Configure the full ADB path in DroidTrace Settings.

Verify the installation:

``` powershell
adb version
adb devices
```

A correctly authorized device should appear as:

``` text
List of devices attached
DEVICE_SERIAL    device
```

If the device appears as `unauthorized`, unlock the phone and accept the
**Allow USB debugging** prompt.

------------------------------------------------------------------------

## Building from Source

Clone the repository:

``` powershell
git clone https://github.com/Ujjwal-4/DroidTrace.git
cd DroidTrace
```

Restore dependencies:

``` powershell
dotnet restore DroidTrace.sln
```

Build the solution:

``` powershell
dotnet build DroidTrace.sln --configuration Release
```

Run the tests:

``` powershell
dotnet test DroidTrace.sln --configuration Release
```

Run DroidTrace:

``` powershell
dotnet run --project src/DroidTrace/DroidTrace.csproj
```

------------------------------------------------------------------------

## Running DroidTrace

### 1. Connect the Android device

Connect the authorized Android device using USB.

### 2. Verify ADB

``` powershell
adb devices
```

Make sure the device state is:

``` text
device
```

### 3. Start DroidTrace

Launch the application.

### 4. Select the device

Use **Refresh** if the device does not immediately appear in the device
selector.

### 5. Create or enter a case ID

Example:

``` text
CASE-20261002
```

Use your organization's approved case-identification convention for real
investigations.

### 6. Start acquisition

Select **Start Acquisition**.

DroidTrace creates a case-specific evidence directory and records the
status of each supported artifact collection.

------------------------------------------------------------------------

## Evidence Output

A typical acquisition produces a structure similar to:

``` text
Evidence/
└── CASE-20261002_20261002_003000/
    │
    ├── device_info.txt
    ├── cpu_memory.txt
    ├── battery.txt
    ├── packages.txt
    ├── contacts.txt
    ├── sms.txt
    ├── calls.txt
    │
    └── evidence-manifest.json
```

The exact artifact contents depend on:

-   Android version
-   Device manufacturer
-   Device configuration
-   ADB authorization
-   Available Android content providers
-   Permissions
-   Device security policy

------------------------------------------------------------------------

## Evidence Manifest

The manifest records the acquisition state of collected artifacts.

Conceptually:

``` json
{
  "caseId": "CASE-20261002",
  "deviceSerial": "DEVICE_SERIAL",
  "artifacts": [
    {
      "type": "SMS",
      "fileName": "sms.txt",
      "records": 120,
      "success": true,
      "sha256": "..."
    }
  ],
  "manifestSha256": "..."
}
```

The SHA-256 digest allows an examiner to detect changes to acquired
evidence after collection.

> Hashing provides integrity verification. It does not by itself
> establish the legal admissibility, authenticity, or completeness of
> evidence.

------------------------------------------------------------------------

## Forensic Timeline

DroidTrace can normalize supported communication artifacts into timeline
events.

Supported event types currently include:

``` text
SMS
CALL
```

The Timeline Explorer provides:

-   Chronological ordering
-   Keyword search
-   SMS filtering
-   Call filtering
-   Event source information
-   Event details

Example:

``` text
Timestamp                  Artifact    Source
----------------------------------------------------------------
2026-10-01 20:14:32        SMS         content://sms
2026-10-01 20:18:07        CALL        content://call_log/calls
```

------------------------------------------------------------------------

## Device Inspector

The Device Inspector provides information obtained through ADB,
including:

-   Manufacturer
-   Model
-   Android version
-   Security patch level
-   Build ID
-   CPU ABI
-   Battery level
-   Device serial

The information is collected through Android system properties and ADB
commands available to the authorized device session.

------------------------------------------------------------------------

## ADB Shell

DroidTrace provides a controlled ADB shell interface for authorized
examination.

Commands are executed against the selected device through ADB.

Use shell access carefully because commands can potentially modify
device state.

For forensic acquisition, prefer read-only commands whenever possible
and document any command that changes the target environment.

------------------------------------------------------------------------

## Configuration

DroidTrace creates:

``` text
droidtrace.settings.json
```

The configuration can contain:

-   ADB executable path
-   Evidence root
-   Optional PostgreSQL connection configuration

Example:

``` json
{
  "AdbPath": "adb",
  "PostgreSqlConnection": "",
  "EvidenceRoot": "Evidence"
}
```

**Never commit credentials or sensitive configuration to Git.**

The repository `.gitignore` excludes:

``` text
droidtrace.settings.json
Evidence/
bin/
obj/
.vs/
```

------------------------------------------------------------------------

## PostgreSQL

PostgreSQL is **optional**.

DroidTrace's core acquisition workflow does not require a PostgreSQL
server.

This allows the application to be used as a standalone acquisition and
analysis utility without requiring database infrastructure.

A future version can expand the persistence layer for:

-   Case management
-   Artifact indexing
-   Large-scale investigations
-   Multi-case search
-   Centralized evidence metadata

------------------------------------------------------------------------

## Testing

The project includes automated tests using xUnit.

Current tests cover areas including:

-   SMS timeline parsing
-   Timeline keyword filtering
-   Timeline artifact-type filtering
-   MainForm initialization
-   MainForm view switching

Run:

``` powershell
dotnet test DroidTrace.sln --configuration Release
```

------------------------------------------------------------------------

## Continuous Integration

GitHub Actions is included under:

``` text
.github/workflows/build.yml
```

The CI workflow restores dependencies, builds the solution, and executes
the test suite on Windows.

This helps catch compilation and regression issues before changes are
merged.

------------------------------------------------------------------------

## Security and Forensic Considerations

DroidTrace is intentionally designed around the principle that **failed
acquisition must not be represented as successful acquisition**.

If Android or ADB denies access to an artifact, the application should
preserve the failure state rather than silently treating the collection
as complete.

Important considerations:

-   Do not modify evidence unnecessarily.
-   Keep the original acquisition directory intact.
-   Preserve the evidence manifest.
-   Record relevant acquisition dates and examiner information
    separately where required.
-   Use write-protected or controlled forensic storage for formal
    investigations.
-   Maintain your organization's chain-of-custody documentation.
-   Verify hashes after copying or transferring evidence.
-   Avoid using destructive ADB commands during examination.
-   Do not expose acquired evidence in public repositories.

### Android limitations

ADB does **not** automatically provide unrestricted access to all
Android data.

Depending on the device, Android version, OEM configuration,
permissions, and application sandboxing:

-   Some contacts may be inaccessible.
-   SMS access may be restricted.
-   Call-log access may be restricted.
-   Application-private data may be inaccessible.
-   Encrypted data may not be available.
-   Root-only artifacts are not automatically acquired.
-   Locked-device data may be unavailable.

DroidTrace should therefore be treated as an **ADB-based logical
acquisition and artifact analysis tool**, not as a replacement for full
forensic extraction platforms.

------------------------------------------------------------------------

## Privacy

Acquired device data can contain highly sensitive information such as:

-   Personal messages
-   Telephone numbers
-   Contacts
-   Application information
-   Device identifiers
-   System configuration

Do not upload real evidence to GitHub, issue trackers, public
repositories, or other third-party services unless explicitly
authorized.

For development and demonstrations, use a test device or sanitized
dataset.

------------------------------------------------------------------------

## Roadmap

Potential future development areas include:

-   [ ] Advanced Android artifact parsers
-   [ ] Application-specific artifact modules
-   [ ] Media metadata extraction
-   [ ] File-system metadata analysis
-   [ ] SQLite database parsing
-   [ ] APK metadata and signing analysis
-   [ ] Improved deleted-artifact analysis where technically available
-   [ ] Extended timeline correlation
-   [ ] Pluggable artifact parser architecture
-   [ ] Case management
-   [ ] Examiner and evidence metadata
-   [ ] Enhanced HTML/PDF reporting
-   [ ] Evidence verification workflow
-   [ ] Multi-device acquisition management
-   [ ] Optional PostgreSQL-backed case database
-   [ ] Automated regression test expansion
-   [ ] Cross-platform CLI acquisition component

------------------------------------------------------------------------

## Contributing

Contributions are welcome.

### Suggested workflow

1.  Fork the repository.
2.  Create a feature branch.

``` powershell
git checkout -b feature/my-feature
```

3.  Make the change.
4.  Run formatting and tests.
5.  Build the solution.

``` powershell
dotnet build DroidTrace.sln --configuration Release
dotnet test DroidTrace.sln --configuration Release
```

6.  Commit the change.

``` powershell
git commit -m "Add: descriptive change"
```

7.  Push the branch and open a Pull Request.

For forensic functionality, include:

-   The artifact source
-   Android-version assumptions
-   Acquisition limitations
-   Expected output
-   Tests covering the change

------------------------------------------------------------------------

## Responsible Use

DroidTrace is intended for:

-   Digital forensics education
-   Authorized forensic examinations
-   Incident response
-   Security research
-   Android artifact research
-   Controlled laboratory environments

The user of DroidTrace is responsible for ensuring that acquisition and
analysis are legally authorized.

**Only examine devices and data for which you have appropriate
authorization.**

------------------------------------------------------------------------

## License

DroidTrace is released under the **MIT License**.

See [`LICENSE`](LICENSE) for the complete license text.

------------------------------------------------------------------------

## Project Status

DroidTrace is an actively evolving forensic research and engineering
project.

The current release focuses on:

``` text
ADB acquisition
      ↓
Artifact preservation
      ↓
SHA-256 integrity
      ↓
Timeline analysis
      ↓
Artifact inspection
      ↓
CSV / JSON reporting
```

It should be validated against the specific Android devices, OS
versions, permissions, and forensic procedures relevant to any real
investigation.

------------------------------------------------------------------------

## Disclaimer

DroidTrace is provided for authorized security and forensic purposes.

The software is provided **"as is"**, without warranty of any kind. The
authors and contributors are not responsible for data loss, device
changes, privacy violations, unauthorized access, or misuse resulting
from the software.

For formal investigations, follow the applicable laws, organizational
procedures, evidence-handling requirements, and chain-of-custody
standards.
