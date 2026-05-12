# Guia del Desarrollador - Bot de Reintegros AssistCard

Documento de referencia para desarrolladores que van a trabajar en el proyecto. Explica la arquitectura, los patrones usados, y muestra con un ejemplo concreto como agregar una nueva funcionalidad.

---

## Indice

1. [Arquitectura general](#1-arquitectura-general)
2. [Capas del proyecto](#2-capas-del-proyecto)
3. [Maquina de estados conversacional](#3-maquina-de-estados-conversacional)
4. [Flujo de un mensaje](#4-flujo-de-un-mensaje)
5. [Anatomia de un State Handler](#5-anatomia-de-un-state-handler)
6. [Mapa de archivos clave](#6-mapa-de-archivos-clave)
7. [Como agregar un nuevo estado/funcionalidad (ejemplo BAC)](#7-como-agregar-un-nuevo-estadofuncionalidad-ejemplo-bac)
8. [Como agregar mensajes en los 3 idiomas](#8-como-agregar-mensajes-en-los-3-idiomas)
9. [Como escribir tests](#9-como-escribir-tests)
10. [Convenciones del proyecto](#10-convenciones-del-proyecto)
11. [Notas tecnicas importantes](#11-notas-tecnicas-importantes)

---

## 1. Arquitectura general

El proyecto sigue **Clean Architecture** (Arquitectura Hexagonal) con 4 capas:

```
  Interfaces (HTTP)      <-- Endpoints FastEndpoints
       |
  Aplicacion             <-- Orquestador, State Handlers, Servicios
       |
  Dominio                <-- Entidades, Enums (sin dependencias externas)
       |
  Infraestructura        <-- Adaptadores concretos (WhatsApp, IA, Redis, SQL, etc.)
```

Las dependencias siempre van hacia adentro: `Interfaces -> Aplicacion -> Dominio`. La capa de `Infraestructura` implementa las interfaces definidas en `Aplicacion/Puertos/`.

---

## 2. Capas del proyecto

### Dominio (`src/Dominio/`)
Entidades puras sin dependencias externas.

| Archivo | Que contiene |
|---------|-------------|
| `EstadoConversacion.cs` | Enum con los 17 estados de la maquina de estados |
| `SesionConversacion.cs` | Modelo de sesion por usuario (estado actual, nombre, IDs, opciones) |
| `HistorialItem.cs` | Item del historial de conversacion (rol, texto, timestamp) |
| `SesionUsuario.cs` | Entidad EF Core para persistencia SQL (opcional) |
| `CacheReintegros.cs` | Entidad EF Core para cache SQL (opcional) |
| `CapturaSalida.cs` | Modelo para capturar respuesta en modo API sincrono |

### Aplicacion (`src/Aplicacion/`)

**Puertos** (`Puertos/`) - Interfaces que definen los contratos con el mundo exterior:

| Puerto | Implementacion | Que hace |
|--------|---------------|----------|
| `IProveedorWhatsApp` | `MetaCloudAdapter` | Enviar mensajes, descargar media |
| `IProveedorIA` | `FoundryAgentAdapter` | Enviar mensajes al agente IA |
| `IProveedorReintegros` | `AssistCardApiAdapter` | Consultar/gestionar reintegros |
| `IProveedorTraduccion` | `AzureTranslatorAdapter` | Detectar idioma, traducir |
| `ICacheAplicacion` | `RedisAdapter` / `InMemoryCacheAdapter` | Cache de sesiones y datos |
| `IAlmacenamientoArchivos` | `AzureBlobAdapter` | Guardar archivos (fotos, PDFs) |
| `IRegistroConversaciones` | `SamuLoggerAdapter` | Registrar conversaciones para auditoria |

**Servicios** (`Servicios/`) - Logica de aplicacion:

| Archivo | Responsabilidad |
|---------|----------------|
| `OrquestadorConversacion.cs` | Punto central: recibe mensaje, carga sesion, detecta comandos, despacha al handler, envia respuesta |
| `SesionManager.cs` | Todas las operaciones de cache (sesion, historial, reintegro, locale, deduplicacion) |
| `BotMessages.cs` | Todos los textos del bot en 3 idiomas (ES/EN/PT) |
| `CommandDetector.cs` | Deteccion de comandos e intenciones del usuario |
| `IdentificadorParser.cs` | Parseo y validacion de identificadores (benefitId, caseId, email, voucher) |
| `OptionSelector.cs` | Interpreta la seleccion del usuario contra las opciones mostradas |
| `TextNormalizer.cs` | Normalizacion de texto (acentos, mayusculas, espacios) |
| `NameValidator.cs` | Validacion de nombres de usuario |
| `GuardrailService.cs` | Validacion de seguridad sobre respuestas de la IA |
| `ReintegroStatusHelper.cs` | Logica para determinar si un reintegro es valido, tiene pagos pendientes, etc. |
| `ReintegrosLocalizer.cs` | Traduccion de campos de reintegro al idioma del usuario |
| `PromptTemplates.cs` | Templates de prompts para la IA |
| `LocaleHelper.cs` | Resolucion de idioma (es/en/pt) |

**State Handlers** (`Servicios/StateHandlers/`) - Un handler por cada estado de la conversacion:

| Handler | Estado | Que hace |
|---------|--------|----------|
| `AwaitingNameHandler` | AwaitingName | Captura el nombre del usuario |
| `MenuPrincipalHandler` | MenuPrincipal | Muestra menu principal, rutea segun opcion |
| `AwaitingIdentifierHandler` | AwaitingIdentifier | Parsea identificador, busca reintegro en API |
| `SelectingReintegroHandler` | SelectingReintegro | Usuario elige entre multiples reintegros |
| `ReintegroMenuHandler` | ReintegroMenu | Menu de opciones del reintegro seleccionado |
| `ReintegroFinancialMenuHandler` | ReintegroFinancialMenu | Detalle financiero del reintegro |
| `ReintegroPaymentsMenuHandler` | ReintegroPaymentsMenu | Detalle de pagos del reintegro |
| `PendingDocsHandler` | PendingDocs* (3 estados) | Flujo de carga de documentos pendientes |
| `ReintegroProblemHandler` | ReintegroProblem | Manejo de quejas/problemas con IA |
| `OtrasConsultasMenuHandler` | OtrasConsultasMenu | Menu de otras consultas |
| `OtrasConsultasProblemHandler` | OtrasConsultasProblem | Otras consultas con IA |
| `ReintegroExitHandler` | ReintegroExit | Cierre de conversacion |
| `StateDispatcher` | (todos) | Rutea al handler correcto segun el estado |

### Infraestructura (`src/Infraestructura/`)
Adaptadores concretos que implementan los puertos. Cada carpeta corresponde a un servicio externo.

### Interfaces (`src/Interfaces/Http/`)
Endpoints HTTP usando FastEndpoints (no Controllers).

| Endpoint | Ruta | Funcion |
|----------|------|---------|
| `WebhookEndpoint` | POST `/webhook/whatsapp` | Recibe mensajes de WhatsApp |
| `VerificacionWebhookEndpoint` | GET `/webhook/whatsapp` | Verificacion del webhook de Meta |
| `SamuChatEndpoint` | POST `/api/chatbot/Agent/Reintegros` | Integracion con plataforma SAMU/Genesys |
| `HealthEndpoint` | GET `/healthz` | Health check |
| `MetricsEndpoint` | GET `/metrics` | Metricas Prometheus |
| `PreProcesadorFirma` | (middleware) | Verificacion de firma HMAC |

---

## 3. Maquina de estados conversacional

El bot funciona como una maquina de estados. Cada usuario tiene un estado actual guardado en Redis, y cada mensaje del usuario se procesa segun ese estado.

```
                    ┌──────────────┐
                    │  AwaitingName │  (primer contacto)
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
         ┌─────────┤ MenuPrincipal │──────────┐
         │         └──────┬───────┘           │
         │                │                    │
  ┌──────▼────────┐  ┌───▼──────────────┐    │
  │ OtrasConsultas │  │AwaitingIdentifier│    │
  │    Menu        │  └───┬─────────────┘    │
  └──────┬────────┘      │                    │
         │          ┌────▼────────────┐       │
  ┌──────▼────────┐ │SelectingReintegro│      │
  │ OtrasConsultas │ └────┬────────────┘      │
  │   Problem     │       │                    │
  └───────────────┘  ┌────▼──────────┐        │
                     │ ReintegroMenu  │────────┘
                     └─┬──┬──┬──┬──┬─┘   (volver)
                       │  │  │  │  │
            ┌──────────┘  │  │  │  └──────────┐
            │             │  │  │              │
     ┌──────▼──────┐ ┌───▼──▼──▼────┐  ┌─────▼──────┐
     │  Financial   │ │  Payments    │  │  Pending   │
     │    Menu      │ │    Menu      │  │   Docs     │
     └─────────────┘ └─────────────┘  └────────────┘
                                            │
                     ┌──────────────┐  ┌────▼────┐
                     │  Reintegro   │  │ Problem │
                     │    Exit      │  │         │
                     └──────────────┘  └─────────┘
```

Los estados se definen en `src/Dominio/Entidades/EstadoConversacion.cs`.

---

## 4. Flujo de un mensaje

Cuando un usuario envia un mensaje por WhatsApp, este es el recorrido:

```
1. Meta envia POST /webhook/whatsapp
2. PreProcesadorFirma verifica firma HMAC (X-Hub-Signature-256)
3. WebhookEndpoint responde 200 inmediatamente y lanza tarea en background
4. OrquestadorConversacion.ProcesarEventoAsync():
   a. Extrae telefono, texto, tipo de mensaje
   b. Semaforo por telefono (evitar concurrencia)
   c. Deduplica por msgId (evitar reprocesamiento)
   d. Si es media (foto/PDF) → ProcesarMediaAsync
   e. Detecta idioma (Azure Translator)
   f. Carga sesion desde Redis (SesionManager)
   g. Detecta comandos globales (hola/menu/atras/cancelar)
   h. StateDispatcher busca el handler para el estado actual
   i. Handler procesa el mensaje y retorna StateResult
   j. OrquestadorConversacion envia respuesta por WhatsApp
   k. Registra en historial y logger
```

---

## 5. Anatomia de un State Handler

Todos los handlers siguen el mismo patron. Ejemplo simplificado:

```csharp
public class MiNuevoHandler : IStateHandler
{
    // 1. Declarar que estado maneja
    public bool CanHandle(EstadoConversacion estado)
        => estado == EstadoConversacion.MiNuevoEstado;

    public async Task<StateResult?> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
    {
        // 2. Parsear/validar input del usuario
        var opcion = OptionSelector.Seleccionar(ctx.TextoUsuario, ctx.Sesion.LastOptions);

        // 3. Segun la opcion, ejecutar logica de negocio
        if (opcion == "ver_detalle")
        {
            var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
            // ... logica ...
        }

        // 4. Transicionar estado si corresponde
        ctx.Sesion.Estado = EstadoConversacion.OtroEstado;
        await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);

        // 5. Retornar mensaje de respuesta
        return new StateResult
        {
            Mensaje = BotMessages.MiMensaje(ctx.Locale),
            Procesado = true
        };
    }
}
```

El `StateHandlerContext` provee todo lo que el handler necesita:
- **Datos**: `Telefono`, `TextoUsuario`, `Locale`, `Sesion`
- **Puertos**: `Ia`, `Reintegros`, `Localizer`
- **Callbacks**: `GuardarSesion`, `ObtenerReintegroActual`, `IntentarEnviar`, etc.

---

## 6. Mapa de archivos clave

Para orientarse rapidamente, estos son los archivos mas importantes:

| Archivo | Lineas | Importancia |
|---------|--------|-------------|
| `OrquestadorConversacion.cs` | ~850 | El cerebro del bot - toda la logica de orquestacion |
| `BotMessages.cs` | ~695 | Todos los textos en 3 idiomas |
| `FoundryAgentAdapter.cs` | ~700 | Integracion con la IA (Azure Foundry) |
| `AssistCardApiAdapter.cs` | ~350 | Cliente de la API de reintegros |
| `MetaCloudAdapter.cs` | ~300 | Envio/recepcion de mensajes WhatsApp |
| `SesionManager.cs` | ~200 | Gestion de cache/sesiones |
| `StateDispatcher.cs` | ~50 | Registro y ruteo de handlers |
| `EstadoConversacion.cs` | ~20 | Enum de estados |
| `ServiceCollectionExtensions.cs` | ~170 | Registro de DI |

---

## 7. Como agregar un nuevo estado/funcionalidad (ejemplo BAC)

### Escenario: Agregar "Actualizar Datos Bancarios"

Supongamos que queremos agregar una nueva opcion al menu del reintegro que permita al usuario actualizar sus datos bancarios (cuenta, CBU/IBAN, titular). Esto implica un nuevo flujo conversacional con su propio estado.

### Paso 1: Agregar el estado al enum

**Archivo:** `src/Dominio/Entidades/EstadoConversacion.cs`

```csharp
public enum EstadoConversacion
{
    // ... estados existentes ...
    ReintegroExit = 15,
    Ended = 16,
    ActualizarDatosBancarios = 17   // <-- NUEVO
}
```

### Paso 2: Crear el handler

**Archivo nuevo:** `src/Aplicacion/Servicios/StateHandlers/ActualizarDatosBancariosHandler.cs`

```csharp
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class ActualizarDatosBancariosHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado)
            => estado == EstadoConversacion.ActualizarDatosBancarios;

        public async Task<StateResult?> HandleAsync(
            StateHandlerContext ctx, CancellationToken ct)
        {
            var texto = (ctx.TextoUsuario ?? "").Trim();

            // Si el usuario quiere volver atras
            if (CommandDetector.EsComandoAtras(texto))
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult
                {
                    Mensaje = BotMessages.MenuReintegro(ctx.Locale),
                    Procesado = true
                };
            }

            // Parsear datos bancarios del texto del usuario
            // (podria ser un flujo de varios pasos con sub-estados)
            var datos = ParsearDatosBancarios(texto);
            if (datos == null)
            {
                return new StateResult
                {
                    Mensaje = BotMessages.SolicitarDatosBancarios(ctx.Locale),
                    Procesado = true
                };
            }

            // Llamar a la API para actualizar
            try
            {
                var benefitId = ctx.Sesion.CurrentBenefitId;
                await ctx.Reintegros.ActualizarDatosBancariosAsync(
                    benefitId, datos, ct);

                ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);

                return new StateResult
                {
                    Mensaje = BotMessages.DatosBancariosActualizados(ctx.Locale),
                    Procesado = true
                };
            }
            catch (Exception ex)
            {
                ctx.Log.LogError(ex,
                    "Error actualizando datos bancarios para {BenefitId}",
                    ctx.Sesion.CurrentBenefitId);

                return new StateResult
                {
                    Mensaje = BotMessages.ErrorGenerico(ctx.Locale),
                    Procesado = true
                };
            }
        }

        private static DatosBancarios? ParsearDatosBancarios(string texto)
        {
            // Logica de parseo...
            return null;
        }
    }
}
```

### Paso 3: Registrar el handler en el dispatcher

**Archivo:** `src/Aplicacion/Servicios/StateHandlers/StateDispatcher.cs`

Agregar la instancia del nuevo handler a la lista del constructor:

```csharp
_handlers = new List<IStateHandler>
{
    // ... handlers existentes ...
    new ReintegroExitHandler(),
    new ActualizarDatosBancariosHandler()   // <-- NUEVO
};
```

### Paso 4: Agregar la transicion desde el menu del reintegro

**Archivo:** `src/Aplicacion/Servicios/StateHandlers/ReintegroMenuHandler.cs`

En la logica donde se procesan las opciones del menu, agregar un nuevo branch:

```csharp
// Dentro del metodo HandleAsync, donde se evaluan las opciones:
if (elegido == "datos_bancarios" || /* deteccion de la opcion */)
{
    ctx.Sesion.Estado = EstadoConversacion.ActualizarDatosBancarios;
    await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
    return new StateResult
    {
        Mensaje = BotMessages.SolicitarDatosBancarios(ctx.Locale),
        Procesado = true
    };
}
```

### Paso 5: Agregar los mensajes en 3 idiomas

**Archivo:** `src/Aplicacion/Servicios/BotMessages.cs`

```csharp
public static string SolicitarDatosBancarios(string locale)
    => LocaleHelper.Loc(locale,
        es: "Por favor, envia tus datos bancarios en el siguiente formato:\n"
          + "Tipo de cuenta | CBU/IBAN | Titular | Email",
        en: "Please send your banking details in the following format:\n"
          + "Account type | CBU/IBAN | Holder name | Email",
        pt: "Por favor, envie seus dados bancarios no seguinte formato:\n"
          + "Tipo de conta | CBU/IBAN | Titular | Email");

public static string DatosBancariosActualizados(string locale)
    => LocaleHelper.Loc(locale,
        es: "Tus datos bancarios fueron actualizados correctamente.",
        en: "Your banking details have been updated successfully.",
        pt: "Seus dados bancarios foram atualizados com sucesso.");
```

### Paso 6: Agregar el estado al dispatch del orquestador

**Archivo:** `src/Aplicacion/Servicios/OrquestadorConversacion.cs`

Buscar la seccion donde se despachan los handlers del grupo 2 (los que estan DESPUES de la deteccion de comandos de estado/pagos). Agregar el nuevo estado a la condicion:

```csharp
// Buscar la linea similar a:
// if (sesion.Estado is EstadoConversacion.SelectingReintegro or
//     EstadoConversacion.ReintegroMenu or ... )

// Agregar:
// EstadoConversacion.ActualizarDatosBancarios
```

> **IMPORTANTE**: Si se omite este paso, el dispatcher nunca ejecutara el handler para este estado, porque el orquestador filtra que estados pasan al dispatcher.

### Paso 7: Agregar la opcion al menu del reintegro

**Archivo:** `src/Aplicacion/Servicios/BotMessages.cs`

Buscar el metodo que genera las opciones del menu del reintegro y agregar la nueva opcion:

```csharp
// En el metodo que construye las opciones del ReintegroMenu:
// Agregar "Actualizar datos bancarios" / "Update banking details" / "Atualizar dados bancarios"
```

### Paso 8: Escribir tests

**Archivo nuevo:** `tests/StateHandlers/ActualizarDatosBancariosHandlerTests.cs`

```csharp
public class ActualizarDatosBancariosHandlerTests
{
    private readonly ActualizarDatosBancariosHandler _sut = new();

    [Fact]
    public void CanHandle_ActualizarDatosBancarios_ReturnsTrue()
    {
        Assert.True(_sut.CanHandle(EstadoConversacion.ActualizarDatosBancarios));
    }

    [Fact]
    public void CanHandle_OtroEstado_ReturnsFalse()
    {
        Assert.False(_sut.CanHandle(EstadoConversacion.MenuPrincipal));
    }

    [Fact]
    public async Task Handle_DatosInvalidos_PideDatosDeNuevo()
    {
        var builder = new TestContextBuilder()
            .ConTexto("texto invalido")
            .ConEstado(EstadoConversacion.ActualizarDatosBancarios);

        var ctx = builder.Build();
        var result = await _sut.HandleAsync(ctx, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Procesado);
        // Verificar que no se llamo a la API
        builder.MockReintegros.Verify(
            r => r.ActualizarDatosBancariosAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ComandoAtras_VuelveAReintegroMenu()
    {
        var builder = new TestContextBuilder()
            .ConTexto("atras")
            .ConEstado(EstadoConversacion.ActualizarDatosBancarios);

        var ctx = builder.Build();
        var result = await _sut.HandleAsync(ctx, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(EstadoConversacion.ReintegroMenu, ctx.Sesion.Estado);
    }
}
```

### Resumen: Archivos tocados para un nuevo estado

| # | Archivo | Cambio |
|---|---------|--------|
| 1 | `src/Dominio/Entidades/EstadoConversacion.cs` | Agregar valor al enum |
| 2 | `src/Aplicacion/Servicios/StateHandlers/NuevoHandler.cs` | **Crear** archivo nuevo con el handler |
| 3 | `src/Aplicacion/Servicios/StateHandlers/StateDispatcher.cs` | Registrar el handler en la lista |
| 4 | `src/Aplicacion/Servicios/StateHandlers/ReintegroMenuHandler.cs` | Agregar transicion desde el menu (o desde el handler que lleve al nuevo estado) |
| 5 | `src/Aplicacion/Servicios/BotMessages.cs` | Agregar mensajes en ES/EN/PT |
| 6 | `src/Aplicacion/Servicios/OrquestadorConversacion.cs` | Agregar estado al grupo de dispatch correspondiente |
| 7 | `tests/StateHandlers/NuevoHandlerTests.cs` | **Crear** archivo de tests |

---

## 8. Como agregar mensajes en los 3 idiomas

Todos los textos del bot estan centralizados en `BotMessages.cs`. El patron es siempre el mismo:

```csharp
public static string NombreDelMensaje(string locale)
    => LocaleHelper.Loc(locale,
        es: "Texto en espanol",
        en: "Text in English",
        pt: "Texto em portugues");
```

Para listas de opciones:

```csharp
public static List<string> OpcionesDelMenu(string locale)
    => LocaleHelper.Loc(locale,
        es: new List<string> { "Opcion 1", "Opcion 2" },
        en: new List<string> { "Option 1", "Option 2" },
        pt: new List<string> { "Opcao 1", "Opcao 2" });
```

`LocaleHelper.Loc()` normaliza cualquier locale a `"es"`, `"en"` o `"pt"` (default: `"es"`).

---

## 9. Como escribir tests

### Herramienta principal: TestContextBuilder

El proyecto provee un builder fluido en `tests/Helpers/TestContextBuilder.cs` que facilita la creacion de contextos de prueba:

```csharp
var builder = new TestContextBuilder()
    .ConTexto("hola")
    .ConTelefono("+5491112345678")
    .ConLocale("es")
    .ConEstado(EstadoConversacion.MenuPrincipal)
    .ConBenefitId("12345");

// Acceso a los mocks para configurar comportamiento:
builder.MockReintegros
    .Setup(r => r.BuscarPorIdentificadorAsync(It.IsAny<ConsultaIdentificador>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new List<ResumenReintegro> { /* ... */ });

var ctx = builder.Build();
var result = await handler.HandleAsync(ctx, CancellationToken.None);

// Verificar efectos secundarios:
Assert.Single(builder.SesionesGuardadas);
Assert.Equal(EstadoConversacion.ReintegroMenu, builder.SesionesGuardadas[0].Estado);
```

### Patron de test

Cada test sigue: **Arrange** (builder + mocks) -> **Act** (HandleAsync) -> **Assert** (resultado + efectos).

### Ejecutar tests

```bash
cd servicio-reintegros
dotnet test ./tests/ServicioReintegros.Tests.csproj
```

---

## 10. Convenciones del proyecto

### Idioma
- Todo el codigo (clases, metodos, variables, comentarios) esta en **espanol**.
- Los mensajes al usuario estan en 3 idiomas (ES/EN/PT) centralizados en `BotMessages.cs`.

### Nomenclatura
- **Clases/Metodos/Propiedades**: PascalCase (`OrquestadorConversacion`, `ProcesarEventoAsync`)
- **Variables locales**: camelCase (`texto`, `sesion`, `reintegro`)
- **Interfaces**: Prefijo `I` (`IProveedorIA`, `ICacheAplicacion`)
- **Opciones de configuracion**: Sufijo `Opciones` (`WabaOpciones`, `FoundryOpciones`)
- **Adapters**: Sufijo `Adapter` (`MetaCloudAdapter`, `RedisAdapter`)
- **Handlers**: Sufijo `Handler` (`AwaitingNameHandler`)
- **Tests**: Sufijo `Tests` (`AwaitingNameHandlerTests`)

### Patron de archivos
- Un handler por archivo (excepcion actual: `PendingDocsHandler.cs` tiene 3)
- Un archivo de test por handler
- Servicios auxiliares en archivos separados

### Dependencias
- Se usan **Puertos** (interfaces) para toda comunicacion con servicios externos
- Los handlers NO conocen al `OrquestadorConversacion`; se comunican via delegates en el `StateHandlerContext`
- Los handlers NO acceden directamente a Redis, SQL o WhatsApp

---

## 11. Notas tecnicas importantes

### El orquestador tiene dos grupos de dispatch

`OrquestadorConversacion.ProcesarEventoAsync` llama al `StateDispatcher` en **dos puntos distintos** del flujo, con filtros de estado diferentes. Si se agrega un nuevo estado, hay que decidir en cual grupo ponerlo:

- **Grupo 1** (antes de comandos de estado/pagos): `AwaitingName`, `MenuPrincipal`, `OtrasConsultasMenu`, `OtrasConsultasProblem`, `AwaitingIdentifier`
- **Grupo 2** (despues): `SelectingReintegro`, `ReintegroMenu`, `ReintegroFinancialMenu`, `ReintegroPaymentsMenu`, `PendingDocs*`, `ReintegroProblem`, `ReintegroExit`

**Regla practica**: si el nuevo estado es un sub-flujo de un reintegro ya seleccionado, va en el Grupo 2. Si es un flujo independiente (como el menu principal o identificacion), va en el Grupo 1.

### El StateDispatcher NO esta en DI

Los handlers se instancian con `new` dentro de `StateDispatcher`, no se resuelven por inyeccion de dependencias. Esto significa que los handlers no pueden recibir servicios por constructor; todo lo que necesitan viene en el `StateHandlerContext`.

### Cache como fuente de verdad para sesiones

Las sesiones se guardan en Redis (o en memoria si Redis no esta configurado). No se usa SQL para sesiones activas. Si Redis se reinicia, las sesiones en curso se pierden.

### Claves de cache

| Patron | Contenido |
|--------|-----------|
| `sess:{telefono}` | Estado de la conversacion (JSON) |
| `locale:{telefono}` | Idioma detectado |
| `reintegro:actual:{telefono}` | Reintegro que se esta consultando |
| `reintegros:lista:{telefono}` | Lista de resultados de busqueda |
| `hist:{telefono}` | Historial de mensajes recientes |
| `wa:msg:{msgId}` | Deduplicacion de mensajes |
