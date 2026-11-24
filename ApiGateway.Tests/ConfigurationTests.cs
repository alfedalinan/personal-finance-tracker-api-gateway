using ApiGateway.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiGateway.Tests;

public class ConfigurationTests
{
    [Fact]
    public void AuthenticationOptions_ValidConfiguration_BindsSuccessfully()
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
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.NotNull(options);
        Assert.Equal("https://auth.example.com", options.Authority);
        Assert.Equal("test-audience", options.Audience);
        Assert.Equal("test-client", options.ClientId);
    }

    [Fact]
    public void AuthenticationOptions_MissingRequiredFields_ThrowsValidationException()
    {
        var configData = new Dictionary<string, string?>
        {
            {"Authentication:Authority", "not-a-url"},
            // Missing Audience and ClientId
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations();

        var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = serviceProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        });
    }

    [Fact]
    public void DownstreamServicesOptions_ValidConfiguration_BindsSuccessfully()
    {
        var configData = new Dictionary<string, string?>
        {
            {"DownstreamServices:UserService", "https://localhost:5001"},
            {"DownstreamServices:TransactionService", "https://localhost:5002"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<DownstreamServicesOptions>()
            .Bind(configuration.GetSection(DownstreamServicesOptions.SectionName))
            .ValidateDataAnnotations();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;

        Assert.NotNull(options);
        Assert.Equal("https://localhost:5001", options.UserService);
        Assert.Equal("https://localhost:5002", options.TransactionService);
    }

    [Fact]
    public void DownstreamServicesOptions_InvalidUrl_ThrowsValidationException()
    {
        var configData = new Dictionary<string, string?>
        {
            {"DownstreamServices:UserService", "invalid-url"},
            {"DownstreamServices:TransactionService", "https://localhost:5002"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<DownstreamServicesOptions>()
            .Bind(configuration.GetSection(DownstreamServicesOptions.SectionName))
            .ValidateDataAnnotations();

        var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
        });
    }

    [Fact]
    public void ReverseProxyOptions_ValidConfiguration_BindsSuccessfully()
    {
        var configData = new Dictionary<string, string?>
        {
            {"ReverseProxy:TimeoutSeconds", "45"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<ReverseProxyOptions>()
            .Bind(configuration.GetSection(ReverseProxyOptions.SectionName))
            .ValidateDataAnnotations();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;

        Assert.NotNull(options);
        Assert.Equal(45, options.TimeoutSeconds);
    }
}
