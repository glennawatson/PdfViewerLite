; Inno Setup script for PdfViewerLite on Windows.
; Build with: iscc /DAppVersion=1.2.3 /DArch=x64 /DSourceDir=..\..\artifacts\win-x64 /DOutputDir=..\..\artifacts packaging\windows\PdfViewerLite.iss
; Installs for the current user by default (no administrator prompt); "Install for all users" is offered.
; PdfViewerLite is registered as a PDF handler so it appears in Open with and Default apps; it does not take over .pdf.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\win-" + Arch
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif

[Setup]
AppId={{6E7C2A41-6E7A-4E5B-9B3E-7C4F1C2D9A10}
AppName=PdfViewerLite
AppVersion={#AppVersion}
AppPublisher=Glenn Watson
AppPublisherURL=https://github.com/glennawatson/PdfViewerLite
DefaultDirName={autopf}\PdfViewerLite
DefaultGroupName=PdfViewerLite
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed={#Arch}compatible
ArchitecturesInstallIn64BitMode={#Arch}compatible
OutputDir={#OutputDir}
OutputBaseFilename=pdfviewerlite-{#AppVersion}-win-{#Arch}-setup
SetupIconFile=PdfViewerLite.ico
UninstallDisplayIcon={app}\pdfviewerlite.exe
LicenseFile=..\..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\PdfViewerLite"; Filename: "{app}\pdfviewerlite.exe"
Name: "{autodesktop}\PdfViewerLite"; Filename: "{app}\pdfviewerlite.exe"; Tasks: desktopicon

[Registry]
; A ProgID for PDF documents, offered in Open with.
Root: HKA; Subkey: "Software\Classes\PdfViewerLite.pdf"; ValueType: string; ValueData: "PDF Document"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PdfViewerLite.pdf\DefaultIcon"; ValueType: string; ValueData: """{app}\pdfviewerlite.exe"",0"
Root: HKA; Subkey: "Software\Classes\PdfViewerLite.pdf\shell\open\command"; ValueType: string; ValueData: """{app}\pdfviewerlite.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "PdfViewerLite.pdf"; ValueData: ""; Flags: uninsdeletevalue
; Registered application capabilities, so PdfViewerLite is listed in Settings > Default apps.
Root: HKA; Subkey: "Software\PdfViewerLite\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "PdfViewerLite"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\PdfViewerLite\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "A calm, fast, tabbed PDF viewer"
Root: HKA; Subkey: "Software\PdfViewerLite\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pdf"; ValueData: "PdfViewerLite.pdf"
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "PdfViewerLite"; ValueData: "Software\PdfViewerLite\Capabilities"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\Applications\pdfviewerlite.exe\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""; Flags: uninsdeletekey

[Run]
Filename: "{app}\pdfviewerlite.exe"; Description: "{cm:LaunchProgram,PdfViewerLite}"; Flags: nowait postinstall skipifsilent
