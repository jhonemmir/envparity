# Plan del proyecto (v2): `envparity` — CLI para validar archivos `.env`

Proyecto de portafolio de 1 a 2 semanas, pensado para verse terminado y publicado sin costo.

**Stack:** C# / .NET 10 (LTS) → `dotnet tool` publicado en NuGet.
**Paquete / comando:** `envparity` (ID libre en NuGet, verificado el 2026-11-12).
**Objetivo real:** demostrar que sabes entregar software completo: especificación, código limpio, tests, CI/CD, paquete publicado y documentación.

> La versión anterior de este plan quedó guardada como `plan-cli-env-validator.v1.md`.

---

## Qué cambia respecto a la v1

| # | Problema de la v1 | Solución en la v2 |
|---|---|---|
| 1 | El parser enumeraba casos pero no decidía ninguno | Gramática + tabla de 43 casos, cada fila con su test |
| 2 | No se sabía si "vacía" aplicaba al `.env` o al `.env.example` | Reglas explícitas y conjuntos disjuntos |
| 3 | `--strict` sin semántica completa | `strict` escala sobrantes **y** warnings del parser |
| 4 | La tabla de exit codes chocaba con `System.CommandLine` | Intercepción de errores de parseo → `2` |
| 5 | Salida JSON sin contrato | Esquema versionado y estable, documentado |
| 6 | Riesgo de que `dotnet format` diera rojos aleatorios | `global.json` + SDK fijado |
| 7 | Tests de integración lentos y frágiles | CLI testeado en proceso, fixtures en memoria |
| 8 | Empaquetado dejado para el final | Spike de empaquetado a mitad de proyecto |
| 9 | .NET 8 (EOL nov 2026) | `net10.0` (LTS hasta nov 2028) |
| 10 | No se decía si se imprimen valores | Regla dura: **nunca** se imprime un valor |

---

## 0. Resumen ejecutivo

`envparity` compara un `.env` contra un archivo de referencia (`.env.example`) y reporta variables **faltantes**, **vacías** y **sobrantes**, con códigos de salida pensados para CI y salida `text` o `json`.

| Etapa | Tiempo |
|---|---|
| 0. Decisiones y especificación | 0,5 día |
| 1. Setup del repositorio | 0,5 día |
| 2. Parser + tests | 3 días |
| 3. Comparador + tests | 0,5 día |
| 4. CLI, formatos y tests | 1 día |
| 5. Spike de empaquetado + primera alpha | 0,5 día |
| 6. CI | 0,5 día |
| 7. Release 1.0 | 0,5 día |
| 8. Documentación y presentación | 1 día |
| **Total** | **~8 días** |

La etapa 2 sube a **3 días** de forma explícita: 43 casos, las aserciones de canaria y el UTF-8 estricto no caben con margen en 2,5. Los días 11–14 quedan como colchón para CI y publicación, no para el parser.

---

## 1. Decisiones de diseño (ADR corto)

Estas decisiones se escriben **antes** de codificar y no se re-discuten a mitad del proyecto. Cada una tiene consecuencias directas en los tests.

| ID | Decisión | Elección | Por qué |
|---|---|---|---|
| D1 | Alcance | Comparar nombres y detectar valores vacíos **solo en `E ∩ X`** (`empty` no aplica a claves presentes en un solo archivo). **No** se comparan valores ni formatos | Pequeño, explicable, útil |
| D2 | Idioma | Código, mensajes del CLI, README y CHANGELOG en **inglés**. Opcional `README.es.md` | Audiencia real de un paquete NuGet |
| D3 | Case sensitivity | Nombres **case-sensitive** siempre | Es un archivo de texto, no el entorno del SO |
| D4 | Clasificación | `missing`, `empty`, `extra` son conjuntos **disjuntos** por construcción | Sin ambigüedad ni duplicados en el reporte |
| D5 | Claves duplicadas | Gana la **última**; se emite warning `duplicate-key` con ambas líneas | Compatible con dotenv, pero visible |
| D6 | Líneas inválidas | Son **error** (`exit 1`) y se reportan **todas** con archivo y línea | Fallar ruidosamente es el punto de un validador |
| D7 | Encoding | Solo UTF-8 estricto. BOM inicial ignorado. UTF-8 inválido → `exit 2` | Evita corrupción silenciosa |
| D8 | Valores multilínea | No soportados en v1.0; se reportan como línea inválida | Complejidad alta, valor bajo |
| D9 | Escapes | El valor es **opaco**: no se desescapa y las `\` se conservan. Pero `\"` no cierra una comilla doble (paridad de `\`) | Los JSON en credenciales son comunes; sin esto se producen falsos positivos en CI |
| D10 | Secretos | **Nunca** se imprime ni se serializa un valor, solo nombres. Los issues llevan solo código, archivo y línea: los mensajes son plantillas fijas y no citan texto del archivo. El label de archivo es la ruta del usuario, nunca contenido leído. Las excepciones de decodificación se traducen a un mensaje fijo | Los logs de CI son públicos o compartidos; citar la línea ofensora es la fuga más probable |
| D11 | Target | `net10.0` | .NET 8 salió de soporte en nov 2026 |
| D12 | Dependencias | Solo `System.CommandLine` 3.0.0 en runtime (verificado en NuGet el 2026-11-11). Tests con xUnit (+ `coverlet.collector`) | Pocas dependencias = paquete confiable |
| D13 | Contrato de salida | Resultado en `stdout`; errores en `stderr`; JSON versionado. Con `exit 2` no se escribe nada en `stdout` | `stdout` siempre parseable o vacío, nunca JSON a medias |
| D14 | `--strict` | Escala a error: sobrantes **y** warnings del parser (duplicados) | Un solo concepto de "modo estricto" |

**Consecuencia importante de D9:** como los valores son opacos, no hay que interpretar `\n`, `${VAR}` ni nada similar. Eso reduce el parser a delimitadores y espacios, que es exactamente lo que se puede probar exhaustivamente.

---

## 2. Especificación funcional

### 2.1 Entradas

| Archivo | Default | Obligatorio |
|---|---|---|
| A validar (`--env`) | `.env` | Sí, debe existir |
| Referencia (`--example`) | `.env.example` | Sí, debe existir |

Rutas relativas al directorio actual. No hay globs ni cascada de archivos.

### 2.2 Clasificación

