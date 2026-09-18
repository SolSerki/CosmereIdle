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

### Pixel perfect

```
orthographicSize = altoDeLaFranja / (2 × pixelsPerUnit)
```

Con franja de 120 px y PPU 100 → `0.6`. Si alguien cambia el alto de la franja
y no recalcula esto, el pixel art empieza a temblar.

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
