using ApiGateway.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddAuthorization();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy(proxyPipeline =>
{
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
