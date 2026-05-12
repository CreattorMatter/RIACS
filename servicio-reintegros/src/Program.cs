using FastEndpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Prometheus;
using ServicioReintegros.AssistCard.Configuracion;
using System.Text;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

// Telemetría
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddHealthChecks();

// FastEndpoints + Swagger
builder.Services.AddFastEndpoints();
builder.Services.AddAppSwagger();

// Configuración, infraestructura y servicios de aplicación
builder.Services
    .AddAppOptions(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddAppServices();

var app = builder.Build();

// Métricas Prometheus
app.UseHttpMetrics();
app.MapMetrics("/metrics");

// Middleware para capturar el cuerpo crudo (raw) y verificar firma HMAC
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/webhook/whatsapp") && string.Equals(ctx.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        ctx.Items["RawBody"] = body;
        ctx.Request.Body.Position = 0;
    }
    await next();
});

// FastEndpoints y Swagger UI
app.UseFastEndpoints();
app.UseSwagger();
app.UseSwaggerUI(s =>
{
    s.DocumentTitle = "Documentación - Servicio de Reintegros";
});

// Health/Readiness
app.MapHealthChecks("/healthz", new HealthCheckOptions());
app.MapHealthChecks("/readyz", new HealthCheckOptions());

app.Run();