Sea `E` el conjunto de claves del archivo a validar y `X` el de la referencia:

| Conjunto | Definición | Efecto en exit code |
|---|---|---|
| `missing` | `X \ E` | Error |
| `empty` | `{ k ∈ E ∩ X : valor(k) == "" }` | Error |
| `extra` | `E \ X` | Warning; error solo con `--strict` |

- Por construcción los tres conjuntos son **disjuntos**: una variable sobrante y vacía se reporta solo como `extra`.
- "Vacío" significa **longitud cero** después de quitar comillas externas. `VAR=""` y `VAR=` son vacías; `VAR=" "` **no** lo es (espacio literal).
- El número de variables del reporte es `|E|` (claves únicas).
- Todos los listados se ordenan alfabéticamente con comparación **ordinal** para que la salida sea estable y reproducible.

### 2.3 Modo estricto

`--strict` convierte en error:

- las variables `extra`;
- los warnings del parser (`duplicate-key`).

Los **errores** del parser (`invalid-line`, `invalid-key`, `unterminated-quote`) son siempre error, con o sin `--strict`.

Fórmula única del resultado:

```
valid = errors == 0
     && missing.Count == 0
     && empty.Count == 0
     && (extra.Count == 0 || !strict)
     && (warnings == 0 || !strict)
```

En código, esto se encapsula en `ComparisonResult.IsValid(bool strict)`; el CLI nunca recalcula reglas.

### 2.4 Códigos de salida

| Código | Significado | Cuándo |
|---|---|---|
| `0` | Correcto | Sin problemas, o solo warnings sin `--strict` |
| `1` | Problemas de validación | Faltantes, vacías, líneas inválidas, y con `--strict` también sobrantes/duplicados |
| `2` | Error de uso o E/S | Argumento inválido, archivo inexistente/ilegible, encoding inválido |
| `130` | Interrumpido | Ctrl+C |

Casos especiales: `--help` y `--version` → `0`. Nunca se imprime un stack trace.

### 2.5 Opciones del CLI

| Opción | Descripción |
|---|---|
| `--env <ruta>` | Archivo a validar (default `.env`) |
| `--example <ruta>` | Archivo de referencia (default `.env.example`) |
| `--strict` | Sobrantes y warnings cuentan como error |
| `--format text\|json` | Formato de salida (default `text`) |
| `--no-color` | Desactiva color (también se respeta `NO_COLOR`) |
| `--version` | Versión y salida `0` |
| `-h`, `--help` | Ayuda y salida `0` |

Texto de ayuda esperado:

```
envparity 1.0.0 — validate a .env file against a reference file

Usage:
  envparity [options]

Options:
  --env <path>          File to validate (default: .env)
  --example <path>      Reference file (default: .env.example)
  --strict              Treat extra variables and parser warnings as errors
  --format <text|json>  Output format (default: text)
  --no-color            Disable colors (honors NO_COLOR)
  --version             Show version and exit
  -h, --help            Show help and exit

Exit codes:
  0  valid
  1  validation problems
  2  usage or I/O error
```

### 2.6 Salidas de ejemplo

Todo correcto:

```
✓ .env is valid (12 variables checked against .env.example)
```

Con problemas:

```
✗ 2 missing variables:
  API_KEY
  DATABASE_URL

✗ 1 empty variable:
  SECRET_KEY

⚠ 1 extra variable (use --strict to treat it as an error):
  DEBUG

envparity: 3 problems, 1 warning
```

Con error de parseo:

```
✗ .env:12 invalid line (expected KEY=VALUE)
✗ .env:20 unterminated quoted value

envparity: 2 problems
```

Error de uso (stderr, exit 2):

```
envparity: file not found: .env
```

### 2.7 Fuera de alcance (a propósito)

- Validar tipos o formatos de valores (URLs, puertos, booleanos).
- Cascada de `.env.local`, `.env.production`, etc.
- Interpolación `${OTRA_VAR}`. Los escapes no se interpretan; la única excepción es que `\"` no cierra un valor entre comillas dobles (el valor sigue siendo crudo).
- Valores multilínea.
- GUI o servidor.

Todo esto vive en la sección **Roadmap** del README.

---

## 3. Especificación del parser

Esta sección es la más importante del proyecto. Cada fila de la tabla es un test.

### 3.1 Algoritmo por línea

Para cada línea (`\n`, `\r\n` o `\r`; numeración 1-based):

1. Si es la primera línea, quitar un BOM inicial (`U+FEFF`).
2. `TrimStart(' ', '\t')`: solo espacios y tabs, nunca `Trim()`.
3. Si queda vacía o empieza con `#` → ignorar.
4. Si empieza con `export` seguido de al menos un espacio/tab → quitarlo y volver a recortar espacios/tabs.
5. Buscar el **primer** `=`. Si no existe → `invalid-line`.
6. Clave = texto anterior a `=`, con `TrimEnd(' ', '\t')`. Validar `\A[A-Za-z_][A-Za-z0-9_]*\z` (o un bucle de caracteres); si falla → `invalid-key`.
7. Resto = texto posterior a `=`, con `TrimStart(' ', '\t')`.
8. Si el resto empieza con `"` o `'`:
   - buscar la comilla de cierre del mismo tipo en la misma línea;
   - en comillas dobles, una `"` precedida por un número **impar** de `\` consecutivas no cierra; en comillas simples no hay escapes;
   - si no existe cierre → `unterminated-quote`;
   - valor = texto **crudo** entre comillas (no se desescapa; las `\` se conservan);
   - lo que sigue al cierre debe ser vacío o empezar con `#` (con o sin espacio); si no → `invalid-line`.
9. Si no está entre comillas:
   - cortar en el primer `#` que esté al inicio del resto o precedido por espacio/tab;
   - `TrimEnd(' ', '\t')` del valor resultante.
10. `IsEmpty = value.Length == 0`.
11. Si la clave ya existe → warning `duplicate-key` (con la línea original) y **reemplazar** el valor.
12. Si no → agregar.

Se leen todos los archivos como **bytes** y se decodifican con UTF-8 estricto:

```csharp
var bytes = File.ReadAllBytes(path);
var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
    .GetString(bytes);
```

