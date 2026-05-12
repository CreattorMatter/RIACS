# Servicio de Reintegros - AssistCard

Agente conversacional inteligente para WhatsApp que permite a los clientes de AssistCard gestionar consultas de reintegros (reembolsos) de forma automatizada, con soporte multilenguaje (ES/EN/PT).

## Arquitectura

- **Lenguaje:** C# / .NET 8
- **Framework:** ASP.NET Core 8 + FastEndpoints
- **Patrón:** Clean Architecture (Dominio / Aplicacion / Infraestructura / Interfaces)
- **IA:** Azure AI Search + Azure Foundry Responses sin agente/MCP en runtime
- **Canal:** WhatsApp Business API (Meta Cloud)
- **Cache:** Azure Cache for Redis
- **Base de datos:** Azure SQL Server (EF Core)
- **Almacenamiento:** Azure Blob Storage
- **Traduccion:** Azure Translator
- **Observabilidad:** Application Insights + Prometheus

## Estructura del proyecto

```
servicio-reintegros/
  src/
    Aplicacion/          Orquestacion, state handlers, DTOs, puertos (interfaces)
    Dominio/             Entidades y logica de negocio
    Infraestructura/     Adaptadores (IA, WhatsApp, Redis, SQL, Blob, Translator)
    Interfaces/          Endpoints HTTP (FastEndpoints)
    Configuracion/       Registro de servicios y opciones
    Program.cs           Punto de entrada
  tests/                 Tests unitarios (xUnit)
  build/Dockerfile       Imagen Docker multi-stage
  appsettings.json       Configuracion base (sin secretos)
  appsettings.Example.json Ejemplo seguro con placeholders
azure-pipelines.yml      Pipeline CI/CD (Azure DevOps)
```

## Endpoints

| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| GET | `/webhook/whatsapp` | Verificacion del webhook de Meta |
| POST | `/webhook/whatsapp` | Recepcion de mensajes WhatsApp |
| POST | `/api/chatbot/Agent/Reintegros` | Integracion con plataforma SAMU/Genesys |
| GET | `/healthz` | Health check |
| GET | `/readyz` | Readiness check |
| GET | `/metrics` | Metricas Prometheus |
| GET | `/swagger` | Documentacion de la API |

## Ejecucion local

### Requisitos previos
- .NET 8 SDK
- (Opcional) Redis local o Docker
- Variables de entorno o .NET User Secrets configurados (ver `GUIA_DE_CONFIGURACION.md`)

### Pasos

```bash
cd servicio-reintegros
dotnet restore
dotnet build
dotnet run
```

La aplicacion se ejecuta en `http://localhost:8080`.

### Con Docker

```bash
cd servicio-reintegros
docker build -f ./build/Dockerfile -t servicio-reintegros:latest .
docker run -p 8080:8080 --env-file <archivo-env-local-no-versionado> servicio-reintegros:latest
```

## Tests

```bash
cd servicio-reintegros
dotnet test ./tests/ServicioReintegros.Tests.csproj
```

## Configuracion

Consultar la **[Guia de Configuracion](GUIA_DE_CONFIGURACION.md)** para el paso a paso completo de como configurar todos los servicios externos y desplegar la aplicacion.

## Coleccion Postman

Importar `servicio-reintegros/POSTMAN_COLLECTION.json` para probar los endpoints.
