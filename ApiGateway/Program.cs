using ApiGateway.Authorization;
using ApiGateway.Configuration;
using ApiGateway.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ReverseProxyOptions>()
    .Bind(builder.Configuration.GetSection(ReverseProxyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(AuthenticationOptions.SectionName))
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

app.UseAuthentication();
app.UseAuthorization();
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