No usar `File.ReadAllText` ni `StreamReader`: detectan BOMs UTF-16/UTF-32 y aceptarían un archivo UTF-16. El BOM UTF-8 queda como `U+FEFF` y lo quita el parser. Un byte inválido lanza `DecoderFallbackException` → el CLI lo traduce a `exit 2` con un mensaje **fijo**; el texto de la excepción nunca se reenvía, porque puede contener los bytes no decodificables.

### 3.2 Tabla de casos (una fila = un test)

> Fuente de verdad mantenida: [`docs/parser-cases.md`](docs/parser-cases.md). Si ambas tablas divergen, manda el documento.

| # | Caso | Comportamiento | Test |
|---|---|---|---|
| 1 | Línea vacía / solo espacios | Ignorar | `Parse_BlankLine_Ignored` |
| 2 | `# comentario` | Ignorar | `Parse_Comment_Ignored` |
| 3 | `  # comentario` | Ignorar | `Parse_IndentedComment_Ignored` |
| 4 | `A=a#b` | Valor `a#b` (sin espacio antes del `#`) | `Parse_HashWithoutSpace_KeptInValue` |
| 5 | `A=a # b` | Valor `a` | `Parse_HashAfterSpace_StartsComment` |
| 6 | `A=# b` | Valor vacío | `Parse_HashAtValueStart_EmptyValue` |
| 7 | `A="a # b"` | Valor `a # b` | `Parse_HashInsideQuotes_Literal` |
| 8 | `A='a # b'` | Valor `a # b` | `Parse_HashInsideSingleQuotes_Literal` |
| 9 | `export A=1` | Clave `A` | `Parse_ExportPrefix_Stripped` |
| 10 | `export    A=1` | Clave `A` | `Parse_ExportWithManySpaces_Stripped` |
| 11 | `exportA=1` | Clave `exportA` | `Parse_ExportWithoutSpace_IsKey` |
| 12 | `A=" x "` | Valor ` x `, **no** vacía | `Parse_QuotedWhitespace_NotEmpty` |
| 13 | `A=""` | Valor vacío | `Parse_EmptyDoubleQuotes_Empty` |
| 14 | `A=''` | Valor vacío | `Parse_EmptySingleQuotes_Empty` |
| 15 | `A=` | Valor vacío | `Parse_MissingValue_Empty` |
| 16 | `A=   ` | Valor vacío | `Parse_WhitespaceValue_Empty` |
| 17 | `A=x=y` | Valor `x=y` | `Parse_EqualsInValue_Kept` |
| 18 | `A = x` | Clave `A`, valor `x` | `Parse_SpacesAroundEquals_Trimmed` |
| 19 | `  A=x` | Clave `A` | `Parse_LeadingSpaces_Trimmed` |
| 20 | `A="x" extra` | `invalid-line` | `Parse_TrailingGarbageAfterQuote_Invalid` |
| 21 | `A="x` | `unterminated-quote` | `Parse_UnterminatedQuote_Invalid` |
| 22 | `A=x"y` | Valor `x"y` (comilla literal) | `Parse_QuoteInsideUnquotedValue_Literal` |
| 23 | `=x` | `invalid-key` | `Parse_EmptyKey_Invalid` |
| 24 | `1A=x` | `invalid-key` | `Parse_KeyStartingWithDigit_Invalid` |
| 25 | `A.B=x` | `invalid-key` | `Parse_KeyWithDot_Invalid` |
| 26 | `A B=x` | `invalid-key` | `Parse_KeyWithSpace_Invalid` |
| 27 | `A` (sin `=`) | `invalid-line` | `Parse_NoEquals_Invalid` |
| 28 | `A=1` dos veces | Última gana + `duplicate-key` | `Parse_DuplicateKey_LastWinsWithWarning` |
| 29 | BOM al inicio | Ignorar | `Parse_LeadingBom_Ignored` |
| 30 | CRLF / LF / CR | Aceptar los tres | `Parse_AllNewlineStyles_Accepted` |
| 31 | Archivo vacío | 0 variables, sin issues | `Parse_EmptyFile_NoIssues` |
| 32 | Última línea sin salto | Aceptar | `Parse_NoTrailingNewline_Accepted` |
| 33 | UTF-8 inválido o archivo UTF-16 | `exit 2` (a nivel CLI) | `Run_NonUtf8File_Returns2` |
| 34 | `export` solo | `invalid-line` | `Parse_ExportOnly_Invalid` |
| 35 | Valor con emoji/Unicode | Se conserva | `Parse_UnicodeValue_Kept` |
| 36 | `A="{\"a\":1}"` | Válido; valor crudo `{\"a\":1}` | `Parse_EscapedDoubleQuote_DoesNotCloseValue` |
| 37 | `A="a\\"` | Válido; valor crudo `a\\` | `Parse_EvenBackslashes_BeforeQuote_Closes` |
| 38 | `A="x\"` | `unterminated-quote` | `Parse_EscapedQuoteAtEnd_Unterminated` |
| 39 | BOM en la línea 2 | `invalid-key` en esa línea; el resto se parsea | `Parse_BomAfterFirstCharacter_InvalidKey` |
| 40 | `A="x⏎B=1` | `unterminated-quote` en línea 1, `B=1` se parsea | `Parse_UnterminatedQuote_DoesNotStopParsing` |
| 41 | `A="x"·#·nota` | Comentario tras cierre; valor `x` | `Parse_CommentAfterClosingQuote_Allowed` |
| 42 | `A="x"#nota` | Comentario sin espacio; valor `x` | `Parse_HashImmediatelyAfterClosingQuote_Comment` |
| 43 | `export·=1` | `invalid-key` | `Parse_ExportBeforeEmptyKey_InvalidKey` |

Los tests del parser construyen los inputs como **strings en memoria** (`[Theory]` con `InlineData` o `MemberData`). No se usan archivos fixtures para el parser: así no dependes del line ending del repo ni de `.gitignore`.

### 3.3 Modelo de datos

```csharp
enum IssueSeverity { Warning, Error }

sealed record EnvVariable(string Name, string Value, int Line, bool IsEmpty);

sealed record ParseIssue(
    IssueSeverity Severity,
    string Code,      // duplicate-key | invalid-line | invalid-key | unterminated-quote
    string File,
    int Line,
    string Message);

sealed record ParseResult(
    IReadOnlyDictionary<string, EnvVariable> Variables,
    IReadOnlyList<ParseIssue> Issues);
```

