# Plan del proyecto: CLI para validar archivos `.env`

Proyecto pequeño de portafolio (1 a 2 semanas), pensado para que se vea terminado, profesional y publicado, sin costo.

**Lenguaje sugerido:** C# (.NET), publicado como `dotnet tool` en NuGet.
**Objetivo real:** no es "una herramienta más", es demostrar que sabes entregar software completo: código limpio, tests, CI/CD, paquete publicado y documentación.

---

## 1. Qué hace (versión 1.0)

Compara un archivo `.env` contra un `.env.example` y reporta:

- Variables **faltantes** (están en el example, no en el `.env`)
- Variables **sobrantes** (están en el `.env`, no en el example)
- Variables **vacías** (existen pero sin valor)

Devuelve un **código de salida** útil para CI:

| Código | Significado |
|--------|-------------|
| `0` | Todo correcto |
| `1` | Se encontraron problemas de validación |
| `2` | Error de uso (archivo no existe, argumentos inválidos) |

### Opciones de línea de comandos

| Opción | Descripción |
|--------|-------------|
| `--env <ruta>` | Archivo a validar (por defecto `.env`) |
| `--example <ruta>` | Archivo de referencia (por defecto `.env.example`) |
| `--strict` | Las variables sobrantes también cuentan como error |
| `--format text\|json` | Formato de salida (por defecto `text`) |
| `--version`, `--help` | Los esperados |

### Fuera de alcance (a propósito)

Para mantenerlo pequeño, **no** hacer en la v1.0:

- Validar tipos o formatos de valores (URLs, puertos, etc.)
- Soportar archivos `.env.local`, `.env.production` en cascada
- Interfaz gráfica o servidor
- Interpolación de variables (`${OTRA_VAR}`)

Estas ideas pueden ir en una sección "Roadmap" del README. Mostrar que sabes recortar alcance también suma.

---

## 2. Estructura del repositorio

```
envcheck/
├── src/
│   └── EnvCheck/
│       ├── EnvCheck.csproj
│       ├── Program.cs              # Punto de entrada y CLI
│       ├── Parsing/
│       │   └── EnvFileParser.cs    # Lee y parsea archivos .env
│       ├── Comparison/
│       │   ├── EnvComparer.cs      # Lógica de comparación
│       │   └── ComparisonResult.cs
│       └── Output/
│           ├── IResultFormatter.cs
│           ├── TextFormatter.cs
│           └── JsonFormatter.cs
├── tests/
│   └── EnvCheck.Tests/
│       ├── EnvFileParserTests.cs
│       ├── EnvComparerTests.cs
│       └── CliIntegrationTests.cs
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── release.yml
├── README.md
├── LICENSE
├── CHANGELOG.md
├── .gitignore
└── EnvCheck.sln
```

**Principio de diseño:** separar parser, comparación y salida. Así cada parte se prueba por separado y el código se lee fácil.

---

## 3. Etapas

### Etapa 1: Preparación (medio día)

- [ ] Crear el repositorio en GitHub con licencia (MIT) y `.gitignore` de .NET
- [ ] Crear la solución y los proyectos (`src` y `tests`)
- [ ] Elegir el nombre del paquete y **verificar que esté libre en NuGet** antes de escribir código
- [ ] Configurar `.editorconfig` y activar nullable reference types
- [ ] Primer commit con README mínimo (título y descripción de una línea)

### Etapa 2: Parser de `.env` + tests (2 a 3 días)

Es la parte más importante y donde se nota tu criterio. Escribe los tests **antes o junto** con el código.

Casos que debe manejar:

- [ ] Líneas vacías y comentarios (`# comentario`)
- [ ] Comentarios al final de la línea (`VAR=valor # comentario`), con cuidado de los valores entre comillas
- [ ] Prefijo `export` (`export VAR=valor`)
- [ ] Comillas simples y dobles (`VAR="valor con espacios"`)
- [ ] Valores con `=` adentro (`URL=https://x.com?a=b`)
- [ ] Valores vacíos (`VAR=` y `VAR=""`)
- [ ] Espacios alrededor de `=` y al inicio de la línea
- [ ] Finales de línea Windows (`\r\n`) y Unix (`\n`)
- [ ] BOM al inicio del archivo (UTF-8 con BOM)
- [ ] Claves duplicadas (decide qué hacer: la última gana, o se reporta como advertencia)
- [ ] Líneas inválidas (sin `=`): decidir si se ignoran o se reportan con número de línea
- [ ] Archivo vacío

**Decisión a documentar:** qué hace el parser con cada caso ambiguo. Anótalo en el README, porque demuestra que pensaste el problema.

### Etapa 3: Lógica de comparación + tests (1 día)

- [ ] Función pura que recibe dos conjuntos de variables y devuelve un `ComparisonResult` (faltantes, sobrantes, vacías)
- [ ] Resultados **ordenados** de forma determinista (alfabético) para que la salida sea estable
- [ ] Tests: sin diferencias, solo faltantes, solo sobrantes, ambos, vacías, modo estricto

### Etapa 4: CLI y formatos de salida (1 día)

