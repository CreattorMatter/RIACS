using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using ServicioReintegros.AssistCard.Infraestructura.Almacenamiento;
using ServicioReintegros.AssistCard.Infraestructura.Cache;
using ServicioReintegros.AssistCard.Infraestructura.IA;
using ServicioReintegros.AssistCard.Infraestructura.Logs;
using ServicioReintegros.AssistCard.Infraestructura.Persistencia.EFCore;
using ServicioReintegros.AssistCard.Infraestructura.Reintegros;
using ServicioReintegros.AssistCard.Infraestructura.Seguridad.Firmas;
using ServicioReintegros.AssistCard.Infraestructura.Traduccion;
using ServicioReintegros.AssistCard.Infraestructura.WhatsApp;

namespace ServicioReintegros.AssistCard.Configuracion
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registra todas las opciones de configuración desde env vars y appsettings.
        /// </summary>
        public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration cfg)
        {
            services.Configure<WabaOpciones>(o =>
            {
                o.AccessToken = cfg["WABA_ACCESS_TOKEN"] ?? string.Empty;
                o.AppSecret = cfg["WABA_APP_SECRET"] ?? string.Empty;
                o.PhoneNumberId = cfg["WABA_PHONE_NUMBER_ID"] ?? string.Empty;
                o.BusinessAccountId = cfg["WABA_BUSINESS_ACCOUNT_ID"] ?? string.Empty;
                o.ApiBase = cfg["WABA_API_BASE"] ?? o.ApiBase;
                o.VerifyToken = cfg["WABA_VERIFY_TOKEN"] ?? string.Empty;
                o.EnableArgentinaFallback = cfg["WABA_ENABLE_ARGENTINA_FALLBACK"] ?? o.EnableArgentinaFallback;
            });

            services.Configure<TranslatorOpciones>(o =>
            {
                o.Endpoint = cfg["AZURE_TRANSLATOR_ENDPOINT"] ?? string.Empty;
                o.Key = cfg["AZURE_TRANSLATOR_KEY"] ?? string.Empty;
                o.Region = cfg["AZURE_TRANSLATOR_REGION"];
            });

            services.Configure<StorageOpciones>(o =>
            {
                o.ConnectionString = cfg["AZURE_STORAGE_CONNECTION_STRING"] ?? string.Empty;
                o.DefaultContainer = cfg["AZURE_BLOB_CONTAINER"] ?? "wa-media";
            });

            services.Configure<FoundryOpciones>(o =>
            {
                o.Endpoint = cfg["FOUNDRY_ENDPOINT"] ?? string.Empty;
                o.ApiKey = cfg["FOUNDRY_API_KEY"] ?? string.Empty;
                o.Deployment = cfg["FOUNDRY_DEPLOYMENT"] ?? string.Empty;
                o.ModelDeployment = cfg["FOUNDRY_MODEL_DEPLOYMENT"] ?? string.Empty;
            });

            services.Configure<SearchOpciones>(o =>
            {
                o.Endpoint = cfg["SEARCH_ENDPOINT"] ?? string.Empty;
                o.ApiKey = cfg["SEARCH_API_KEY"] ?? string.Empty;
                o.KnowledgeBaseName = cfg["SEARCH_KNOWLEDGE_BASE_NAME"] ?? string.Empty;
                o.IndexName = cfg["SEARCH_INDEX_NAME"] ?? string.Empty;
            });

            services.Configure<ReintegrosOpciones>(cfg.GetSection("Reintegros"));
            services.Configure<ReintegrosLocalizationOpciones>(cfg.GetSection("ReintegrosLocalization"));
            services.Configure<LoggerOpciones>(cfg.GetSection("ConversacionesLogger"));

            return services;
        }

        /// <summary>
        /// Registra HttpClients tipados y adaptadores de infraestructura.
        /// </summary>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
        {
            services.AddHttpClient<IProveedorWhatsApp, MetaCloudAdapter>((sp, c) =>
            {
                var waba = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WabaOpciones>>().Value;
                c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(waba.ApiBase)
                    ? "https://graph.facebook.com/v20.0/"
                    : (waba.ApiBase.EndsWith('/') ? waba.ApiBase : waba.ApiBase + "/"));
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddHttpClient<IRegistroConversaciones, SamuLoggerAdapter>((sp, c) =>
            {
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddHttpClient<IProveedorReintegros, AssistCardApiAdapter>((sp, c) =>
            {
                var r = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReintegrosOpciones>>().Value;
                if (!string.IsNullOrWhiteSpace(r.BaseUrl))
                    c.BaseAddress = new Uri(r.BaseUrl.EndsWith('/') ? r.BaseUrl : r.BaseUrl + "/");
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                c.DefaultRequestVersion = HttpVersion.Version11;
                c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12
                }
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddHttpClient<IProveedorTraduccion, AzureTranslatorAdapter>((sp, c) =>
            {
                var t = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranslatorOpciones>>().Value;
                if (!string.IsNullOrWhiteSpace(t.Endpoint))
                    c.BaseAddress = new Uri(t.Endpoint.EndsWith('/') ? t.Endpoint : t.Endpoint + "/");
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddTransient<RetryHandler>();
            services.AddHttpClient<IProveedorIA, FoundryAgentAdapter>((sp, c) =>
            {
                var f = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FoundryOpciones>>().Value;
                if (!string.IsNullOrWhiteSpace(f.Endpoint))
                    c.BaseAddress = new Uri(f.Endpoint);
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddHttpMessageHandler<RetryHandler>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddSingleton<IAlmacenamientoArchivos, AzureBlobAdapter>();
            services.AddSingleton<WhatsAppMetaSignatureVerifier>();

            // Cache: Redis si disponible, InMemory como fallback
            var redisUrl = cfg["REDIS_URL"];
            if (!string.IsNullOrWhiteSpace(redisUrl))
                services.AddSingleton<ICacheAplicacion>(_ => RedisAdapter.Crear(redisUrl));
            else
                services.AddSingleton<ICacheAplicacion, InMemoryCacheAdapter>();

            // EF Core (opcional)
            var dbConn = cfg["DATABASE_URL"] ?? cfg.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(dbConn))
                services.AddDbContext<ContextoAplicacion>(opt => opt.UseSqlServer(dbConn));

            return services;
        }

        /// <summary>
        /// Registra servicios de aplicación (orquestador, localizer, sesion manager).
        /// </summary>
        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            services.AddScoped<OrquestadorConversacion>();
            services.AddScoped<IReintegrosLocalizer, ReintegrosLocalizer>();
            services.AddSingleton<SesionManager>();
            return services;
        }

        /// <summary>
        /// Registra Swagger con la info del proyecto.
        /// </summary>
        public static IServiceCollection AddAppSwagger(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Servicio de Reintegros - AssistCard",
                    Version = "v1",
                    Description = "API del webhook de WhatsApp y servicios de IA/Reintegros"
                });
            });
            return services;
        }
    }
}