### 3.4 Formato de archivo de referencia

`.env.example` se parsea con **las mismas reglas**. Un example inválido también produce `exit 1`: es exactamente el tipo de error que debe romper CI.

---

## 4. Contrato de salida

### 4.1 Reglas generales

- `stdout` contiene **solo** el resultado (texto o JSON). Nada más.
- `stderr` contiene **solo** errores de uso o E/S, en texto legible.
- En `exit 2` no se escribe **nada** en `stdout`: el error va solo a `stderr`. La señal documentada es «`stdout` vacío + código 2», nunca un JSON truncado.
- `--format json` implica sin color y sin símbolos Unicode.
- Ninguna salida contiene valores de variables ni texto de las líneas (D10). Los mensajes de issue usan solo archivo y número de línea.

### 4.2 Formato `text`

- Encabezado de resultado: `✓` / `✗` / `⚠`.
- Grupos en orden fijo: `missing` → `empty` → `extra` → `issues`.
- Nombres ordenados alfabéticamente (ordinal), uno por línea con dos espacios de sangría.
- Línea final de resumen solo si hay problemas o warnings.
- Color: verde `0`, rojo `1`, amarillo `2`, cyan para rutas. Se desactiva si:
  - se pasa `--no-color`, o
  - existe `NO_COLOR` con valor no vacío (spec de no-color.org), o
  - la salida está redirigida (`Console.IsOutputRedirected`).
- Símbolos: si `Console.OutputEncoding` no se puede fijar a UTF-8 (Windows antiguo), usar `OK`, `X`, `!` como fallback. El formateador recibe un flag `useUnicode`.

### 4.3 Formato `json`

Esquema versionado. Ejemplo completo:

```json
{
  "schemaVersion": 1,
  "toolVersion": "1.0.0",
  "valid": false,
  "strict": false,
  "files": {
    "env": ".env",
    "example": ".env.example"
  },
  "summary": {
    "variables": 10,
    "missing": 2,
    "empty": 1,
    "extra": 1,
    "errors": 0,
    "warnings": 1
  },
  "missing": ["API_KEY", "DATABASE_URL"],
  "empty": ["SECRET_KEY"],
  "extra": ["DEBUG"],
  "issues": [
    {
      "severity": "warning",
      "code": "duplicate-key",
      "file": ".env",
      "line": 12,
      "message": "Duplicate key; the last definition wins (first defined on line 11)."
    }
  ]
}
```

Reglas del contrato:

- El orden de las propiedades es **fijo** (el del DTO, igual al del ejemplo); los arrays van en orden alfabético.
- `schemaVersion` se incrementa solo ante cambios incompatibles.
- `summary.errors`/`warnings` cuentan `issues`, no variables.
- Los nombres de archivo se emiten tal como los pasó el usuario.
- Se serializa con `System.Text.Json` y `JsonSerializerOptions` fijos: indentado 2 espacios, `PropertyNamingPolicy = CamelCase` y `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (la salida no es HTML).

### 4.4 Estabilidad

Los tests del formateador comparan el JSON contra un string esperado (o lo deserializan y comparan objetos). Así un cambio accidental de contrato rompe CI en lugar de romper a los usuarios.

---

## 5. Arquitectura y estructura

### 5.1 Proyectos

- `src/EnvParity` — la herramienta (un único proyecto).
- `tests/EnvParity.Tests` — unitarios + integración en proceso.

Un solo proyecto de producción es suficiente y demuestra criterio: no hay sobre-arquitectura.

### 5.2 Clases y responsabilidades

```
src/EnvParity/
├── EnvParity.csproj
├── Program.cs                  # mínimo: encoding, try/catch, return app.Run(...)
├── Cli/
│   ├── CliApplication.cs       # Run(string[] args, TextWriter out, TextWriter err) : int
│   └── CliOptions.cs           # opciones validadas
├── Parsing/
│   ├── EnvFileParser.cs        # texto -> ParseResult (puro, sin I/O)
│   ├── EnvFileReader.cs        # ruta -> texto (UTF-8, errores de I/O)
│   ├── EnvVariable.cs
│   ├── ParseIssue.cs
│   └── ParseResult.cs
├── Comparison/
│   ├── EnvComparer.cs          # dos ParseResult -> ComparisonResult (puro)
│   └── ComparisonResult.cs     # incluye IsValid(strict)
└── Output/
    ├── IResultFormatter.cs
    ├── TextFormatter.cs
    ├── JsonFormatter.cs
    └── ColorWriter.cs          # ANSI, NO_COLOR, redirección, fallback ASCII