- [ ] Usar una librería de argumentos (por ejemplo `System.CommandLine`) en lugar de parsear a mano
- [ ] Salida `text` legible, con colores solo si la terminal lo soporta (respetar `NO_COLOR`)
- [ ] Salida `json` estable, pensada para que otras herramientas la consuman
- [ ] Mensajes de error claros cuando un archivo no existe
- [ ] Códigos de salida según la tabla de arriba
- [ ] Tests de integración que ejecuten el CLI con archivos de ejemplo y verifiquen salida y código

**Ejemplo de salida de texto:**

```
✗ 2 variables faltantes:
    DATABASE_URL
    API_KEY
⚠ 1 variable vacía:
    SECRET_KEY
```

### Etapa 5: CI en GitHub Actions (medio día)

`ci.yml`, en cada push y pull request:

- [ ] Restaurar, compilar y correr tests
- [ ] Probar en Linux y Windows (matriz de sistemas operativos)
- [ ] Reportar cobertura de tests
- [ ] Verificar formato del código (`dotnet format --verify-no-changes`)
- [ ] Badge de estado del build en el README

### Etapa 6: Empaquetado y publicación (1 día)

En el `.csproj`:

- [ ] `PackAsTool` en `true` y `ToolCommandName` definido (ej. `envcheck`)
- [ ] `PackageId`, `Description`, `Authors`, `PackageTags`, `RepositoryUrl`, `PackageLicenseExpression`
- [ ] `PackageReadmeFile` apuntando al README para que se vea en NuGet
- [ ] Versionado semántico (`0.1.0`, luego `1.0.0`)

`release.yml`, al crear un tag tipo `v1.0.0`:

- [ ] Compilar, correr tests, empaquetar y publicar a NuGet
- [ ] Guardar la API key de NuGet como **secret** del repositorio, nunca en el código
- [ ] Crear un GitHub Release con las notas del cambio

Verificación final: en una máquina limpia, `dotnet tool install -g <paquete>` y ejecutar el comando.

### Etapa 7: Documentación y presentación (1 día)

- [ ] README completo (ver sección 5)
- [ ] `CHANGELOG.md`
- [ ] Descripción y topics del repositorio en GitHub
- [ ] Fijar (pin) el repo en tu perfil

---

## 4. Calidad: lo que debe verse en el repositorio

- **Tests:** unitarios para parser y comparador, de integración para el CLI. Apunta a cobertura alta en el parser.
- **Commits claros:** mensajes descriptivos, preferiblemente con convención (`feat:`, `fix:`, `test:`, `docs:`).
- **Pull requests propios:** trabaja en ramas y haz PR hacia `main`, aunque estés solo. Deja el historial de CI visible.
- **Sin código muerto ni TODOs sueltos** en la rama principal.
- **Manejo de errores:** nada de excepciones sin controlar ni stack traces para el usuario final.
- **Compatibilidad:** probar que funciona en Linux, Windows y macOS si es posible.

---

## 5. Estructura sugerida del README

1. Nombre y descripción en una línea
2. Badges: build, versión de NuGet, licencia
3. **GIF o captura** de la salida en una terminal
4. Instalación en una línea (`dotnet tool install -g ...`)
5. Uso básico con ejemplos
6. Tabla de opciones y códigos de salida
7. **Uso en CI** (ejemplo con GitHub Actions)
8. Decisiones del parser (qué soporta y cómo trata los casos ambiguos)
9. Roadmap (lo que quedó fuera a propósito)
10. Cómo contribuir y licencia

---

## 6. Extras opcionales (solo si sobra tiempo)

Elige **uno o dos**, no todos:

- Un hook de `pre-commit` o ejemplo para Husky
- Una GitHub Action reutilizable que envuelva la herramienta
- Imagen Docker publicada en GitHub Container Registry
- Comando `envcheck init` que genere un `.env.example` desde un `.env`, ocultando los valores
- Benchmarks sencillos con BenchmarkDotNet sobre archivos grandes
- Detección de posibles secretos expuestos en el `.env.example` (valores que parezcan claves reales)

---

## 7. Errores comunes a evitar

- Empezar por el empaquetado o el diseño del logo antes de tener el parser probado
- Crecer el alcance ("ya que estoy, agrego validación de tipos...")
- Dejar la publicación para el final y descubrir que el nombre del paquete ya existe
- Subir un `.env` real al repositorio (revisa el historial de commits antes de hacerlo público)
- README sin ejemplos de salida
- Tests que solo cubren el caso feliz

---

## 8. Definición de "terminado"

- [ ] `dotnet tool install -g` funciona desde NuGet en una máquina limpia
- [ ] CI en verde en `main`
- [ ] Tests del parser con casos borde cubiertos
- [ ] README con GIF, instalación, uso y ejemplo de CI
- [ ] Release `1.0.0` publicado en GitHub
- [ ] Repositorio fijado en tu perfil de GitHub

---

## 9. Cómo contarlo en una entrevista

Prepara respuestas cortas para:

- **¿Qué problema resuelve?** Evita que una app falle en producción o en el equipo por una variable de entorno olvidada.
- **¿Qué fue lo más difícil?** Seguramente el parser y sus casos ambiguos (comillas, comentarios, `=` en valores).
- **¿Qué decisiones tomaste y por qué?** Separar parser, comparador y salida; recortar alcance; códigos de salida pensados para CI.
- **¿Qué harías después?** Lo que dejaste en el roadmap.
