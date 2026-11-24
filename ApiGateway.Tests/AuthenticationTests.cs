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
}
