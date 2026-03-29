# OnPass Desktop

OnPass Desktop is the Windows desktop application for the OnPass password manager project. It stores password vault data locally, encrypts user data with a key derived from the master password, supports TOTP authenticator entries, and exposes a local authenticated API that the OnPass browser extension can use for autofill.

The application is built as a WPF desktop client on `.NET 8` and follows a refactored project structure with separate `Domain`, `Infrastructure`, and `Presentation` folders.

## Features

- User registration and login with password-based authentication
- Encrypted local password vault
- Password history and password restore support
- Built-in password generator with configurable character rules
- Built-in TOTP authenticator manager
- Windows Hello biometric login support
- Auto-lock after inactivity
- Minimize-to-tray support with tray menu actions
- Start-with-Windows option
- Import and export for user data and password data
- Local desktop API for the OnPass browser extension
- Clipboard auto-clear timer for copied extension keys, generated passwords, and TOTP codes

## Architecture

The desktop app is organized into three main layers:

- `Domain`
  Contains the core data models such as `PasswordItem` and password history records.
- `Infrastructure`
  Contains encryption, key derivation, local file storage, and the local web server used by the extension.
- `Presentation`
  Contains WPF windows, controls, dialogs, navigation, and user-facing workflows.

This split keeps UI code separate from storage, security, and local integration logic without changing the existing application behavior.

## Security Model

- User credentials are stored under `%AppData%\\OnPass`
- Master-password-derived encryption keys are created with `PBKDF2-SHA256`
- Vault files and authenticator files are encrypted with `AES-CBC`
- A per-session access token is generated for extension access
- The browser extension connects only to the local desktop API on `localhost`
- Biometric login uses Windows Hello and stores the protected secret with Windows DPAPI
- Copied sensitive values are cleared from the clipboard after 30 seconds if unchanged

## Local Extension Integration

When a user logs in successfully, the desktop app starts a local `HttpListener` service through `LocalWebServer`.

Current integration behavior:

- primary endpoint: `http://localhost:9876/`
- fallback endpoint: `http://localhost:9877/`
- `GET /validate` verifies the extension bearer token
- `GET /passwords` returns decrypted password entries for the logged-in user

The access token is shown on the home dashboard and can be copied into the browser extension so the extension can authenticate against the local API.

## Prerequisites

- Windows 10 or later
- `.NET 8 SDK`
- Visual Studio 2022 or MSBuild from Visual Studio
- Windows Hello configured if biometric login will be used

## Installation

1. Clone the repository.
2. Open the project folder in Visual Studio or open `OnPass.csproj`.
3. Restore NuGet packages.
4. Build and run the application with Visual Studio or full MSBuild.

Example build command:

```powershell
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" OnPass.csproj /t:Build /p:Configuration=Debug
```

## Setup

1. Launch the desktop app.
2. Register a new account with a master password.
3. Log in to load the encrypted vault.
4. Open the dashboard home page to view the extension access key.
5. Copy that key into the OnPass browser extension if you want browser autofill support.
6. Configure optional settings such as auto-lock, start with Windows, minimize to tray, and biometric login.

## Usage

Typical user flow:

1. Register or log in with the master password.
2. Open `Password Manager` to add, edit, search, or delete vault entries.
3. Use `Generate Password` to create strong passwords and copy them safely.
4. Open `Two-Factor Authentication` to add TOTP secrets and generate 6-digit codes.
5. Open `Settings` to change the master password, enable or disable biometric login, configure auto-lock, and manage import or export operations.
6. Use the dashboard home page to copy the extension access key for browser integration.

## Project Structure

```text
OnPass-Desktop/
|-- App.xaml
|-- App.xaml.cs
|-- OnPass.csproj
|-- Domain/
|   `-- PasswordItem.cs
|-- Infrastructure/
|   |-- Security/
|   |   |-- AesEncryption.cs
|   |   `-- KeyDerivation.cs
|   |-- Storage/
|   |   `-- PasswordStorage.cs
|   `-- Web/
|       `-- LocalWebServer.cs
|-- Presentation/
|   |-- Controls/
|   |-- Dialogs/
|   `-- Windows/
|-- Resources/
|   `-- Images/
|-- Properties/
`-- README.md
```

## Key UI Modules

- `MainWindow`
  Application shell, tray integration, inactivity monitoring, and top-level navigation.
- `LoginControl`
  Master-password login, session setup, and import entry point.
- `RegisterControl`
  Account registration and initial storage bootstrap.
- `DashboardControl`
  Main authenticated navigation shell.
- `HomeDashboardControl`
  Security overview, extension key display, and extension token copy flow.
- `PasswordVaultControl`
  Password CRUD, search, and password history access.
- `GeneratePasswordControl`
  Secure password generation and clipboard copy support.
- `AuthenticatorControl`
  TOTP secret management and live code generation.
- `Settings`
  App settings, biometric setup, master password changes, and import/export workflows.

## Data Storage

OnPass stores its user files in:

`%AppData%\\OnPass`

Common files include:

- `credentials.txt`
- `passwords_<username>.dat`
- `<username>_settings.ini`
- `<username>_authenticators.enc`
- `biometric_config.txt`

## Supported Platform

- Windows desktop
- WPF on `.NET 8`

The browser extension integration is intended for the companion OnPass browser extension and depends on the desktop app being logged in and running locally.

## Known Limitations

- The local extension API is available only while the desktop app is running and the user is logged in.
- The local web server currently uses localhost HTTP, not HTTPS, because it is intended for same-machine communication only.
- The project currently relies heavily on WPF code-behind rather than a full MVVM separation.
- Automated tests are not yet included in the repository.

## Future Improvements

- Add automated unit tests for encryption, storage, and extension API behavior
- Extract more presentation logic into dedicated services or view models
- Centralize file-path and settings management
- Add packaging and installer support
- Add richer documentation and screenshots for the desktop workflows