```

Reglas:

- `EnvFileParser` **no toca el disco**. Recibe `string content` y un `fileLabel` que es la **ruta tal como la escribió el usuario**. El CLI nunca deriva ese label de nada leído dentro del archivo.
- `EnvComparer` **no formatea**. Devuelve listas ordenadas.
- `TextFormatter`/`JsonFormatter` **no deciden** el exit code.
- `CliApplication` orquesta: parsear args → leer archivos → comparar → formatear → devolver código. Es el único que conoce `Program`.

### 5.3 Testabilidad del CLI

`CliApplication.Run(string[] args, TextWriter stdout, TextWriter stderr)` devuelve `int`.

- Los tests de integración lo invocan en proceso: rápidos, sin `dotnet run`, sin procesos ni handles.
- `Program.Main` solo hace:

```csharp
try
{
    Console.OutputEncoding = Encoding.UTF8;
    return new CliApplication().Run(args, Console.Out, Console.Error);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"envparity: {ex.Message}");
    return 2;
}
```

- El único test end-to-end real (proceso publicado) vive en un test marcado como `[Trait("Category","E2E")]` y se puede excluir en local si molesta.

### 5.4 Configuración de build

`global.json` (fija el SDK; evita rojos aleatorios de `dotnet format`):

```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }
```

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

`.editorconfig` con estilo de llaves, `var` explícito según preferencia, y reglas de nombres. No hace falta StyleCop.

### 5.5 Estructura final del repositorio

```
envparity/
├── .github/
│   ├── workflows/
│   │   ├── ci.yml
│   │   └── release.yml
│   ├── ISSUE_TEMPLATE/bug_report.yml
│   └── pull_request_template.md
├── src/EnvParity/
│   └── ... (ver 5.2)
├── tests/EnvParity.Tests/
│   ├── Parsing/EnvFileParserTests.cs
│   ├── Comparison/EnvComparerTests.cs
│   ├── Cli/CliApplicationTests.cs
│   ├── Output/FormatterTests.cs
│   └── EndToEnd/PublishedToolTests.cs
├── Directory.Build.props
├── global.json
├── .editorconfig
├── .gitignore
├── EnvParity.slnx
├── docs/
│   ├── adr/0001-design-decisions.md
│   └── parser-cases.md
├── README.md
├── README.es.md          (opcional)
├── CHANGELOG.md
├── CONTRIBUTING.md
├── SECURITY.md
└── LICENSE               (MIT)
```

`.gitignore` de .NET **más**:

```gitignore
.env
.env.*
!.env.example
!*.sample
```

La negación de `.env.example` es obligatoria: sin ella tu propio ejemplo no llega al repo.

---

## 6. Etapas

Cada etapa tiene un **criterio de salida** verificable. No se pasa a la siguiente sin cumplirlo.

### Etapa 0 — Decisiones y especificación (0,5 día)

- [x] Revisar `docs/adr/0001-design-decisions.md` y `docs/parser-cases.md`, y enlazarlos desde el README
- [x] Definir el esquema JSON final (sección 4.3)
- [x] Crear el repositorio con licencia MIT y `.gitignore` de .NET corregido
- [x] Verificar que `envparity` sigue libre en NuGet

**Salida:** README con especificación y tabla de decisiones, aunque todavía no haya código.

### Etapa 1 — Setup (0,5 día)

- [x] Crear solución, proyecto `src/EnvParity` y proyecto de tests
- [x] `global.json`, `Directory.Build.props`, `.editorconfig`
- [x] Activar nullable y warnings-as-errors
- [x] Commit inicial: `chore: bootstrap solution`
- [x] Instalar el SDK de .NET 10 en tu máquina (no está instalado)

**Salida:** `dotnet build` y `dotnet test` en verde con un test trivial.

Nota de entorno: SDK **10.0.404** instalado en `~/.dotnet` (usuario, sin root; no está en `PATH`). El SDK 10 genera `EnvParity.slnx` y esa es la solución del repo. Los tests desactivan CA1707 porque los nombres siguen la spec (`Parse_UnterminatedQuote_Invalid`).

### Etapa 2 — Parser + tests (3 días)

- [x] Implementar `EnvFileParser` puro (sin I/O)
- [x] Implementar `EnvFileReader` con UTF-8 estricto (leer bytes, no `File.ReadAllText`)
- [x] Cubrir **los 43 casos** de `docs/parser-cases.md` (el caso 33 se prueba como test de CLI), incluidas las aserciones de canaria de los casos 20, 21, 24 y 27
- [x] Mensajes de issue construidos solo con archivo y línea; `DecoderFallbackException` traducida a mensaje fijo
- [x] Cobertura del parser ≥ 95 % de ramas

**Salida:** parser completo, tests nombrados según la tabla, cero casos "decididos sobre la marcha".

Resultado: **43/43 nombres de test implementados** (90 ejecuciones contando las teorías), parser con **98,6 % de ramas** y lector con 100 %. `CliApplication` es todavía un esqueleto que solo resuelve el caso 33 (archivo no UTF-8 → `exit 2`, `stdout` vacío); el resto del CLI se completa en la Etapa 4.

### Etapa 3 — Comparador + tests (0,5 día)

- [x] `EnvComparer` puro, con conjuntos disjuntos
- [x] Orden alfabético ordinal
- [x] `ComparisonResult.IsValid(strict)` con la fórmula de 2.3
- [x] Tests: sin diferencias, solo faltantes, solo sobrantes, solo vacías, todas, vacía-sobrante (no duplicada), case-sensitive, `strict` con sobrantes, `strict` con duplicados

**Salida:** todos los tests de comparación en verde y la fórmula de validez en un solo lugar.

Resultado: **16 tests de comparación**, comparador y `ComparisonResult` con **100 % de líneas y ramas**, y **106 tests** en total.

### Etapa 4 — CLI y salida (1 día)

- [x] `System.CommandLine` **3.0.0** para parsear (no seguir tutoriales de 2.0-beta)
- [x] Errores de parseo de argumentos → `2` (verificar el comportamiento por defecto de 3.0.0; históricamente devolvía `1`)
- [x] `--help`/`--version` → `0`
- [x] `--format text|json` con el contrato de la sección 4
- [x] Test que verifica que ningún valor de variable aparece en la salida `text` ni `json`, con una canaria sembrada en el archivo
- [x] Test de que `exit 2` deja `stdout` vacío y escribe el error solo en `stderr` (incluido el caso 33)
- [x] Colores con `NO_COLOR`, `--no-color` y redirección
- [x] Fallback ASCII si no hay UTF-8
- [x] `CliApplication.Run` testeado en proceso
- [x] Ctrl+C → `130`

**Salida:** todos los ejemplos de 2.6 reproducidos por tests, incluido el JSON.

Resultado: **151 tests** en total (45 nuevos de CLI/formateadores). Los mensajes de error de `System.CommandLine` no se reenvían porque están localizados: se traducen a plantillas fijas. Los archivos se leen antes de escribir `stdout`, de modo que `exit 2` nunca deja JSON parcial. `Ctrl+C` se cancela en `Program`, y token cancelado → `130`.
Nota: los cambios de esta etapa se publicaron en el commit `5ad3674`.

### Etapa 5 — Spike de empaquetado + alpha (0,5 día)

Objetivo: eliminar el riesgo de "el paquete no funciona" **antes** de invertir en CI y docs.

- [x] `PackAsTool=true`, `ToolCommandName=envparity`
- [x] `dotnet pack -c Release -o artifacts`
- [x] Instalar desde fuente local en un contenedor limpio:

```bash
docker run --rm -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -lc '
  dotnet tool install -g --add-source /src/artifacts envparity --version 0.1.0-alpha.1 &&
  export PATH=$PATH:/root/.dotnet/tools &&
  cd /tmp && envparity --version
