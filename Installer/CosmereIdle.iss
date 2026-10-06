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
;
; "force" y no "yes": el juego no responde al pedido de cierre del Restart
; Manager, y con "yes" en modo silencioso Inno elige Abortar y deshace todo.
; Asi fallaba la actualizacion automatica. Antes de llegar a esto,
; InitializeSetup (abajo) espera a que el juego se cierre solo.
CloseApplications=force
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

[Code]
// La actualizacion automatica lanza este instalador y recien despues cierra el
// juego. El instalador arranca mas rapido de lo que el juego tarda en cerrarse,
// asi que lo encontraba abierto y la instalacion se abortaba. En modo
// silencioso se espera a que el juego (y su crash handler) terminen de salir.

const
  GameWaitMs = 20000;
  GameWaitStepMs = 250;

// Si hay un proceso del juego instalado corriendo. Se filtra por la carpeta
// para no esperar a un build de prueba abierto desde el proyecto, ni al crash
// handler de otro juego hecho en Unity (todos se llaman igual).
function GameIsRunning(): Boolean;
var
  Locator, Service, Processes, Process: Variant;
  ExePath: String;
  I: Integer;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Service.ExecQuery(
      'SELECT ExecutablePath FROM Win32_Process ' +
      'WHERE Name = ''{#ExeName}'' OR Name = ''UnityCrashHandler64.exe''');

    for I := 0 to Processes.Count - 1 do
    begin
      Process := Processes.ItemIndex(I);

      // De los procesos de otros usuarios WMI no da la ruta: viene Null.
      ExePath := '';
      if not VarIsNull(Process.ExecutablePath) then ExePath := Process.ExecutablePath;

      if Pos('\{#AppName}\', ExePath) > 0 then
      begin
        Result := True;
        Exit;
      end;
    end;
  except
    // Sin WMI no se puede saber: CloseApplications=force se encarga.
    Result := False;
  end;
end;

function InitializeSetup(): Boolean;
var
  Waited: Integer;
begin
  Result := True;
  if not WizardSilent() then Exit;

  Waited := 0;
  while GameIsRunning() and (Waited < GameWaitMs) do
  begin
    Sleep(GameWaitStepMs);
    Waited := Waited + GameWaitStepMs;
  end;

  if Waited >= GameWaitMs then
    Log('El juego sigue abierto despues de esperar; lo cierra el Restart Manager.')
  else
    Log(Format('Juego cerrado (espere %d ms).', [Waited]));
end;
