; Instalador de AirMe (Inno Setup 6). Se compila en la nube (build.yml):
;   iscc /DAppVersion=0.3.0 /DSourceDir=dist\AirMe /DOutputDir=dist installer\AirMe.iss
; Instala en Archivos de programa, crea la regla del Firewall (redes privadas) y los accesos directos.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\AirMe"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#define AppName "AirMe"
#define AppExe "AirMe.exe"
#define EngineExe "engine\bin\uxplay.exe"

[Setup]
AppId={{2C0A3AE5-1778-4760-B921-97A7AAB0031A}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
AppPublisherURL=https://github.com/rianmo80/airme
AppSupportURL=https://github.com/rianmo80/airme
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=AirMe-Setup-{#AppVersion}
SetupIconFile=..\app\AirMe\Assets\airme.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ShowLanguageDialog=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; "Iniciar con Windows" lo gestiona la app; al desinstalar se quita
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "AirMe"; Flags: uninsdeletevalue

[Run]
; Regla del Firewall para el motor (solo redes privadas). Se borra antes por si ya existía.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""AirMe"" program=""{app}\{#EngineExe}"""; Flags: runhidden waituntilterminated
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""AirMe"" dir=in action=allow program=""{app}\{#EngineExe}"" enable=yes profile=private"; StatusMsg: "Configurando el Firewall de Windows..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /T /IM {#AppExe}"; Flags: runhidden waituntilterminated; RunOnceId: "CerrarAirMe"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""AirMe"" program=""{app}\{#EngineExe}"""; Flags: runhidden waituntilterminated; RunOnceId: "QuitarFirewall"

[Code]
// Al actualizar: cerrar AirMe (y su motor) para poder reemplazar los archivos
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := '';
end;
