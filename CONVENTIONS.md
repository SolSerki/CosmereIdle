# Convenciones del proyecto

Unity 6000.3.22f1 · Built-in Render Pipeline · Color space Gamma.

El objetivo de este archivo es que los tres podamos trabajar a la vez sin
pisarnos. Es corto a propósito.

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

## Estructura

```
Assets/
  Art/          arte que no pertenece a un personaje: globo, iconos, fuente   [Kato]
  Characters/   una carpeta por personaje (ver abajo)                    [los tres]
  Prefabs/      todo lo que sea instanciable
  Scenes/       Main.unity, la única escena
  Scripts/
    Pet/        comportamientos, movimiento, estado del personaje          [Serka]
    Desktop/    ventana transparente, always-on-top, barra de tareas       [Serka]
    Speech/     frases y globo de diálogo                                   [Joch]
    App/        bandeja del sistema, settings, preferencias                 [Joch]
  Settings/     InputSystem_Actions y afines
```

**Trabajá dentro de tu carpeta.** Si necesitás tocar la de otro, avisá en
Discord antes. No es burocracia: es que los conflictos de git se resuelven
por archivo, así que mientras cada uno escriba en archivos distintos no hay
conflicto posible.

## Un personaje = una carpeta

```
Assets/Characters/<Nombre>/
  <Nombre>.aseprite        el sprite con sus tags de animación      [Kato]
  <Nombre>.asset           CharacterDefinition (ScriptableObject)   [Serka]
  <Nombre>.phrases.json    las frases, por categoría                 [Joch]
```

Tres archivos, tres dueños, cero conflictos. Y el día que alguien de la
comunidad quiera aportar un personaje, la respuesta es "mandanos una carpeta".

Unity importa el `.aseprite` directo: genera los sprites, los clips de
animación y el Animator solo. **El importer crea un clip por cada tag de
frames**, así que el nombre del tag es el nombre del comportamiento. Si el tag
dice `read`, el código encuentra `read`. Si dice `leyendo`, no lo encuentra.

Tags de la v1: `idle`, `walk`, `read`, `sleep`, `talk`, `drag`, `special`.

## Reglas de la escena

- **Hay una sola escena**, `Main.unity`. Avisá antes de tocarla.
- Todo lo que pueda vivir en un **prefab** o en un **ScriptableObject**, vive
  ahí y no en la escena. La escena es el punto de conflicto más caro de Unity;
  mientras esté casi vacía, no nos molesta.

## Lo que NO estamos haciendo todavía

- **Assembly definitions (`.asmdef`).** Separarían la compilación por carpeta,
  pero agregan fricción de referencias justo cuando Joch está aprendiendo
  Unity. Las carpetas ya alcanzan para no pisarnos. Se evalúan si el tiempo de
  compilación empieza a molestar.
- **`Resources/`.** Carga todo lo que haya adentro al arrancar. Si necesitamos
  cargar algo por nombre, lo resolvemos con referencias directas o
  Addressables.

## Assets binarios

Van por Git LFS: `.aseprite`, `.ase`, `.png`, `.psd`, `.wav`, `.ttf` y demás.
Ya está en `.gitattributes`, no hay que hacer nada especial — pero sí correr
`git lfs install` una vez, o los binarios entran crudos y el repo se infla sin
vuelta atrás.
