# Convenciones del proyecto

Unity **6000.3.22f1** · **Built-in** Render Pipeline · Color space **Gamma** · **Direct3D11**.

El objetivo de este archivo es que los tres podamos trabajar a la vez sin
pisarnos, y que nadie vuelva a perder un día con los mismos tres bugs.

---

## Dos reglas que rompen el build si no se respetan

**1. Nada de `UnityEngine.Input`.** El proyecto tiene `activeInputHandler = Input
System package`, así que la API vieja tira `InvalidOperationException` en cada
frame y el script deja de funcionar entero. Todo el input va por
`UnityEngine.InputSystem`:

```csharp
using UnityEngine.InputSystem;

Mouse mouse = Mouse.current;
if (mouse == null) return;
Vector2 pos = mouse.position.ReadValue();
bool click = mouse.leftButton.wasPressedThisFrame;
```

**2. Todo lo clickeable necesita un `Collider2D`.** La ventana deja pasar los
clicks al escritorio salvo cuando el cursor está sobre un collider. Sin
collider tu objeto se dibuja pero el click se va a la ventana de atrás, y no
hay ningún error que te avise.

---

## Setup: cada uno corre esto una vez después de clonar

```bash
git lfs install
```

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p --force --fallback none %O %A %B %A'
git config merge.unityyamlmerge.recursive binary
```

El segundo bloque registra el merge de Unity para escenas y prefabs. El
`.gitattributes` ya lo declara, pero sin este config **no hace nada** y un
conflicto en un `.unity` o un `.prefab` es basura ilegible. Va en la config
local, así que no se hereda al clonar: lo corre cada uno en su máquina.

---

## Estructura

```
Assets/
  Art/                    iconos y arte que no es de un personaje          [Kato]
  Characters/
    Pets/                 los spritesheets .png                            [Kato]
    Animations/           MascotaController.controller (base)
      <Personaje>/        los 4 .anim de ese personaje                     [Joch]
      <X>Controller.overrideController                                     [Joch]
  Prefabs/                el prefab base y sus variantes
  Scenes/                 Main.unity
  Scripts/
    Pet/                  comportamientos, movimiento, estado            [Serka]
    Desktop/              ventana, transparencia, barra de tareas        [Serka]
    Speech/               frases y globo de diálogo                       [Joch]
    App/                  bandeja del sistema, settings, preferencias     [Joch]
    Update/               actualización automática desde GitHub
  Settings/               InputSystem_Actions y afines
