; ---------------------------------------------------------------------------
; Instalador de CosmereIdle
;
; Se compila con Inno Setup 6.3 o mas nuevo: https://jrsoftware.org/isdl.php
; Abrilo con el Inno Setup Compiler y dale Build > Compile (F9), o por consola:
;
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\CosmereIdle.iss
;
; Sale en Installer\Output\.
;
; Instala SIN pedir permisos de administrador (va a la carpeta del usuario).
; Para un juego fan sin firma digital eso importa: un instalador que abre el
; cartel de UAC espanta a la mitad de la gente.
; ---------------------------------------------------------------------------

#define AppName      "CosmereIdle"
#define AppPublisher "Comunidad CosmereAR"
#define AppUrl       "https://github.com/SolSerki/CosmereIdle"
#define BuildDir     "..\Build"

; Version.iss lo genera Tools > CosmereIdle > Buildear, para que el numero de
; version salga de Player Settings y no haya que mantenerlo en dos lados.
#ifexist "Version.iss"
  #include "Version.iss"
#else
  #define AppVersion "1.0"
  #define ExeName    "CosmereArgIdle.exe"
#endif

; Si no hay build, mejor fallar aca que repartir un instalador vacio.
#if !FileExists(AddBackslash(SourcePath) + BuildDir + "\" + ExeName)
  #error No encuentro el build. Corré Tools > CosmereIdle > Buildear primero.
#endif

[Setup]
; El AppId identifica la app entre versiones. NO cambiarlo nunca, o Windows
; pasa a tratar cada version como un programa distinto y se acumulan.
AppId={{8F3A1C74-6D29-4B15-9E0A-2C7B5D84F1A3}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=Output
OutputBaseFilename={#AppName}-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Aviso legal de contenido fan. Va antes de instalar, no escondido en un readme.
InfoBeforeFile=Disclaimer.txt

; El juego corre sin barra de tareas y sin alt-tab, asi que si ya esta abierto
; nadie lo puede cerrar a mano. Esto lo cierra solo antes de sobrescribir.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; \
    GroupDescription: "Accesos directos:"

Name: "startupicon"; Description: "Que arranque junto con Windows"; \
    GroupDescription: "Inicio:"

[Files]
; recursesubdirs + createallsubdirs: el .exe no sirve sin la carpeta _Data,
; UnityPlayer.dll y MonoBleedingEdge al lado.
Source: "{#BuildDir}\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs; \
    Excludes: "*_BurstDebugInformation_DoNotShip\*,*_BackUpThisFolder_ButDontShipItWithYourGame\*,*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "Abrir {#AppName} ahora"; \
    Flags: nowait postinstall skipifsilent

; La actualizacion automatica (GitHubUpdater) corre este instalador en modo
; silencioso despues de cerrar el juego. Sin esta linea el juego no vuelve a
; abrirse, y como no tiene barra de tareas nadie se daria cuenta de que se fue.
Filename: "{app}\{#ExeName}"; Flags: nowait; Check: WizardSilent