'
```

- [x] Publicar `0.1.0-alpha.1` en NuGet para **reservar el ID**, con OIDC (verificado: su scope «publish new packages» cubre la primera publicación; la política debe existir antes del push)
- [x] Verificar `dotnet tool install -g envparity --prerelease`

**Salida:** el comando funciona instalado globalmente desde NuGet. Este es el hito de riesgo cero.

Resultado: el paquete `envparity.0.1.0-alpha.1.nupkg` se genera sin advertencias e incluye metadatos completos (id, versión, licencia MIT, README, repositorio y `DotnetTool`). La instalación desde fuente local se verificó en `mcr.microsoft.com/dotnet/sdk:10.0`: `envparity --version`, `--help`, validación correcta (`0`), archivo ausente (`2`, `stdout` vacío) y JSON con problemas (`1`) funcionan a través del shim de la herramienta global.

Publicación: `0.1.0-alpha.1` se publicó el 2026-11-18 mediante trusted publishing y está disponible en NuGet (`https://www.nuget.org/packages/envparity/0.1.0-alpha.1`). La instalación global desde NuGet se verificó en un contenedor limpio: `dotnet tool install -g envparity --version 0.1.0-alpha.1 --add-source https://www.nuget.org/api/v2/`; `--version` → `0`, `--help` → `0`, caso válido → `0`, archivo ausente → `2` con `stdout` de 0 bytes, JSON con problemas → `1`. El `.nupkg` servido por nuget.org incluye la firma del repositorio y `DotnetToolSettings.xml`. El `toolVersion` informado sigue siendo `1.0.0` porque es el valor fijado por el contrato de salida, aunque el paquete sea `0.1.0-alpha.1`.

Nota operativa: durante la verificación el blob y el `.nuspec` ya estaban disponibles en el *flat container*, pero el índice V3 (`registration5-semver1`) y la búsqueda tardaron más de 12 minutos en propagarse. Por eso `dotnet tool install` contra el source V3 por defecto aún no encontraba el paquete; el feed V2 lo resolvió de inmediato. No es un problema del paquete: la instalación por nombre desde el feed por defecto funcionará cuando termine la indexación.

Workflow: `.github/workflows/publish-alpha.yml` (solo `workflow_dispatch`). Restaura, compila, ejecuta los 151 tests, empaqueta con `dotnet pack -c Release --no-build`, obtiene la clave temporal con `NuGet/login@v1` (`id-token: write`) y hace push con `--skip-duplicate`. Se validó que el YAML parsea, que el paquete con `--no-build` conserva `DotnetToolSettings.xml` y que el login precede inmediatamente al push (clave válida 1 hora, un token → una clave). El primer run falló porque `actions/setup-dotnet` con `cache: true` exige `packages.lock.json`; se eliminó esa opción en el commit `94d9817` y el run siguiente terminó correctamente. Para la Etapa 6 se evaluará restaurar la caché añadiendo lock files.

Pasos ejecutados para la publicación:

1. Política en nuget.org → *Trusted Publishing*: Repository Owner `jhonemmir`, Repository `envparity`, Workflow File `publish-alpha.yml` (solo el nombre), Environment vacío, scope «Push new packages and package versions» con glob `envparity`, sin scopes de unlist ni staging.
2. Secret de GitHub `NUGET_USER` con el **nombre de perfil** de nuget.org (no el email).
3. Workflow lanzado manualmente en GitHub (Actions → *Publish alpha to NuGet* → *Run workflow*), con OIDC y sin API key persistente.
4. Instalación global verificada desde NuGet; casillas de esta etapa marcadas.

### Etapa 6 — CI (0,5 día)

- [x] `ci.yml` con matriz Ubuntu/Windows/macOS
- [x] Restore con cache, build, tests con cobertura, `dotnet format --verify-no-changes`
- [x] `concurrency` para cancelar runs viejos
- [x] Badge de CI en el README

**Salida:** CI verde en `main` y en un PR de prueba.

Resultado: `ci.yml` corre en Ubuntu, Windows y macOS. El run `44255863199` sobre `5184ce6` terminó en verde en los tres SO, con restore, build Release, 151 tests, cobertura y `dotnet format` correctos; los tres artefactos `coverage-<os>` se subieron. El PR de prueba #1 (rama temporal `ci/verify-pr` con un commit vacío) también terminó en verde en los tres SO y se cerró y eliminó después. La caché de `setup-dotnet` usa `cache-dependency-path: '**/packages.lock.json'`, habilitada por los dos `packages.lock.json` añadidos (`src` y `tests`). Se añadió `.gitattributes` con `eol=lf` para que el checkout de Windows/macOS mantenga los finales de línea que exige `.editorconfig` y `dotnet format` sea consistente. Cobertura local del run limpio: 94,3 % de líneas y 95,3 % de ramas globales, parser por encima del 95 % de ramas. El badge de CI quedó enlazado en el README.

### Etapa 7 — Release 1.0 (0,5 día)

- [ ] `release.yml` disparado por tags `v*.*.*`
- [ ] Verificación tag == versión del `.csproj`
- [ ] Publicación con `--skip-duplicate`
- [ ] GitHub Release con `--generate-notes`
- [ ] Instalación limpia de `1.0.0` verificada

**Salida:** `1.0.0` en NuGet + release en GitHub.

### Etapa 8 — Documentación y presentación (1 día)

- [ ] README completo (sección 10)
- [ ] `CHANGELOG.md` (Keep a Changelog)
- [ ] `CONTRIBUTING.md` y `SECURITY.md`
- [ ] GIF reproducible con `vhs`
- [ ] Topics del repo y descripción
- [ ] Repo fijado en el perfil

**Salida:** el repo se entiende en 30 segundos y se instala en una línea.

---

## 7. Estrategia de tests

| Nivel | Qué cubre | Cómo | Cantidad |
|---|---|---|---|
| Unitario parser | Tabla 3.2 completa | `[Theory]` con strings en memoria | ~43 |
| Unitario comparador | Clasificación y `strict` | Strings → `ParseResult` → comparar | ~8 |
| Formateadores | Contrato text/JSON, colores off | `StringWriter` | ~6 |
| Integración CLI | Args, exit codes, stdout/stderr | `CliApplication.Run` en proceso | ~15 |
| End-to-end | Paquete real | `Process` sobre el binario | 1–3 |

Reglas:

