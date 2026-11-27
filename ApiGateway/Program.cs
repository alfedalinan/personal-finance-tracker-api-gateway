using ApiGateway.Authorization;
using ApiGateway.Configuration;
using ApiGateway.Middleware;
using ApiGateway.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ReverseProxyOptions>()
    .Bind(builder.Configuration.GetSection(ReverseProxyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(AuthenticationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<TenantHeaderOptions>()
    .Bind(builder.Configuration.GetSection(TenantHeaderOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<DownstreamServicesOptions>()
    .Bind(builder.Configuration.GetSection(DownstreamServicesOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ApiGateway.Authorization.AuthorizationOptions>()
    .Bind(builder.Configuration.GetSection(ApiGateway.Authorization.AuthorizationOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

builder.Services.AddApiGatewayRateLimiting(builder.Configuration);

var otelConfig = builder.Configuration.GetSection("OpenTelemetry");
var otelServiceName = otelConfig["ServiceName"] ?? "ApiGateway";
var otelEndpoint = otelConfig["Endpoint"] ?? "http://localhost:4317";
var samplingRatio = otelConfig.GetValue<double?>("SamplingRatio") ?? 0.1;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(otelServiceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                var correlationId = request.HttpContext.Items["CorrelationId"]?.ToString();
                if (!string.IsNullOrEmpty(correlationId))
                {
                    activity.SetTag("correlation.id", correlationId);
                }
            };
        })
        .AddHttpClientInstrumentation()
        .AddSource("ApiGateway.*")
        .SetSampler(new ApiGateway.Observability.GatewayHeadSampler(samplingRatio))
        .AddProcessor(new ApiGateway.Observability.TelemetrySanitizationProcessor())
        .AddOtlpExporter(options =>
        {
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            options.Endpoint = new Uri(otelEndpoint);
        }));

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var maxRequestBodySize = builder.Configuration.GetValue<long?>("MaxRequestBodySize") ?? 31457280;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodySize;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var authOptions = builder.Configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>()!;
    options.Authority = authOptions.Authority;
    options.Audience = authOptions.Audience;
    options.RequireHttpsMetadata = builder.Environment.IsProduction();
    
    if (!string.IsNullOrEmpty(authOptions.MetadataAddress))
    {
        options.MetadataAddress = authOptions.MetadataAddress;
    }
    
    if (authOptions.BackchannelTimeoutSeconds > 0)
    {
        options.BackchannelTimeout = TimeSpan.FromSeconds(authOptions.BackchannelTimeoutSeconds);
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = authOptions.ValidateIssuer,
        ValidIssuer = authOptions.Authority,
        ValidateAudience = authOptions.ValidateAudience,
        ValidAudience = authOptions.Audience,
        ValidateLifetime = authOptions.ValidateLifetime,
        ValidateIssuerSigningKey = authOptions.ValidateIssuerSigningKey,
        ClockSkew = TimeSpan.FromMinutes(authOptions.ClockSkewMinutes)
    };
});

builder.Services.AddAuthorization(options =>
{
    var authzConfig = builder.Configuration.GetSection(ApiGateway.Authorization.AuthorizationOptions.SectionName).Get<ApiGateway.Authorization.AuthorizationOptions>();

    if (authzConfig?.Policies != null && authzConfig.Policies.Count > 0)
    {
        foreach (var kvp in authzConfig.Policies)
        {
            var policyName = kvp.Key;
            var policyConfig = kvp.Value;
            options.AddPolicy(policyName, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ScopeRequirement(policyConfig.Scopes, policyConfig.Roles));
            });
        }
    }
    else
    {
        options.AddPolicy("RequireFinanceRead", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ScopeRequirement(new[] { "finance.read" }, new[] { "FinanceReader", "Admin" }));
        });

        options.AddPolicy("RequireFinanceWrite", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ScopeRequirement(new[] { "finance.write" }, new[] { "FinanceWriter", "Admin" }));
        });

        options.AddPolicy("RequireAdmin", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ScopeRequirement(Enumerable.Empty<string>(), new[] { "Admin" }));
        });
    }
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var correlationId = context.Items["CorrelationId"]?.ToString()
            ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        var responseObj = new
        {
            error = "Internal Server Error",
            message = "An unexpected error occurred processing your request.",
            correlationId = correlationId,
            statusCode = 500
        };

        await context.Response.WriteAsJsonAsync(responseObj);
    });
});

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<OperationalLoggingMiddleware>();

app.Use(async (context, next) =>
{
    var maxBodySizeLimit = builder.Configuration.GetValue<long?>("MaxRequestBodySize") ?? 31457280;
    var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (maxRequestBodySizeFeature != null && !maxRequestBodySizeFeature.IsReadOnly)
    {
        maxRequestBodySizeFeature.MaxRequestBodySize = maxBodySizeLimit;
    }

    if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > maxBodySizeLimit)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/json";
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Payload Too Large",
            message = $"Request body exceeds the maximum allowed size of {maxBodySizeLimit} bytes.",
            correlationId = correlationId,
            statusCode = 413
        });
        return;
    }

    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantIdInjectionMiddleware>();
app.UseRateLimiter();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseRateLimiter();

    proxyPipeline.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");
            context.Response.Headers.Remove("X-AspNet-Version");
            return Task.CompletedTask;
        });

        try
        {
            await next();

            var errorFeature = context.Features.Get<Yarp.ReverseProxy.Forwarder.IForwarderErrorFeature>();
            if (errorFeature != null && !context.Response.HasStarted)
            {
                context.Response.StatusCode = errorFeature.Error switch
                {
                    Yarp.ReverseProxy.Forwarder.ForwarderError.RequestTimedOut => StatusCodes.Status504GatewayTimeout,
                    _ => StatusCodes.Status502BadGateway
                };
            }
        }
        catch (Exception)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
            }
        }
    });
});

app.MapGet("/", () => "Personal Finance Tracker API Gateway is running!");

app.Run();

public partial class Program { }