```

**Trabajá dentro de tu carpeta.** Si necesitás tocar la de otro, avisá en
Discord antes. No es burocracia: los conflictos de git se resuelven por
archivo, así que mientras cada uno escriba en archivos distintos no hay
conflicto posible.

---

## Sprites

Kato exporta desde Aseprite en 1x / 2x / 4x; **importamos el 1x**. El 2x y el
4x no se usan: el escalado entero lo hace el motor.

| Parámetro | Valor | Por qué |
|---|---|---|
| Sheet | 768 × 24, una fila de 32 frames | Joch es la excepción: 768 × 32 |
| Celda | 24 × 24 | **Grid By Cell Size**, nunca slicing automático |
| Pivot | **Bottom Center** | Los pies apoyan a altura fija; si no, el personaje vibra al animar |
| Pixels Per Unit | 100 | |
| Filter | Point, sin compresión | Pixel art borroso mata el proyecto |

El **slicing automático es el error a evitar**: le da a cada frame un rect
ajustado a sus píxeles visibles, así que el alto cambia de frame en frame y el
personaje tiembla verticalmente. Siempre grid.

Si hay que re-slicear, hacelo por el **Sprite Editor data provider**, no
rehaciendo el slice a mano: preserva el `spriteID` de cada sprite y las
animaciones no se rompen.

### Animaciones

Un `.anim` por comportamiento, 12 fps, looping. Hoy son cuatro por personaje:
`idle`, `walk`, `read`, y el freno que se usa al terminar de caminar.

> **Deuda conocida:** ese cuarto clip tiene seis grafías distintas entre
> personajes (`idle-slide`, `side-idle`, `idle-side`, `Idle Side`...). No rompe
> nada porque los override controllers mapean por slot, pero conviene
> unificarlo a `idle-slide` cuando alguien pase por ahí.

---

## Prefabs de personaje

`Joch_Spritesheet_0.prefab` es el **prefab base** y los otros nueve son
**variantes** suyas. Cambiás el comportamiento en el base y lo heredan los diez.

Cada variante overridea tres cosas y nada más:

- `m_Name` — el nombre del personaje
- `m_Sprite` — su propio spritesheet
- `m_Controller` — su propio `.overrideController`

Si una variante no overridea el controller, **anima con los clips de Joch** y no
te vas a dar cuenta mirando la escena. Pasó con FranFernet y Juanmant.

El collider de cada variante se ajusta a **su** sprite (Joch mide 32 de alto,
el resto 24). Con pivot abajo, el offset va en `(0, alto/2)`.

---

## La ventana transparente

Vive entera en `Scripts/Desktop/`. `DesktopWindow` es el único lugar que toca
la ventana del SO, y `Win32.cs` el único con P/Invoke.

**Estos settings no son opcionales.** Cada uno nos costó una tarde:

| Setting | Valor | Si está mal |
|---|---|---|
| Graphics API | **Direct3D11**, Auto Graphics **off** | Fondo negro. En D3D12 el swapchain no le pasa alpha a DWM y no hay código que lo arregle |
| Use DXGI Flip Model Swapchain | **off** | Fondo negro (y solo aplica a D3D11) |
| Render pipeline | **Built-in** | URP no sirve para overlays transparentes en Unity 6 |
| Cámara | Solid Color, alpha **0**, HDR **off** | Con HDR prendido no compone alpha |
| Fullscreen Mode | Windowed | Una franja no puede estar en fullscreen |
| Run In Background | **on** | La mascota se congela al perder el foco |

Dos detalles de Win32 que no son obvios:

- **`WS_EX_LAYERED` va siempre.** El pass-through de mouse de
  `WS_EX_TRANSPARENT` está definido por Microsoft solo para ventanas layered;
  suelto no deja pasar un solo click.
- **El mouse se lee por `GetCursorPos`, no por el Input System.** Cuando la
  ventana es click-through no recibe mensajes de mouse, así que Unity dejaría
  de ver el cursor justo cuando hay que decidir si volver a capturarlo.

### La ventana tiene dos formas

`DesktopWindow` es dueño de la ventana del SO y sabe cambiarle la forma sin
cambiar de escena:

| Forma | Qué es | Quién la pide |
|---|---|---|
| `Strip` | Franja del ancho de la pantalla, 200 px de alto, apoyada sobre la barra de tareas | Es la forma por defecto; vuelve con `SetStrip()` al confirmar |
| `Panel` | Cuadrado centrado en la pantalla | `SetPanel(lado)`, desde `CharacterPicker` al abrir la selección |

No se cambia de escena a propósito: la ventana transparente se configura sobre
la cámara de `Main`, así que cargar otra escena destruiría esa cámara y habría
que rearmar transparencia, click-through y posicionamiento al volver.

El lado del panel **no es un número fijo**: `CharacterPicker` lo calcula a
partir de su cuadrícula, para que sumar personajes no se coma el borde.

### Pixel perfect

```
orthographicSize = altoDeLaVentana / (2 × pixelsPerUnit)
```

**Nadie toca `orthographicSize` a mano.** Lo fija `DesktopWindow` cada vez que
la ventana cambia de forma, porque el alto de la ventana y el zoom de la cámara
son la misma decisión: si se tocan por separado, el pixel art deja de caer
sobre la grilla de píxeles de la pantalla y tiembla al moverse.

El efecto secundario bueno es que **una unidad de mundo siempre mide 100 px en
pantalla**, en la franja y en el panel. Por eso los personajes y los botones se
ven exactamente del mismo tamaño en los dos, aunque las ventanas midan
distinto: 200 px de alto → `1.0`, 640 px → `3.2`.

---

## Reglas de la escena

- **Hay una sola escena**, `Main.unity`. Avisá antes de tocarla.
- Todo lo que pueda vivir en un **prefab** o en un **ScriptableObject**, vive
  ahí y no en la escena. La escena es el punto de conflicto más caro de Unity.
- Nada de crear GameObjects por código en scripts de bootstrap. Si algo tiene
  que existir en la escena, es un prefab y se instancia.

Los `DebugQuitButton` y `DebugTopmostButton` son **andamio**: el `.exe` no
tiene otra forma de cerrarse ni de cambiar settings. Los reemplaza el menú del
icono de bandeja.

---
---

## Buildear y repartir

**El build va por `Tools > CosmereIdle > Buildear`, no por la ventana de Build
Profiles.** El menú hace dos cosas que a mano se olvidan:

- Pasa la lista de escenas a mano (`Main.unity` y nada más). La lista del
  proyecto ya se ensució una vez con `Menu.unity` y a partir de ahí el build
  salía distinto según quién lo hacía.
- Antes de compilar revisa los ajustes de los que depende la ventana
  transparente. Si alguno está mal **corta y no buildea**: un build con esos
  ajustes mal compila igual, pero se ve con el fondo negro, y no hay forma de
  enterarse hasta correr el `.exe`.

Si corta, `Tools > CosmereIdle > Arreglar ajustes de ventana` los deja bien.
Es un menú aparte a propósito: cambiar Player Settings por atrás de quien
apretó "buildear" es el tipo de sorpresa que después nadie entiende.

Queda un ajuste que el chequeo **no** puede ver, porque vive en la escena y no
en Player Settings: la Main Camera tiene que estar en Solid Color con alpha 0 y
con HDR apagado.

### Instalador

`Installer/CosmereIdle.iss`. **Lo compila el mismo `Buildear`** al terminar
el build, y sale en `Installer/Output/`. Para eso quien buildea necesita
[Inno Setup](https://jrsoftware.org/isdl.php) 6.3 o más nuevo instalado; el
menú lo busca solo, esté instalado para todos o solo para el usuario. Si no
está, el build del juego sale igual y avisa que falta el instalador.

Si el build ya está hecho y solo querés rearmar el instalador:
`Tools > CosmereIdle > Compilar solo el instalador`.

Instala **sin pedir administrador**, en la carpeta del usuario. Para un juego
fan sin firma digital eso importa: un instalador que abre el cartel de UAC
espanta a la mitad de la gente.

El número de versión sale de Player Settings: el build escribe
`Installer/Version.iss` y el `.iss` lo incluye. No se toca a mano.

`Installer/Disclaimer.txt` tiene el aviso de contenido fan que exige la
política de Dragonsteel, y el instalador lo muestra antes de instalar.

### Publicar una versión

El juego instalado se actualiza solo desde los
[Releases de GitHub](https://github.com/SolSerki/CosmereIdle/releases).

**No todo commit es una versión.** Se commitea como siempre; una versión es el
momento en que decidís que los jugadores reciban lo que hay.

Para sacarla: **`Tools > CosmereIdle > Publicar versión...`**. Escribís el
número nuevo (viene sugerido) y qué cambió, y el menú hace todo:

1. Sube el **Version** de Player Settings.
2. Buildea el juego y el instalador.
3. Commitea (`Versión 1.3`) y pushea.
4. Crea el release `v1.3` en GitHub con el instalador adjunto.

Antes de tocar nada revisa que gh esté logueado, que tu rama no esté atrás de
GitHub y que el tag no exista. Si tenés cambios sin commitear te los muestra y
no publica hasta que marques que entran en la versión.

Necesita la [CLI de GitHub](https://cli.github.com), una vez por máquina:
`winget install --id GitHub.cli` y `gh auth login`. Si la instalás con Unity
abierto, el menú igual la encuentra.

Si algo falla a mitad de camino, el cartel dice en qué paso quedó. Si el
problema fue al subir, deja copiado el comando para terminar a mano.

A mano es lo mismo, en ese orden: Version, `Tools > CosmereIdle > Buildear`,
commit y push, y `gh release create v1.3 "Installer/Output/CosmereIdle-1.3-setup.exe"
--title v1.3 --notes "..."` desde la carpeta del proyecto.

El instalador **no se commitea** (`Installer/Output/` está en el `.gitignore`):
va adjunto al release, que es de donde lo baja el juego.

**Qué pasa del lado del jugador** (`Scripts/Update/`):

- **Al abrir el juego**, si hay versión nueva, se baja e instala sola, sin
  esperar a que se elijan los personajes: el cartel de arriba de la pantalla
  de selección muestra "Descargando la versión X... 45%" y después
  "Instalando". El juego se cierra, el instalador corre en silencio y lo
  vuelve a abrir.
- **Con el juego abierto** revisa cada 6 horas, y si aparece una versión **solo
  avisa**: una mascota lo dice en un globo y aparece un icono amarillo que late
  en la esquina. Click ahí actualiza en el momento. Si no, se instala en el
  próximo inicio.
- **Mientras consulta a GitHub** se ve: en la pantalla de selección el cartel
  dice "Buscando actualizaciones...", y en la franja aparece el icono de la
  esquina, tenue y latiendo despacio. Si no hay nada nuevo, el cartel queda en
  "v1.1 · Al día".

Detalles que importan:

- **El juego lanza el instalador y recién después se cierra**, así que el
  instalador arranca con el juego todavía abierto. Por eso `CosmereIdle.iss`
  espera a que se cierre (`InitializeSetup`) y tiene `CloseApplications=force`.
  Con `yes`, en modo silencioso Inno encontraba el juego abierto, elegía
  "Abortar" y deshacía la instalación: así fallaba la primera versión. Como el
  arreglo vive en el instalador, sirve también para las copias viejas.
- **El tag tiene que coincidir con el Version del build.** Si publicás
  `v1.2` con un build que adentro dice `1.1`, el juego instala, vuelve a
  arrancar, se ve en `1.1` y cree que sigue habiendo versión nueva. El juego
  lo detecta por el log del instalador: si la instalación terminó bien y la
  versión no cambió, no reintenta y queda solo el aviso. Si el instalador
  falló, reintenta en los próximos inicios, **3 veces como mucho** por versión.
- **Si algo falla, hay log.** En
  `%USERPROFILE%\AppData\LocalLow\Comunidad CosmereAR\CosmereArgIdle\` están
  el `Player.log` del juego (líneas `[GitHubUpdater]`) y `update-install.log`,
  el del instalador.
- **El nombre del adjunto tiene que terminar en `-setup.exe`.** Es como el
  juego lo encuentra entre los archivos del release.
- **Solo se autoinstala la copia que vino del instalador** (la detecta por el
  `unins000.exe` al lado). Un build suelto, como el de la carpeta `Build/`,
  avisa igual, pero el botón abre la página del release en vez de instalar.
  Si no fuera así, abrir el build de prueba instalaría otra copia en otro lado.
- **En el Editor no revisa.** Para probarlo, `checkInEditor` en el prefab
  `Updater`; aun así en el Editor nunca instala.
- Los drafts y los pre-releases **no** cuentan: GitHub no los devuelve como
  "latest". Sirven para subir una versión de prueba sin que le llegue a nadie.
- El repo tiene que ser **público**: el juego consulta la API de GitHub sin
  token.

El aviso usa `PetSpeech.Say`, un botón de la esquina (`UpdateButton`, slot 3)
y el cartel del panel (`UpdateStatusLabel`, dentro del prefab `Updater`).
Cuando exista el menú de la bandeja, el botón se puede reemplazar por una
entrada ahí sin tocar `GitHubUpdater`, que no tiene UI propia.

### No existe el "exe único"

El `.exe` son ~600 KB de lanzador; el juego vive en `_Data`, `UnityPlayer.dll`
y `MonoBleedingEdge`, al lado. Sacarlo de la carpeta no arranca. Para repartir:
zip (lo que espera itch.io) o el instalador.


## Lo que NO estamos haciendo todavía

- **Assembly definitions (`.asmdef`).** Separarían la compilación por carpeta,
  pero agregan fricción de referencias justo cuando Joch está aprendiendo
  Unity. Las carpetas ya alcanzan. Se evalúan si el compile time molesta.
- **`Resources/`.** Carga todo lo que haya adentro al arrancar.
- **El importer de Aseprite.** El proyecto tiene `com.unity.2d.aseprite`
  instalado: si Kato commiteara los `.aseprite` en vez de PNGs, Unity generaría
  sprites, clips y Animator solo, con **un clip por cada tag de frames**. Hoy
  Joch arma los 4 clips a mano por personaje. Vale la pena evaluarlo antes de
  sumar más personajes.

---

## Assets binarios

Van por Git LFS: `.aseprite`, `.ase`, `.png`, `.psd`, `.wav`, `.ttf` y demás.
Ya está en `.gitattributes` — pero sí hay que correr `git lfs install` una vez,
o los binarios entran crudos y el repo se infla sin vuelta atrás.