- **Nada de fixtures `.env` para el parser**: evita que `core.autocrlf` o `.gitignore` rompan los tests.
- Si algún día necesitas fixtures de archivo, usa extensión `.sample` y cópialos al output del proyecto de tests.
- Nombres de test: `Metodo_Escenario_ResultadoEsperado`.
- Un test por fila de la tabla de decisiones; el nombre de la fila vive en el código.
- Cobertura: parser ≥ 95 % de ramas, global > 90 %. Se publica como artefacto en CI (no se persigue un badge de cobertura).
- `FluentAssertions` **no**: desde la versión 8 tiene licencia comercial para empresas grandes. Usa `Assert` de xUnit o `Shouldly`.
- `coverlet.collector` debe estar referenciado en el proyecto de tests para que funcione `--collect:"XPlat Code Coverage"`.
- Si usas **xUnit v3 / Microsoft.Testing.Platform**, la cobertura se configura distinto (extensión de cobertura de MTP). Con xUnit v2 + VSTest los comandos de abajo funcionan tal cual; elige uno y no mezcles.

Comandos:

```bash
dotnet test
dotnet test --collect:"XPlat Code Coverage" --results-directory artifacts
dotnet test --filter "Category!=E2E"
```

---

## 8. CI/CD

### 8.1 `ci.yml`

```yaml
name: ci

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  test:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
          cache: true
      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts
      - run: dotnet format --verify-no-changes --no-restore
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: coverage-${{ matrix.os }}
          path: artifacts/**/coverage.cobertura.xml
```

Notas:

- `global-json-file` + `global.json` fijan el SDK en todos los SO.
- `dotnet format` va **después** del restore y con `--no-restore`.
- No se usa Codecov: agrega dependencia externa y ruido de permisos para un proyecto pequeño.

### 8.2 `release.yml`

```yaml
name: release

on:
  push:
    tags: ['v*.*.*']

permissions:
  contents: write
  id-token: write

jobs:
  publish:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
          cache: true
      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build
      - name: Verify tag matches project version
        shell: bash
        run: |
          VERSION="${GITHUB_REF_NAME#v}"
          PROJECT_VERSION=$(grep -oP '(?<=<Version>)[^<]+' src/EnvParity/EnvParity.csproj)
          if [ "$VERSION" != "$PROJECT_VERSION" ]; then
            echo "Tag $VERSION does not match csproj version $PROJECT_VERSION" >&2
            exit 1
          fi
      - run: dotnet pack src/EnvParity/EnvParity.csproj -c Release --no-build -o artifacts
      - uses: NuGet/login@v1
        id: nuget
        with:
          user: ${{ secrets.NUGET_USER }}
      - run: >
          dotnet nuget push "artifacts/*.nupkg"
          --api-key ${{ steps.nuget.outputs.NUGET_API_KEY }}
          --source https://api.nuget.org/v3/index.json
          --skip-duplicate
      - run: gh release create "$GITHUB_REF_NAME" artifacts/*.nupkg --generate-notes --verify-tag
        env:
          GH_TOKEN: ${{ github.token }}
```

- **Trusted Publishing (OIDC)** evita guardar una API key de larga duración; se configura la política en nuget.org y solo se guarda `NUGET_USER` como secret (es el **nombre de perfil** de nuget.org, no el email). Verificado el 2026-11-11 en la documentación de NuGet: el scope **«publishing new packages»** permite la **primera** publicación de un ID nuevo, así que no hace falta una API key de arranque.
- Matices operativos de OIDC: la política debe existir antes del primer push; el primer push asigna la propiedad del paquete al usuario de `NuGet/login`; la clave temporal dura **1 hora** y un token sirve para **una** clave; en algunos repos (típicamente privados) la política está activa solo **7 días** hasta que una publicación la consolide.
- Fallback solo si OIDC no estuviera disponible para un ID nuevo: una API key con scope y caducidad cortas para el primer push, migrando a OIDC de inmediato. No es el plan.
- No se publican símbolos (`snupkg`) para una herramienta CLI: menos pasos, menos fallos.
- `--skip-duplicate` hace el workflow idempotente ante reintentos.

### 8.3 Versionado

- `0.1.0-alpha.1` → reserva del ID y prueba del pipeline.
- `1.0.0` → primera versión estable.
- Una versión publicada en NuGet es **inmutable**: solo se puede deslistar, no borrar. Por eso el tag y la versión deben coincidir y el workflow lo verifica.

---

## 9. Empaquetado

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>envparity</ToolCommandName>
  <PackageId>envparity</PackageId>
  <Version>1.0.0</Version>
  <Authors>Tu Nombre</Authors>
  <Description>Validate .env files against a reference file. Made for CI.</Description>
  <PackageTags>dotnet-tool;dotenv;env;ci;validation;linter</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageProjectUrl>https://github.com/jhonemmir/envparity</PackageProjectUrl>
  <RepositoryUrl>https://github.com/jhonemmir/envparity</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>

<ItemGroup>
  <None Include="../../README.md" Pack="true" PackagePath="/" />
  <PackageReference Include="System.CommandLine" Version="3.0.0" />
</ItemGroup>
```

> Versión verificada en nuget.org el 2026-11-11: `System.CommandLine 3.0.0` es estable, MIT, verified y declara target `net10.0`. Si al implementar existe una `3.0.x` posterior, fíjala explícitamente (sin rangos) y revisa su changelog antes de actualizar.

Checklist de verificación del paquete:

- [ ] `dotnet pack` genera un `.nupkg` con el README embebido
- [ ] `dotnet tool install -g --add-source ./artifacts envparity --version X` funciona
- [ ] El comando `envparity` queda en `~/.dotnet/tools` (mencionarlo en el README)
- [ ] `envparity --help` y `--version` salen con `0`
- [ ] El paquete no contiene archivos de tests ni `.env` reales

---

## 10. Documentación

### 10.1 README

1. Nombre + una línea: qué hace y para quién.
2. Badges: CI, versión NuGet, licencia.
3. GIF de terminal (regenerable con `vhs`, no grabación manual).
4. Instalación: `dotnet tool install -g envparity` (+ nota de `PATH`).
5. Uso básico y salidas de ejemplo (éxito y fallo).
6. Tabla de opciones y códigos de salida.
7. **Uso en CI** con ejemplo real.
8. **Decisiones del parser** (la tabla de 3.2, resumida).
9. Contrato de salida JSON (esquema y ejemplo).
10. Roadmap.
11. Cómo contribuir, seguridad y licencia.

Ejemplo de uso en CI:

```yaml
- name: Validate environment
  run: |
    dotnet tool install -g envparity
    envparity --env .env.ci --example .env.example --strict
