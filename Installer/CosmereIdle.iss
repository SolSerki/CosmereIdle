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
;
; Modo parche: el menu Publicar version lo compila una segunda vez con
; /DPatch=1 /DPatchDir=<carpeta> /DPatchCheck=<archivo>. Sale un instalador
; con solo los archivos que cambiaron (un par de MB), que antes de tocar nada
; verifica que el resto de la instalacion sea exactamente la esperada. A mano
; no hace falta compilarlo nunca.
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
#ifdef Patch
OutputBaseFilename={#AppName}-{#AppVersion}-patch
#else
OutputBaseFilename={#AppName}-{#AppVersion}-setup
#endif
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Aviso legal de contenido fan. Va antes de instalar, no escondido en un readme.
InfoBeforeFile=Disclaimer.txt

; Logo oficial (COSMEREARG_Logo2): en el instalador, en "Aplicaciones
; instaladas" de Windows y en los accesos directos (ver [Icons]).
; CosmereIdle.ico se genera desde Assets/Art/COSMEREARG_Logo2.png con todos
; los tamaños que usa Windows; si cambia el logo, hay que regenerarlo.
SetupIconFile=CosmereIdle.ico
UninstallDisplayIcon={app}\CosmereIdle.ico

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
#ifdef Patch
; Solo lo que cambio. Lo elige el menu Publicar version, que tambien genera
; la verificacion de todo lo demas (ver PatchCanApply mas abajo).
Source: "{#PatchDir}\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs
#else
; recursesubdirs + createallsubdirs: el .exe no sirve sin la carpeta _Data,
; UnityPlayer.dll y MonoBleedingEdge al lado.
;
; Si se cambian estos Excludes, cambiar tambien ShippedFiles en
; CosmereIdlePatch.cs: la lista de archivos del parche tiene que coincidir.
Source: "{#BuildDir}\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs; \
    Excludes: "*_BurstDebugInformation_DoNotShip\*,*_BackUpThisFolder_ButDontShipItWithYourGame\*,*.pdb"
#endif

#ifdef Patch
[InstallDelete]
; Archivos de versiones anteriores que ya no son parte del juego (paquetes que
; se sacaron, por ejemplo). Lo genera Publicar version.
#include PatchDelete

[Files]
#endif
; El icono va instalado al lado del juego: lo usan los accesos directos y el
; desinstalador de Windows.
Source: "CosmereIdle.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Los accesos directos llevan el logo explicito y no el del .exe: asi se ven
; bien aunque el build salga con otro icono.
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\CosmereIdle.ico"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\CosmereIdle.ico"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\CosmereIdle.ico"; Tasks: startupicon

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

procedure WaitForGameToClose();
var
  Waited: Integer;
begin
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

#ifdef Patch
// Un parche solo trae lo que cambio: si el resto de la instalacion no es
// exactamente la version para la que se armo, instalarlo dejaria una mezcla
// rota de dos versiones. El juego ya lo revisa antes de elegir el parche;
// esto es la segunda red, por si alguien lo corre a mano o algo cambio entre
// medio.

// Carpeta donde esta instalado, segun el registro de Windows. Mismo GUID que
// AppId. Vacio si no esta instalado.
function InstalledDir(): String;
var
  Key: String;
begin
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3A1C74-6D29-4B15-9E0A-2C7B5D84F1A3}_is1';
  Result := '';
  if not RegQueryStringValue(HKCU, Key, 'InstallLocation', Result) then
    RegQueryStringValue(HKLM, Key, 'InstallLocation', Result);
  Result := RemoveBackslashUnlessRoot(Result);
end;

function FileMatches(Dir, RelPath, Sha256: String): Boolean;
var
  Actual: String;
begin
  try
    Actual := GetSHA256OfFile(Dir + '\' + RelPath);
  except
    Actual := '';
  end;

  Result := CompareText(Actual, Sha256) = 0;
  if not Result then Log('Parche: ' + RelPath + ' no es el esperado.');
end;

// Define PatchBaseMatches(Dir): una llamada a FileMatches por cada archivo
// que el parche NO trae. Lo genera Publicar version.
#include PatchCheck

function PatchCanApply(): Boolean;
var
  Dir: String;
  Code: Integer;
begin
  Dir := InstalledDir();
  Result := (Dir <> '') and PatchBaseMatches(Dir);
  if Result then
  begin
    Log('Parche: la instalacion coincide, sigo.');
    Exit;
  end;

  Log('Parche: no sirve para esta instalacion. Hace falta el instalador completo.');

  if WizardSilent() then
  begin
    // Lo lanzo la actualizacion automatica, que ya cerro el juego. Sin esto
    // el juego desapareceria sin aviso; al abrirse, ve que el parche no
    // sirvio y baja el instalador completo.
    if (Dir <> '') and Exec(Dir + '\{#ExeName}', '', Dir, SW_SHOWNORMAL, ewNoWait, Code) then
      Log('Parche: juego reabierto.')
    else
      Log('Parche: no pude reabrir el juego.');
  end
  else
    MsgBox('Esta es una actualización parcial y no sirve para la versión que tenés instalada.' + #13#10#13#10 +
           'Bajá el instalador completo ({#AppName}-{#AppVersion}-setup.exe) de la misma página.',
           mbError, MB_OK);
end;
#endif

function InitializeSetup(): Boolean;
begin
  Result := True;
  if WizardSilent() then WaitForGameToClose();

#ifdef Patch
  Result := PatchCanApply();
#endif
end;
