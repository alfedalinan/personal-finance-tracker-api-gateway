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

    [Fact]
    public void YarpConfiguration_RoutesAndClusters_BindCorrectly()
    {
        var configData = new Dictionary<string, string?>
        {
            {"ReverseProxy:Routes:auth-route:ClusterId", "identity-cluster"},
            {"ReverseProxy:Routes:auth-route:Match:Path", "/auth/{**catch-all}"},
            {"ReverseProxy:Routes:api-route:ClusterId", "finance-cluster"},
            {"ReverseProxy:Routes:api-route:Match:Path", "/api/{**catch-all}"},
            {"ReverseProxy:Clusters:identity-cluster:Destinations:destination1:Address", "https://localhost:5001"},
            {"ReverseProxy:Clusters:finance-cluster:Destinations:destination1:Address", "https://localhost:5002"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

        var serviceProvider = services.BuildServiceProvider();
        var proxyConfigProvider = serviceProvider.GetRequiredService<Yarp.ReverseProxy.Configuration.IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        Assert.NotNull(proxyConfig);
        
        var routes = proxyConfig.Routes.ToList();
        Assert.Equal(2, routes.Count);
        
        var authRoute = routes.FirstOrDefault(r => r.RouteId == "auth-route");
        Assert.NotNull(authRoute);
        Assert.Equal("identity-cluster", authRoute.ClusterId);
        Assert.Equal("/auth/{**catch-all}", authRoute.Match.Path);

        var apiRoute = routes.FirstOrDefault(r => r.RouteId == "api-route");
        Assert.NotNull(apiRoute);
        Assert.Equal("finance-cluster", apiRoute.ClusterId);
        Assert.Equal("/api/{**catch-all}", apiRoute.Match.Path);

        var clusters = proxyConfig.Clusters.ToList();
        Assert.Equal(2, clusters.Count);

        var identityCluster = clusters.FirstOrDefault(c => c.ClusterId == "identity-cluster");
        Assert.NotNull(identityCluster);
        Assert.NotNull(identityCluster.Destinations);
        Assert.Equal("https://localhost:5001", identityCluster.Destinations["destination1"].Address);

        var financeCluster = clusters.FirstOrDefault(c => c.ClusterId == "finance-cluster");
        Assert.NotNull(financeCluster);
        Assert.NotNull(financeCluster.Destinations);
        Assert.Equal("https://localhost:5002", financeCluster.Destinations["destination1"].Address);
    }

    [Fact]
    public void YarpConfiguration_ApiRoute_PreservesApiPrefix()
    {
        var configData = new Dictionary<string, string?>
        {
            {"ReverseProxy:Routes:api-route:ClusterId", "finance-cluster"},
            {"ReverseProxy:Routes:api-route:Match:Path", "/api/{**catch-all}"},
            {"ReverseProxy:Routes:api-route:Transforms:0:ResponseHeader", "Server"},
            {"ReverseProxy:Routes:api-route:Transforms:0:Set", ""},
            {"ReverseProxy:Clusters:finance-cluster:Destinations:destination1:Address", "http://localhost:5088"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

        var serviceProvider = services.BuildServiceProvider();
        var proxyConfigProvider = serviceProvider.GetRequiredService<Yarp.ReverseProxy.Configuration.IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        Assert.NotNull(proxyConfig);

        var apiRoute = proxyConfig.Routes.FirstOrDefault(r => r.RouteId == "api-route");
        Assert.NotNull(apiRoute);
        Assert.Equal("/api/{**catch-all}", apiRoute.Match.Path);
        
        // Ensure no PathRemovePrefix transform exists on api-route
        if (apiRoute.Transforms != null)
        {
            foreach (var transform in apiRoute.Transforms)
            {
                Assert.DoesNotContain("PathRemovePrefix", transform.Keys);
            }
        }
    }

    [Fact]
    public void YarpConfiguration_TransformsAndHealthChecks_BindCorrectly()
    {
        var configData = new Dictionary<string, string?>
        {
            {"ReverseProxy:Routes:auth-route:ClusterId", "identity-cluster"},
            {"ReverseProxy:Routes:auth-route:Match:Path", "/auth/{**catch-all}"},
            {"ReverseProxy:Routes:auth-route:Transforms:0:PathRemovePrefix", "/auth"},
            {"ReverseProxy:Routes:auth-route:Transforms:1:PathPrefix", "/api/auth"},
            {"ReverseProxy:Routes:auth-route:Transforms:2:ResponseHeader", "Server"},
            {"ReverseProxy:Routes:auth-route:Transforms:2:Set", ""},
            
            {"ReverseProxy:Clusters:identity-cluster:Destinations:destination1:Address", "https://localhost:5001"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Active:Enabled", "true"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Active:Interval", "00:00:10"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Active:Timeout", "00:00:05"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Active:Policy", "ServiceProviderPolicy"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Active:Path", "/health"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Passive:Enabled", "true"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Passive:Policy", "ServiceProviderPolicy"},
            {"ReverseProxy:Clusters:identity-cluster:HealthCheck:Passive:ReactivationPeriod", "00:01:00"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

        var serviceProvider = services.BuildServiceProvider();
        var proxyConfigProvider = serviceProvider.GetRequiredService<Yarp.ReverseProxy.Configuration.IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        Assert.NotNull(proxyConfig);

        var authRoute = proxyConfig.Routes.FirstOrDefault(r => r.RouteId == "auth-route");
        Assert.NotNull(authRoute);
        Assert.NotNull(authRoute.Transforms);
        Assert.NotEmpty(authRoute.Transforms);

        var identityCluster = proxyConfig.Clusters.FirstOrDefault(c => c.ClusterId == "identity-cluster");
        Assert.NotNull(identityCluster);
        Assert.NotNull(identityCluster.HealthCheck);
        Assert.NotNull(identityCluster.HealthCheck.Active);
        Assert.True(identityCluster.HealthCheck.Active.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(10), identityCluster.HealthCheck.Active.Interval);
        Assert.Equal("/health", identityCluster.HealthCheck.Active.Path);

        Assert.NotNull(identityCluster.HealthCheck.Passive);
        Assert.True(identityCluster.HealthCheck.Passive.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(1), identityCluster.HealthCheck.Passive.ReactivationPeriod);
    }
}
