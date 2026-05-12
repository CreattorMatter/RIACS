# Guia de Configuracion - Bot de Reintegros AssistCard

Guia paso a paso para configurar, desplegar y poner en funcionamiento el bot de reintegros de AssistCard.

---

## Indice

1. [Requisitos previos](#1-requisitos-previos)
2. [Variables de entorno](#2-variables-de-entorno)
3. [WhatsApp Business API (Meta Cloud)](#3-whatsapp-business-api-meta-cloud)
4. [Azure Foundry Responses y Azure AI Search](#4-azure-foundry-responses-y-azure-ai-search)
5. [Azure Translator](#5-azure-translator)
6. [Azure Cache for Redis](#6-azure-cache-for-redis)
7. [Azure SQL Database](#7-azure-sql-database)
8. [Azure Blob Storage](#8-azure-blob-storage)
9. [Azure Application Insights](#9-azure-application-insights)
10. [Azure Key Vault (secretos)](#10-azure-key-vault-secretos)
11. [API de Reintegros AssistCard](#11-api-de-reintegros-assistcard)
12. [Logger de Conversaciones (SAMU)](#12-logger-de-conversaciones-samu)
13. [Ejecucion local](#13-ejecucion-local)
14. [Despliegue en Azure App Service](#14-despliegue-en-azure-app-service)
15. [Despliegue con Docker](#15-despliegue-con-docker)
16. [Pipeline CI/CD (Azure DevOps)](#16-pipeline-cicd-azure-devops)
17. [Verificacion y pruebas](#17-verificacion-y-pruebas)
18. [Checklist de produccion](#18-checklist-de-produccion)
19. [Solucion de problemas](#19-solucion-de-problemas)

---

## 1. Requisitos previos

Antes de comenzar, asegurese de contar con:

- **.NET 8 SDK** instalado (descargar de https://dotnet.microsoft.com/download/dotnet/8.0)
- **Suscripcion de Azure** activa con permisos para crear recursos
- **Cuenta de Meta Business** verificada (para WhatsApp Business API)
- **Git** instalado
- (Opcional) **Docker** instalado para despliegue en contenedores
- (Opcional) **Azure CLI** (`az`) para gestion de recursos desde terminal

### Clonar el repositorio

```bash
git clone <URL_DEL_REPOSITORIO>
cd BotAI
```

---

## 2. Variables de entorno

La aplicacion lee toda su configuracion desde **variables de entorno**, `appsettings.json` y, para desarrollo local, .NET User Secrets. A continuacion se listan todas las variables necesarias.

Para desarrollo local, no guardar secretos reales en archivos versionables. Use .NET User Secrets, variables de entorno del sistema o un archivo local ignorado como `appsettings.Local.json`.

### Tabla completa de variables

| Variable | Obligatoria | Descripcion | Ejemplo |
|----------|:-----------:|-------------|---------|
| **WABA_ACCESS_TOKEN** | Si | Token de acceso de WhatsApp Business API | `<whatsapp-access-token>` |
| **WABA_APP_SECRET** | Si | App Secret de la aplicacion Meta | `<meta-app-secret>` |
| **WABA_PHONE_NUMBER_ID** | Si | ID del numero de telefono de WhatsApp | `836221682917058` |
| **WABA_BUSINESS_ACCOUNT_ID** | Si | ID de la cuenta de WhatsApp Business | `1923339785272742` |
| **WABA_VERIFY_TOKEN** | Si | Token personalizado para verificar el webhook | `mi-token-secreto` |
| **WABA_API_BASE** | No | URL base de la API de Meta Graph | `https://graph.facebook.com/v24.0/` |
| **WABA_ENABLE_ARGENTINA_FALLBACK** | No | Habilitar ajuste de numeros argentinos | `true` |
| **FOUNDRY_ENDPOINT** | Si | URL del proyecto en Azure AI Foundry | `https://<recurso>.services.ai.azure.com/api/projects/<proyecto>` |
| **FOUNDRY_MODEL_DEPLOYMENT** | Si | Nombre del deployment del modelo usado por `/openai/v1/responses` | `gpt-4o` |
| **FOUNDRY_API_KEY** | No | API Key Foundry solo para desarrollo/contingencia; en produccion se prefiere Entra ID | `<foundry-api-key>` |
| **FOUNDRY_DEPLOYMENT** | No | Fallback temporal si falta `FOUNDRY_MODEL_DEPLOYMENT`; no debe ser el agente en runtime | `<model-deployment-name>` |
| **SEARCH_ENDPOINT** | Si | Endpoint del servicio Azure AI Search | `https://<search-service>.search.windows.net` |
| **SEARCH_KNOWLEDGE_BASE_NAME** | Si | Nombre de la Knowledge Base usada por `retrieve` | `<knowledge-base-name>` |
| **SEARCH_INDEX_NAME** | Si | Nombre del indice usado como fallback documental | `<search-index-name>` |
| **SEARCH_API_KEY** | No | API key de Search solo para desarrollo/contingencia; en produccion dejar vacia y usar RBAC | `<search-api-key>` |
| **AZURE_TRANSLATOR_ENDPOINT** | Si | URL del servicio Azure Translator | `https://api.cognitive.microsofttranslator.com/` |
| **AZURE_TRANSLATOR_KEY** | Si | Clave de Azure Translator | `<translator-key>` |
| **AZURE_TRANSLATOR_REGION** | Si | Region del recurso Translator | `eastus2` |
| **REDIS_URL** | No* | Cadena de conexion de Azure Cache for Redis | `<host>:10000,password=<key>,ssl=true` |
| **DATABASE_URL** | No* | Cadena de conexion de Azure SQL Server | `Server=tcp:<server>.database.windows.net;...` |
| **AZURE_STORAGE_CONNECTION_STRING** | No* | Cadena de conexion de Azure Blob Storage | `DefaultEndpointsProtocol=https;AccountName=...` |
| **AZURE_BLOB_CONTAINER** | No | Nombre del contenedor de blobs | `wa-media` (por defecto) |
| **Reintegros:BaseUrl** | Si | URL base de la API de reintegros | `https://samumiddlewareqa.assistcard.com/` |
| **Reintegros:ApiKey** | Si | API Key para la API de reintegros | `<reintegros-api-key>` |
| **ConversacionesLogger:BaseUrl** | No | URL del servicio de logging de conversaciones | `https://.../api/chatbot/Agent/reimbursement/log` |
| **ConversacionesLogger:ApiKey** | No | API Key del servicio de logging | `<logger-api-key>` |
| **ApplicationInsights:ConnectionString** | No | Cadena de conexion de Application Insights | `InstrumentationKey=...` |

> *\* Si REDIS_URL no se configura, la aplicacion usa cache en memoria (no recomendado para produccion). DATABASE_URL y AZURE_STORAGE_CONNECTION_STRING son opcionales si no se necesita persistencia SQL o almacenamiento de archivos.*

---

## 3. WhatsApp Business API (Meta Cloud)

### 3.1 Crear la aplicacion en Meta

1. Ir a [Meta for Developers](https://developers.facebook.com/) e iniciar sesion.
2. Crear una nueva aplicacion de tipo **Business**.
3. En el dashboard de la app, agregar el producto **WhatsApp**.
4. Asociar una **cuenta de WhatsApp Business (WABA)** existente o crear una nueva.

### 3.2 Obtener credenciales

Desde el panel de la aplicacion en Meta:

- **WABA_ACCESS_TOKEN**: En *WhatsApp > Configuracion de la API* generar un token de acceso permanente (System User Token). El token temporal caduca en 24h y no sirve para produccion.
- **WABA_APP_SECRET**: En *Configuracion > Basica* de la aplicacion Meta.
- **WABA_PHONE_NUMBER_ID**: En *WhatsApp > Configuracion de la API*, es el ID numerico del numero de telefono.
- **WABA_BUSINESS_ACCOUNT_ID**: En *WhatsApp > Configuracion de la API*, es el ID de la cuenta business.

### 3.3 Configurar el webhook

1. En *WhatsApp > Configuracion*, buscar la seccion **Webhook**.
2. Configurar:
   - **URL de callback**: `https://<SU_DOMINIO>/webhook/whatsapp`
   - **Token de verificacion**: El valor que elija para `WABA_VERIFY_TOKEN` (debe coincidir exactamente con la variable de entorno del servicio).
3. Hacer clic en **Verificar y guardar**. Meta enviara un `GET` al endpoint y esperara recibir el `hub.challenge`.
4. Suscribirse al campo **messages** para recibir mensajes entrantes.

### 3.4 Permisos necesarios

El System User que genera el token debe tener estos permisos:
- `whatsapp_business_messaging`
- `whatsapp_business_management`

### 3.5 Plantillas de mensajes

Para enviar mensajes fuera de la ventana de 24 horas, debe crear plantillas aprobadas:

1. Ir a *WhatsApp > Administrador de cuentas > Plantillas de mensajes*.
2. Crear plantillas en los 3 idiomas (es, en, pt) para:
   - Bienvenida / saludo inicial
   - Solicitud de identificador
   - Resumen de beneficio
3. Enviar a aprobacion (Meta revisa en 24-48h).

### 3.6 Verificacion de firma HMAC

El servicio verifica automaticamente la firma `X-Hub-Signature-256` en cada POST recibido, usando `WABA_APP_SECRET`. Esto garantiza que los mensajes provienen de Meta.

---

## 4. Azure Foundry Responses y Azure AI Search

El runtime productivo no invoca un agente de Foundry ni herramientas MCP. El backend consulta Azure AI Search para conocimiento documental y, cuando necesita redactar o razonar, llama directamente a Foundry Responses (`/openai/v1/responses`) con `model` e `input`.

### 4.1 Crear un proyecto en Azure AI Foundry

1. Ir a [Azure AI Foundry](https://ai.azure.com/) e iniciar sesion.
2. Crear un **nuevo proyecto** (o usar uno existente).
3. Anotar la URL del proyecto: sera el valor de `FOUNDRY_ENDPOINT`.
   - Formato tipico: `https://<recurso>.services.ai.azure.com/api/projects/<proyecto>`

### 4.2 Desplegar un modelo

1. Dentro del proyecto, ir a **Deployments** (Implementaciones).
2. Desplegar el modelo **GPT-4o** (o el modelo que desee usar).
3. Configurar los limites de tokens y tasa de solicitudes segun su volumen esperado.

### 4.3 Configurar Azure AI Search

1. En Azure Portal, abrir el recurso **Azure AI Search** que contiene la base de conocimiento.
2. Confirmar estos valores:
   - **SEARCH_ENDPOINT**: URL del servicio Search, con formato `https://<search-service>.search.windows.net`.
   - **SEARCH_KNOWLEDGE_BASE_NAME**: nombre exacto de la Knowledge Base.
   - **SEARCH_INDEX_NAME**: nombre exacto del indice que contiene los documentos.
3. En produccion, asignar al App Registration/identidad del IIS el rol **Search Index Data Reader** sobre el recurso Search.
4. Dejar `SEARCH_API_KEY` vacia para usar Entra ID/RBAC. Usar `SEARCH_API_KEY` solo para desarrollo o contingencia.
5. Corregir Semantic Search/Knowledge Base para que `knowledgebases/{name}/retrieve` funcione como camino primario. El codigo mantiene busqueda directa al indice como fallback.

### 4.4 Obtener credenciales y variables

- **FOUNDRY_ENDPOINT**: URL del proyecto Foundry (paso 4.1).
- **FOUNDRY_MODEL_DEPLOYMENT**: nombre del deployment del modelo, por ejemplo `gpt-4o`. No es el nombre del agente.
- **AZURE_TENANT_ID**, **AZURE_CLIENT_ID**, **AZURE_CLIENT_SECRET**: credenciales del App Registration usado por IIS para `DefaultAzureCredential`.
- **SEARCH_ENDPOINT**, **SEARCH_KNOWLEDGE_BASE_NAME**, **SEARCH_INDEX_NAME**: valores del recurso Search.
- **FOUNDRY_API_KEY** y **SEARCH_API_KEY**: opcionales; preferir dejarlas vacias en produccion.

> **Nota**: El agente de Foundry puede quedar publicado para pruebas manuales, pero el backend no lo invoca en runtime. No se requieren `agent_reference`, tools MCP ni aprobaciones MCP.

---

## 5. Azure Translator

Usado para detectar automaticamente el idioma del usuario y traducir respuestas.

### 5.1 Crear el recurso

1. En [Azure Portal](https://portal.azure.com/), buscar **Translator**.
2. Crear un nuevo recurso de tipo **Translator**.
3. Seleccionar la region (ej: `eastus2`) y el plan de precios (F0 gratuito para pruebas, S1 para produccion).

### 5.2 Obtener credenciales

En el recurso creado, ir a *Keys and Endpoint*:

- **AZURE_TRANSLATOR_ENDPOINT**: `https://api.cognitive.microsofttranslator.com/`
- **AZURE_TRANSLATOR_KEY**: Copiar Key 1 o Key 2
- **AZURE_TRANSLATOR_REGION**: La region seleccionada (ej: `eastus2`)

### 5.3 Idiomas soportados

El servicio detecta y traduce automaticamente entre:
- Espanol (es)
- Ingles (en)
- Portugues (pt)

---

## 6. Azure Cache for Redis

Usado para almacenar sesiones de conversacion, historial de mensajes, deduplicacion y locale del usuario.

### 6.1 Crear la instancia

1. En Azure Portal, buscar **Azure Cache for Redis** (o **Azure Managed Redis**).
2. Crear una nueva instancia:
   - **SKU**: Basic (pruebas) o Standard/Premium (produccion)
   - **TLS**: Habilitado (recomendado)
   - **Puerto**: 6380 (TLS) o 10000 (Azure Managed Redis)

### 6.2 Obtener la cadena de conexion

En el recurso, ir a *Access keys*:

- **REDIS_URL**: `<hostname>:<puerto>,password=<clave>,ssl=true`

Ejemplo: `mibot-redis.redis.cache.windows.net:6380,password=abc123...,ssl=true`

### 6.3 Claves Redis usadas por la aplicacion

| Patron de clave | Contenido | TTL |
|-----------------|-----------|-----|
| `sess:{telefono}` | Estado de la conversacion (JSON) | 30 min |
| `locale:{telefono}` | Idioma detectado del usuario | 24 h |
| `reintegro:actual:{telefono}` | Reintegro que esta consultando | 30 min |
| `reintegros:lista:{telefono}` | Lista de resultados de busqueda | 30 min |
| `hist:{telefono}` | Historial de mensajes recientes | 30 min |

> Si no configura Redis, la aplicacion funciona con cache en memoria. Esto es util para desarrollo pero **no se recomienda para produccion** porque el estado se pierde al reiniciar la aplicacion.

---

## 7. Azure SQL Database

Usado para persistencia opcional de sesiones y cache de reintegros.

### 7.1 Crear el servidor y la base de datos

1. En Azure Portal, buscar **SQL databases**.
2. Crear un nuevo servidor SQL:
   - Configurar administrador y contrasena.
   - Habilitar **Microsoft Entra authentication** (recomendado).
3. Crear una base de datos en ese servidor.

### 7.2 Configurar el firewall

- Agregar la IP de su maquina local para desarrollo.
- En produccion, habilitar **"Allow Azure services"** o usar Private Endpoint.

### 7.3 Obtener la cadena de conexion

En el recurso de la base de datos, ir a *Connection strings*:

- **DATABASE_URL**: `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;Persist Security Info=False;User ID=<user>;Password=<pass>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`

### 7.4 Ejecutar migraciones

```bash
cd servicio-reintegros
dotnet ef database update
```

Esto crea las tablas `SesionesUsuario` y `CacheReintegros` automaticamente.

> Este componente es **opcional**. La aplicacion funciona sin SQL si se usa solo Redis para sesiones.

---

## 8. Azure Blob Storage

Usado para almacenar archivos (fotos, PDFs) que los usuarios envian por WhatsApp.

### 8.1 Crear la cuenta de almacenamiento

1. En Azure Portal, buscar **Storage accounts**.
2. Crear una nueva cuenta:
   - **Performance**: Standard
   - **Redundancy**: LRS (pruebas) o GRS (produccion)
   - **Public access**: Deshabilitado

### 8.2 Crear el contenedor

1. Dentro de la cuenta, ir a **Containers**.
2. Crear un contenedor llamado `wa-media` con acceso **Private**.

### 8.3 Obtener la cadena de conexion

En la cuenta de almacenamiento, ir a *Access keys*:

- **AZURE_STORAGE_CONNECTION_STRING**: Copiar la cadena completa.
- **AZURE_BLOB_CONTAINER**: `wa-media` (o el nombre que elija)

---

## 9. Azure Application Insights

Usado para logs, trazas, metricas y monitoreo de la aplicacion.

### 9.1 Crear el recurso

1. En Azure Portal, buscar **Application Insights**.
2. Crear un nuevo recurso vinculado a un Log Analytics Workspace.

### 9.2 Obtener la cadena de conexion

En el recurso, ir a *Overview*:

- **ApplicationInsights:ConnectionString**: Copiar la cadena de conexion (formato: `InstrumentationKey=...;IngestionEndpoint=...`).

### 9.3 Que se monitorea

- Requests HTTP (latencia, errores, codigos de estado)
- Excepciones y trazas de error
- Dependencias externas (Redis, SQL, APIs)
- Metricas custom (mensajes procesados, tokens IA, etc.)
- Live Metrics (monitoreo en tiempo real)

---

## 10. Azure Key Vault (secretos)

Recomendado para produccion para no almacenar secretos en texto plano.

### 10.1 Crear el Key Vault

1. En Azure Portal, buscar **Key vaults**.
2. Crear un vault nuevo.

### 10.2 Cargar los secretos

Cargar cada variable de entorno como secreto individual:

```bash
az keyvault secret set --vault-name <nombre-vault> --name "WABA-ACCESS-TOKEN" --value "<valor>"
az keyvault secret set --vault-name <nombre-vault> --name "WABA-APP-SECRET" --value "<valor>"
az keyvault secret set --vault-name <nombre-vault> --name "FOUNDRY-API-KEY" --value "<valor>"
# ... repetir para cada secreto
```

> **Nota**: Los nombres de secretos en Key Vault usan guiones (-) en lugar de guiones bajos (_). Azure App Service los convierte automaticamente.

### 10.3 Configurar acceso desde App Service

1. En el App Service, habilitar **System assigned managed identity** (en *Identity*).
2. En el Key Vault, ir a *Access policies* o *RBAC* y otorgar permiso **Key Vault Secrets User** a la identidad del App Service.
3. En el App Service, en *Configuration*, referenciar secretos con la sintaxis:
   ```
   @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<nombre-secreto>/)
   ```

---

## 11. API de Reintegros AssistCard

El servicio consume la API interna de AssistCard para consultar y gestionar reintegros.

### 11.1 Configuracion

- **Reintegros:BaseUrl**: URL base de la API. Ejemplo:
  - QA: `https://samumiddlewareqa.assistcard.com/`
  - Produccion: `https://samumiddleware.assistcard.com/` (confirmar con el equipo)
- **Reintegros:ApiKey**: Clave proporcionada por el equipo de AssistCard para autenticacion.

### 11.2 Endpoints consumidos

| Operacion | Metodo | Ruta | Headers |
|-----------|--------|------|---------|
| Buscar reintegros | POST | `/api/chatbot/Agent/Reimbursement` | `X-API-Key` |
| Agregar documentos | POST | `/api/chatbot/Agent/Reimbursement/documents` | `X-API-Key` |
| Actualizar datos bancarios | POST | `/api/chatbot/Agent/Reimbursement/bank-data` | `X-API-Key` |

### 11.3 Tipos de busqueda soportados

Se puede buscar un reintegro por:
- **benefitRequestId** (numerico, 5-8 digitos)
- **caseId** (alfanumerico, 7 caracteres)
- **email** del cliente
- **voucherCode** (codigo ISO + numeros)
- **nationalId + countryIsoCode** (documento + pais)

---

## 12. Logger de Conversaciones (SAMU)

Servicio opcional para registrar las conversaciones para auditoria y compliance.

- **ConversacionesLogger:BaseUrl**: URL del endpoint de logging
- **ConversacionesLogger:ApiKey**: Clave de autenticacion

> Si no se configura, las conversaciones se registran solo en Application Insights.

---

## 13. Ejecucion local

### 13.1 Configurar variables

Opcion A - .NET User Secrets:
```bash
cd servicio-reintegros
dotnet user-secrets set "FOUNDRY_ENDPOINT" "https://<recurso>.services.ai.azure.com/api/projects/<proyecto>"
dotnet user-secrets set "FOUNDRY_MODEL_DEPLOYMENT" "<model-deployment-name>"
dotnet user-secrets set "SEARCH_ENDPOINT" "https://<search-service>.search.windows.net"
dotnet user-secrets set "SEARCH_KNOWLEDGE_BASE_NAME" "<knowledge-base-name>"
dotnet user-secrets set "SEARCH_INDEX_NAME" "<search-index-name>"
dotnet user-secrets set "AZURE_TENANT_ID" "<tenant-id>"
dotnet user-secrets set "AZURE_CLIENT_ID" "<application-client-id>"
dotnet user-secrets set "AZURE_CLIENT_SECRET" "<client-secret-value>"
```

Opcion B - Archivo local ignorado `appsettings.Local.json` o variables de entorno:
```json
{
  "FOUNDRY_ENDPOINT": "https://<recurso>.services.ai.azure.com/api/projects/<proyecto>",
  "FOUNDRY_MODEL_DEPLOYMENT": "<model-deployment-name>",
  "SEARCH_ENDPOINT": "https://<search-service>.search.windows.net",
  "SEARCH_KNOWLEDGE_BASE_NAME": "<knowledge-base-name>",
  "SEARCH_INDEX_NAME": "<search-index-name>",
  "WABA_ACCESS_TOKEN": "<whatsapp-access-token>",
  "WABA_APP_SECRET": "<meta-app-secret>",
  "WABA_PHONE_NUMBER_ID": "<phone-number-id>",
  "WABA_BUSINESS_ACCOUNT_ID": "<business-account-id>",
  "WABA_VERIFY_TOKEN": "<webhook-verify-token>",
  "AZURE_TRANSLATOR_ENDPOINT": "https://api.cognitive.microsofttranslator.com/",
  "AZURE_TRANSLATOR_KEY": "<translator-key>",
  "AZURE_TRANSLATOR_REGION": "<region>",
  "Reintegros": {
    "BaseUrl": "https://<reintegros-api-host>/",
    "ApiKey": "<reintegros-api-key>"
  }
}
```

`SEARCH_API_KEY` y `FOUNDRY_API_KEY` son opcionales. En produccion se recomienda dejarlas vacias y autenticar con `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` y `AZURE_CLIENT_SECRET`.

### 13.2 Compilar y ejecutar

```bash
cd servicio-reintegros
dotnet restore
dotnet build
dotnet run
```

La aplicacion se inicia en `http://localhost:8080`.

### 13.3 Verificar funcionamiento

- Abrir `http://localhost:8080/swagger` para ver la documentacion de la API
- Probar `http://localhost:8080/healthz` (debe responder `Healthy`)
- Probar `http://localhost:8080/readyz` (debe responder `Healthy`)
- Probar `http://localhost:8080/metrics` (metricas Prometheus)

### 13.4 Exponer el servicio local para pruebas WhatsApp

Para recibir webhooks de Meta en desarrollo local, use un tunel como **ngrok**:

```bash
ngrok http 8080
```

Use la URL generada (ej: `https://abc123.ngrok.io`) como URL de callback del webhook en Meta.

---

## 14. Despliegue en Azure App Service

### 14.1 Crear el App Service

1. En Azure Portal, buscar **App Services**.
2. Crear un nuevo Web App:
   - **Runtime**: .NET 8 (Linux)
   - **Plan**: B1 minimo (produccion: S1 o superior)
   - **Region**: La misma que sus otros recursos Azure

### 14.2 Configurar ajustes

En *Configuration > Application settings*, agregar todas las variables de entorno de la [Seccion 2](#2-variables-de-entorno).

En *Configuration > General settings*:
- **HTTPS Only**: Si
- **Always On**: Si
- **HTTP version**: 2.0

### 14.3 Configurar health checks

En *Monitoring > Health check*:
- **Path**: `/healthz`
- **Interval**: 60 segundos

### 14.4 Desplegar el codigo

Opcion A - Desde Azure DevOps (ver [Seccion 16](#16-pipeline-cicd-azure-devops)).

Opcion B - Manualmente:
```bash
cd servicio-reintegros
dotnet publish -c Release -o ./publish
cd publish
zip -r app.zip .
az webapp deploy --resource-group <grupo> --name <nombre-webapp> --src-path app.zip
```

### 14.5 Registrar la URL como webhook

Una vez desplegado, copie la URL del App Service (ej: `https://servicio-reintegros.azurewebsites.net`) y configurela como URL de callback del webhook en Meta (ver [Seccion 3.3](#33-configurar-el-webhook)).

---

## 15. Despliegue con Docker

### 15.1 Construir la imagen

```bash
cd servicio-reintegros
docker build -f ./build/Dockerfile -t servicio-reintegros:latest .
```

### 15.2 Ejecutar el contenedor

```bash
docker run -d \
  -p 8080:8080 \
  -e FOUNDRY_ENDPOINT="..." \
  -e FOUNDRY_MODEL_DEPLOYMENT="..." \
  -e SEARCH_ENDPOINT="..." \
  -e SEARCH_KNOWLEDGE_BASE_NAME="..." \
  -e SEARCH_INDEX_NAME="..." \
  -e AZURE_TENANT_ID="..." \
  -e AZURE_CLIENT_ID="..." \
  -e AZURE_CLIENT_SECRET="..." \
  -e WABA_ACCESS_TOKEN="..." \
  -e WABA_APP_SECRET="..." \
  -e WABA_PHONE_NUMBER_ID="..." \
  -e WABA_BUSINESS_ACCOUNT_ID="..." \
  -e WABA_VERIFY_TOKEN="..." \
  -e AZURE_TRANSLATOR_ENDPOINT="..." \
  -e AZURE_TRANSLATOR_KEY="..." \
  -e AZURE_TRANSLATOR_REGION="..." \
  -e REDIS_URL="..." \
  -e Reintegros__BaseUrl="..." \
  -e Reintegros__ApiKey="..." \
  servicio-reintegros:latest
```

> Para secciones de configuracion anidadas (como `Reintegros:BaseUrl`), usar doble guion bajo `__` como separador en variables de entorno: `Reintegros__BaseUrl`.

### 15.3 Desplegar en Azure Container Apps (alternativa)

```bash
az containerapp create \
  --name servicio-reintegros \
  --resource-group <grupo> \
  --image servicio-reintegros:latest \
  --target-port 8080 \
  --ingress external
```

---

## 16. Pipeline CI/CD (Azure DevOps)

El repositorio incluye `azure-pipelines.yml` con 3 etapas automaticas.

### 16.1 Configurar el pipeline

1. En Azure DevOps, crear un nuevo pipeline apuntando al repositorio.
2. Seleccionar el archivo `azure-pipelines.yml` existente.
3. Configurar las **Service Connections**:
   - `sc-smartsolutions-dev` (o el nombre que use) hacia la suscripcion Azure donde esta el App Service.

### 16.2 Etapas del pipeline

| Etapa | Que hace |
|-------|----------|
| **Build** | Restaura dependencias, compila y empaqueta en `app.zip` |
| **Test** | Ejecuta los tests unitarios (xUnit) y publica resultados |
| **Deploy** | Despliega `app.zip` en el Azure App Service configurado |

### 16.3 Ejecucion

- **Automatica**: Se ejecuta al hacer push a la rama `main`.
- **Manual**: Desde Azure DevOps se puede ejecutar manualmente seleccionando rama y ambiente.

---

## 17. Verificacion y pruebas

### 17.1 Verificacion rapida

Despues de desplegar, verificar estos endpoints:

```bash
# Health check
curl https://<SU_DOMINIO>/healthz

# Readiness
curl https://<SU_DOMINIO>/readyz

# Metricas
curl https://<SU_DOMINIO>/metrics

# Swagger (navegador)
https://<SU_DOMINIO>/swagger
```

### 17.2 Prueba del webhook (verificacion GET)

```bash
curl "https://<SU_DOMINIO>/webhook/whatsapp?hub.mode=subscribe&hub.challenge=test123&hub.verify_token=<SU_VERIFY_TOKEN>"
```

Debe responder: `test123`

### 17.3 Coleccion Postman

Importar `servicio-reintegros/POSTMAN_COLLECTION.json` en Postman para ejecutar pruebas completas de todos los endpoints.

### 17.4 Prueba completa del flujo

1. Enviar un mensaje de WhatsApp al numero configurado.
2. El bot debe responder solicitando el nombre.
3. Enviar un nombre y luego un identificador de reintegro valido.
4. Verificar que el bot muestra el resumen del reintegro y las opciones del menu.

---

## 18. Checklist de produccion

Antes de dar por operativo el servicio, verifique:

### Servicios Azure
- [ ] Azure Foundry Responses: `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_DEPLOYMENT` y credenciales Entra configuradas.
- [ ] Azure AI Search: `SEARCH_ENDPOINT`, `SEARCH_KNOWLEDGE_BASE_NAME`, `SEARCH_INDEX_NAME` y rol `Search Index Data Reader` asignado al App Registration.
- [ ] Azure Translator: claves configuradas, deteccion de idioma funcionando.
- [ ] Azure Cache for Redis: conexion TLS activa, expiraciones definidas.
- [ ] Azure SQL (si aplica): cadena de conexion, migraciones ejecutadas, firewall/Private Endpoint.
- [ ] Azure Blob Storage: contenedor `wa-media` creado, sin acceso publico.
- [ ] Application Insights: conexion configurada, metricas visibles.
- [ ] Key Vault: todos los secretos cargados, permisos de Managed Identity otorgados.

### WhatsApp Meta
- [ ] App creada y verificada en Meta Business.
- [ ] Tokens: `WABA_ACCESS_TOKEN` permanente (System User), no temporal.
- [ ] Webhook: URL publica configurada y verificada con `WABA_VERIFY_TOKEN`.
- [ ] Suscripcion al campo `messages` activa.
- [ ] Plantillas: aprobadas en ES/EN/PT.
- [ ] Permisos: `whatsapp_business_messaging`, `whatsapp_business_management`.

### API de Reintegros
- [ ] `Reintegros:BaseUrl` apunta al ambiente correcto (QA o Produccion).
- [ ] `Reintegros:ApiKey` valida y con permisos.

### App Service
- [ ] Todas las variables de entorno configuradas (o referenciadas desde Key Vault).
- [ ] HTTPS Only habilitado.
- [ ] Always On habilitado.
- [ ] Health check configurado con ruta `/healthz`.

### Endpoints
- [ ] `GET /healthz` responde `Healthy`.
- [ ] `GET /readyz` responde `Healthy`.
- [ ] `GET /metrics` expone metricas Prometheus.
- [ ] `GET /webhook/whatsapp` verifica correctamente.
- [ ] `POST /webhook/whatsapp` procesa mensajes (probar con WhatsApp real).

### Seguridad
- [ ] No hay secretos hardcodeados en el codigo fuente.
- [ ] Verificacion de firma HMAC (`X-Hub-Signature-256`) activa.
- [ ] Logs no contienen PII sensible.
- [ ] Redis con TLS habilitado.
- [ ] SQL con conexion encriptada.

---

## 19. Solucion de problemas

### El webhook no se verifica

- Verifique que `WABA_VERIFY_TOKEN` en el servicio coincide exactamente con el configurado en Meta.
- Verifique que el servicio responde en el endpoint `GET /webhook/whatsapp`.
- Verifique que la URL es HTTPS y accesible desde Internet.

### El bot no responde a mensajes de WhatsApp

- Revise los logs en Application Insights para errores.
- Verifique que `WABA_ACCESS_TOKEN` no ha expirado (los tokens temporales duran 24h).
- Verifique la suscripcion al campo `messages` en el webhook de Meta.
- Verifique que `WABA_PHONE_NUMBER_ID` corresponde al numero correcto.

### Error de conexion con Azure Foundry

- Verifique que `FOUNDRY_ENDPOINT` apunta al proyecto correcto.
- Verifique que `FOUNDRY_MODEL_DEPLOYMENT` corresponde al deployment del modelo, no al nombre del agente.
- Verifique que `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` y `AZURE_CLIENT_SECRET` pertenecen al mismo App Registration.
- Verifique que la identidad tiene permisos sobre el proyecto/recurso Foundry.

### Error de conexion con Azure AI Search

- Verifique que `SEARCH_ENDPOINT`, `SEARCH_KNOWLEDGE_BASE_NAME` y `SEARCH_INDEX_NAME` apuntan a la KB/indice correctos.
- En produccion, confirme que `SEARCH_API_KEY` esta vacia y que el log muestra `SearchAuthMode=Entra`.
- Si Search devuelve `403`, asigne al App Registration el rol `Search Index Data Reader` sobre el recurso Search.
- Si `retrieve` falla por Semantic Search, corrija la configuracion de Semantic Search/Knowledge Base. El servicio intentara fallback al indice, pero el camino primario debe ser `retrieve`.

### Error de conexion con Redis

- Verifique que la cadena de conexion incluye `ssl=true` para conexiones TLS.
- Verifique que el puerto es correcto (6380 para Redis Cache, 10000 para Managed Redis).
- Si Redis no esta disponible, la aplicacion usa cache en memoria automaticamente.

### Las traducciones no funcionan

- Verifique que `AZURE_TRANSLATOR_REGION` corresponde a la region del recurso.
- Verifique que el endpoint es `https://api.cognitive.microsofttranslator.com/`.
- Revise los limites de su plan (F0 gratuito tiene un limite de 2M caracteres/mes).

### Los archivos no se suben

- Verifique que `AZURE_STORAGE_CONNECTION_STRING` es correcto.
- Verifique que el contenedor `wa-media` existe.
- Verifique que la cuenta de almacenamiento permite conexiones desde el App Service.
