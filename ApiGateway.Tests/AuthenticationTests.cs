using ApiGateway.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiGateway.Tests;

public class AuthenticationTests
{
    [Fact]
    public async Task JwtBearerAuthentication_RegistersDefaultSchemesAndOptions()
    {
        var configData = new Dictionary<string, string?>
        {
            {"Authentication:Authority", "https://auth.example.com"},
            {"Authentication:Audience", "test-audience"},
            {"Authentication:ClientId", "test-client"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var authOptions = configuration.GetSection(ApiGateway.Configuration.AuthenticationOptions.SectionName).Get<ApiGateway.Configuration.AuthenticationOptions>()!;
            options.Authority = authOptions.Authority;
            options.Audience = authOptions.Audience;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authOptions.Authority,
                ValidateAudience = true,
                ValidAudience = authOptions.Audience
            };
        });

        var serviceProvider = services.BuildServiceProvider();
        var authSchemeProvider = serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        var defaultAuthScheme = await authSchemeProvider.GetDefaultAuthenticateSchemeAsync();
        Assert.NotNull(defaultAuthScheme);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, defaultAuthScheme.Name);

        var defaultChallengeScheme = await authSchemeProvider.GetDefaultChallengeSchemeAsync();
        Assert.NotNull(defaultChallengeScheme);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, defaultChallengeScheme.Name);

        var jwtOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = jwtOptionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(jwtOptions);
        Assert.Equal("https://auth.example.com", jwtOptions.Authority);
        Assert.Equal("test-audience", jwtOptions.Audience);
        Assert.True(jwtOptions.RequireHttpsMetadata);
        Assert.NotNull(jwtOptions.TokenValidationParameters);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateIssuer);
        Assert.Equal("https://auth.example.com", jwtOptions.TokenValidationParameters.ValidIssuer);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateAudience);
        Assert.Equal("test-audience", jwtOptions.TokenValidationParameters.ValidAudience);
    }

    [Fact]
    public void AuthenticationOptions_BindsClockSkewSuccessfully()
    {
        var configData = new Dictionary<string, string?>
        {
            {"Authentication:Authority", "https://auth.example.com"},
            {"Authentication:Audience", "test-audience"},
            {"Authentication:ClientId", "test-client"},
            {"Authentication:ClockSkewMinutes", "10"},
            {"Authentication:ValidateLifetime", "true"},
            {"Authentication:ValidateIssuer", "true"},
            {"Authentication:ValidateAudience", "true"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<ApiGateway.Configuration.AuthenticationOptions>()
            .Bind(configuration.GetSection(ApiGateway.Configuration.AuthenticationOptions.SectionName))
            .ValidateDataAnnotations();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ApiGateway.Configuration.AuthenticationOptions>>().Value;

        Assert.NotNull(options);
        Assert.Equal(10, options.ClockSkewMinutes);
        Assert.True(options.ValidateLifetime);
        Assert.True(options.ValidateIssuer);
        Assert.True(options.ValidateAudience);
    }

    [Fact]
    public void JwtBearerOptions_ConfiguresTokenValidationParametersAndClockSkew()
    {
        var configData = new Dictionary<string, string?>
        {
            {"Authentication:Authority", "https://auth.example.com"},
            {"Authentication:Audience", "test-audience"},
            {"Authentication:ClientId", "test-client"},
            {"Authentication:ClockSkewMinutes", "5"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var authOptions = configuration.GetSection(ApiGateway.Configuration.AuthenticationOptions.SectionName).Get<ApiGateway.Configuration.AuthenticationOptions>()!;
            options.Authority = authOptions.Authority;
            options.Audience = authOptions.Audience;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
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

        var serviceProvider = services.BuildServiceProvider();
        var jwtOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = jwtOptionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(jwtOptions);
        Assert.NotNull(jwtOptions.TokenValidationParameters);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateIssuer);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateAudience);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateLifetime);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.Equal(TimeSpan.FromMinutes(5), jwtOptions.TokenValidationParameters.ClockSkew);
    }

    [Fact]
    public void JwtBearerOptions_ConfiguresKeyRotationAndResilienceProperties()
    {
        var configData = new Dictionary<string, string?>
        {
            {"Authentication:Authority", "https://auth.example.com"},
            {"Authentication:Audience", "test-audience"},
            {"Authentication:ClientId", "test-client"},
            {"Authentication:BackchannelTimeoutSeconds", "15"},
            {"Authentication:MetadataAddress", "https://auth.example.com/custom-metadata"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var authOptions = configuration.GetSection(ApiGateway.Configuration.AuthenticationOptions.SectionName).Get<ApiGateway.Configuration.AuthenticationOptions>()!;
            options.Authority = authOptions.Authority;
            options.Audience = authOptions.Audience;
            options.MetadataAddress = authOptions.MetadataAddress!;
            options.BackchannelTimeout = TimeSpan.FromSeconds(authOptions.BackchannelTimeoutSeconds);
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authOptions.Authority,
                ValidateAudience = true,
                ValidAudience = authOptions.Audience,
                ValidateIssuerSigningKey = true
            };
        });

        var serviceProvider = services.BuildServiceProvider();
        var jwtOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = jwtOptionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(jwtOptions);
        Assert.Equal("https://auth.example.com/custom-metadata", jwtOptions.MetadataAddress);
        Assert.Equal(TimeSpan.FromSeconds(15), jwtOptions.BackchannelTimeout);
        Assert.NotNull(jwtOptions.TokenValidationParameters);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey);
    }

    [Fact]
    public async Task ConfigurationManager_FetchesOpenIdConfigurationAndJwksSuccessfully()
    {
        // Setup a stub HttpMessageHandler to simulate OIDC discovery and JWKS endpoints
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri.EndsWith(".well-known/openid-configuration") == true)
            {
                var json = "{\"issuer\":\"https://auth.example.com\",\"jwks_uri\":\"https://auth.example.com/jwks\"}";
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
            else if (request.RequestUri?.AbsoluteUri.EndsWith("/jwks") == true)
            {
                var jwksJson = "{\"keys\":[{\"kty\":\"RSA\",\"kid\":\"key-1\",\"use\":\"sig\",\"n\":\"abc\",\"e\":\"AQAB\"}]}";
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jwksJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler);
        var documentRetriever = new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever(httpClient)
        {
            RequireHttps = false
        };

        var configManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
            "https://auth.example.com/.well-known/openid-configuration",
            new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever(),
            documentRetriever
        );

        var config = await configManager.GetConfigurationAsync(CancellationToken.None);

        Assert.NotNull(config);
        Assert.Equal("https://auth.example.com", config.Issuer);
        Assert.NotNull(config.JsonWebKeySet);
        Assert.Single(config.JsonWebKeySet.Keys);
        Assert.Equal("key-1", config.JsonWebKeySet.Keys[0].Kid);
    }

    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