```

Patrón típico: el pipeline genera `.env.ci` a partir de secrets/variables del repositorio y `envparity` falla el job si falta alguna variable requerida.

### 10.2 Otros documentos

- `CHANGELOG.md` con formato Keep a Changelog.
- `CONTRIBUTING.md`: cómo compilar, testear y `dotnet format`.
- `SECURITY.md`: cómo reportar vulnerabilidades (y la garantía de que la tool no imprime valores).
- `README.es.md` opcional, enlazado desde el inglés.

### 10.3 Presentación

- Descripción del repo: "Validate .env files against .env.example in CI".
- Topics: `dotnet`, `dotnet-tool`, `dotenv`, `env`, `ci`, `cli`, `validation`.
- GIF en el README y pin del repo en el perfil.

---

## 11. Calidad y definición de "terminado"

- [ ] `dotnet tool install -g envparity` funciona en una máquina limpia (contenedor)
- [ ] CI verde en `main` en los tres SO
- [ ] Parser al 95 %+ de cobertura de ramas y los 43 casos de `docs/parser-cases.md` cubiertos
- [ ] Contrato JSON con test de regresión
- [ ] README con GIF, instalación de una línea, ejemplos y uso en CI
- [ ] `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, `LICENSE`
- [ ] Release `1.0.0` en GitHub y en NuGet
- [ ] Repo fijado en el perfil

Criterios de código:

- Sin `TODO` en `main`, sin código muerto, sin excepciones sin controlar.
- `dotnet format --verify-no-changes` limpio.
- Ningún valor de `.env` en logs, tests, fixtures ni README.
- Commits con convención (`feat:`, `fix:`, `test:`, `docs:`, `chore:`).
- Trabajo en ramas con PR propios hacia `main` para dejar el historial de CI visible.

---

## 12. Riesgos y mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Semántica ambigua | Alto | Secciones 1–3 escritas antes de codificar; un test por fila |
| Nombre tomado en NuGet | Alto | Verificado hoy; publicar `0.1.0-alpha.1` en la etapa 5 |
| `System.CommandLine` 3.0.0 distinto a los tutoriales | Medio | Encapsular el parseo en `CliApplication`; versión fijada |
| `dotnet format` falla solo en CI | Medio | `global.json` + `dotnet format --no-restore` |
| Tests frágiles por CRLF/`.gitignore` | Medio | Inputs en memoria; fixtures `.sample` si hicieran falta |
| Empaquetado tarde | Alto | Spike en etapa 5, antes de CI y docs |
| .NET 8 EOL | Medio | `net10.0` desde el inicio |
| Scope creep | Alto | Sección 2.7 y roadmap como contrato |

---

## 13. Roadmap (lo que queda fuera a propósito)

- Validación de tipos y formatos (`url`, `int`, `bool`, regex).
- Cascada de `.env.local` / `.env.{environment}`.
- Interpolación `${VAR}` y escapes.
- Valores multilínea.
- `envparity init` para generar `.env.example` desde `.env` ocultando valores.
- GitHub Action reutilizable que envuelva la herramienta.
- Salida SARIF para GitHub Code Scanning.
- `--ignore <patrón>` para variables exentas.
- Detección de posibles secretos reales en `.env.example`.
- Benchmarks (solo si algún día hay archivos grandes; hoy no aportan).

Recomendación de extras si sobra tiempo: **`envparity init`** y **GitHub Action reutilizable**. Son los que más demuestran criterio de producto y se conectan con la historia de CI.

---

## 14. Errores comunes a evitar

- Empezar por el logo, el README bonito o el empaquetado antes del parser.
- Decidir casos del parser "cuando aparezcan" en lugar de en la tabla.
- Imprimir valores "solo para depurar" y dejarlo en el código.
- Copiar ejemplos de `System.CommandLine` 2.0-beta (no compilan con 3.x).
- Usar `.env` reales en el repo o en los tests; revisar el historial antes de hacerlo público.
- Creer que `dotnet format` es determinista sin `global.json`.
- Tests que solo cubren el caso feliz.
- Publicar `1.0.0` sin haber probado `dotnet tool install` desde NuGet.
- Añadir dependencias (FluentAssertions 8, Codecov, analizadores) que suman ruido sin sumar señal.

---

## 15. Cómo contarlo en una entrevista

- **¿Qué problema resuelve?** Evita despliegues rotos por una variable de entorno olvidada o vacía, y mantiene `.env.example` sincronizado con el equipo.
- **¿Qué fue lo difícil?** El parser y sus ambigüedades: comentarios en línea, comillas, `=` dentro de valores. La solución fue fijar una gramática y convertir cada decisión en un test.
- **¿Qué decisiones tomaste y por qué?**
  - Valores opacos: no se interpretan escapes ni interpolación porque solo importa la presencia y el vacío. Matiz importante si preguntan por `\"`: en comillas dobles, una comilla precedida por un número **impar** de `\` no cierra el valor; la barra afecta a dónde termina el delimitador, pero el valor se devuelve **crudo**, sin desescapar.
  - Conjuntos disjuntos: una variable se reporta una sola vez.
  - Exit codes diseñados para CI, con `strict` como único interruptor.
  - JSON versionado y `stdout` limpio para que otras herramientas lo consuman.
  - Nunca imprimir valores, por seguridad de los logs.
- **¿Qué harías después?** El roadmap: tipos de valores, `init`, SARIF y una Action reutilizable.
- **¿Qué demuestra el proyecto?** Que sabes especificar antes de codificar, probar casos borde, montar CI multiplataforma y publicar un paquete real.

---

## Apéndice A — Orden de trabajo diario sugerido

| Día | Trabajo |
|---|---|
| 1 | Etapa 0 + Etapa 1 |
| 2–4 | Etapa 2 (parser y sus 43 tests) |
| 5 | Etapa 3 + inicio de Etapa 4 |
| 6 | Etapa 4 completa |
| 7 | Etapa 5 (packaging + alpha) |
| 8 | Etapa 6 (CI) |
| 9 | Etapa 7 (release 1.0) |
| 10 | Etapa 8 (docs, GIF, presentación) |

Los días 11–14 quedan como colchón para CI, GIF y detalles de publicación.
