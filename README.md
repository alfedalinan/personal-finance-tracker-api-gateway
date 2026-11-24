# Personal Finance Tracker API Gateway

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2011.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![YARP Proxy](https://img.shields.io/badge/YARP-Reverse%20Proxy-blue)](https://microsoft.github.io/reverse-proxy/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

The **Personal Finance Tracker API Gateway** is a high-performance, resilient API Gateway built using **.NET** and **YARP (Yet Another Reverse Proxy)**. It serves as the single entry point for all client requests in the Personal Finance Tracker ecosystem, managing secure request routing, JWT-based authentication and authorization, rate limiting, and observability.

---

## Key Features

- **Reverse Proxying via YARP**: Efficiently routes downstream traffic to microservices (such as identity/authentication and core finance services) with configurable health checks, load balancing, and request transforms.
- **JWT Authentication & Authorization**: Validates incoming bearer tokens against configured identity providers (`Authority`, `Audience`), ensuring unauthorized requests are intercepted at the edge.
- **Modular Configuration**: Uses strongly-typed configuration options (`AuthenticationOptions`, `DownstreamServicesOptions`, `ReverseProxyOptions`) bound directly to configuration sections with data annotation validation and startup checks.
- **Rate Limiting & Resilience**: Protects downstream services against traffic spikes and abuse.
- **Observability & Diagnostics**: Centralized logging, diagnostics, and health check endpoints.

---

## Project Architecture & Structure

The solution is organized into clean, modular projects and directories:

```text
personal-finance-tracker-api-gateway/
├── PersonalFinanceTrackerApi.sln      # Solution file supporting multi-target .NET builds
├── ApiGateway/                       # Main API Gateway application
│   ├── ApiGateway.csproj
│   ├── Program.cs                    # Service registration, YARP middleware, auth setup
│   ├── appsettings.json              # Production/base configuration (routes, clusters, auth)
│   ├── appsettings.Development.json  # Development overrides
│   ├── Authentication/               # Authentication handlers & middleware
│   ├── Authorization/                # Custom authorization policies & rules
│   ├── Configuration/                # Strongly-typed configuration options
│   │   ├── AuthenticationOptions.cs
│   │   ├── DownstreamServicesOptions.cs
│   │   └── ReverseProxyOptions.cs
│   ├── Middleware/                   # Custom gateway middleware (logging, correlation IDs, etc.)
│   ├── Observability/                # Metrics, tracing, and health checks
│   └── RateLimiting/                 # Rate limiting policies and rate-limit counters
└── ApiGateway.Tests/                 # Unit and integration test suite
    ├── ApiGateway.Tests.csproj
    ├── AuthenticationTests.cs
    └── ConfigurationTests.cs
```

---

## Configuration Options

Configuration is managed via `appsettings.json` (and environment overrides). The gateway validates options on startup using `Microsoft.Extensions.Options.DataAnnotations`.

### 1. Reverse Proxy (`ReverseProxy`)
Configures YARP routes, route transforms (e.g., path prefix removal, header sanitization), and destination clusters with active/passive health checks.

### 2. Authentication (`AuthenticationOptions`)
Defines token issuer validation, authority endpoint, audience, and backchannel timeouts:
```json
"Authentication": {
  "Authority": "https://auth.example.com",
  "Audience": "finance-api",
  "ValidateIssuer": true,
  "ValidateAudience": true
}
```

### 3. Downstream Services (`DownstreamServicesOptions`)
Maps logical service names to their physical base URLs and endpoints.

---

## Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) (or later compatible .NET SDK)

### Building the Solution
Clone the repository and build the solution:
```bash
dotnet restore
dotnet build --no-restore
```

### Running Tests
Execute the unit and configuration test suite:
```bash
dotnet test --no-build
```

### Running the API Gateway
Start the API Gateway development server:
```bash
cd ApiGateway
dotnet run
```
By default, the gateway listens on configured HTTP/HTTPS ports (see `Properties/launchSettings.json` or `appsettings.json`).

---

## Contributing

1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/amazing-feature`).
3. Commit your changes (`git commit -m 'Add amazing feature'`).
4. Push to the branch (`git push origin feature/amazing-feature`).
5. Open a Pull Request.

---

## License

This project is licensed under the [MIT License](LICENSE).
