# Personal Finance Tracker API Gateway

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2011.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![YARP Proxy](https://img.shields.io/badge/YARP-Reverse%20Proxy-blue)](https://microsoft.github.io/reverse-proxy/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

The **Personal Finance Tracker API Gateway** is a high-performance, resilient API Gateway built using **.NET** and **YARP (Yet Another Reverse Proxy)**. It serves as the single entry point for all client requests in the Personal Finance Tracker ecosystem, managing secure request routing, JWT-based authentication and role/scope authorization, rate limiting, tenant header injection, correlation tracking, and advanced observability.

---

## Current Project State & Progress

The API Gateway is fully operational with robust middleware pipelines, security validations, and a comprehensive unit & integration test suite (`ApiGateway.Tests`). Below is the current implementation status and milestone tracker:

| Component / Feature | Status | Description |
| :--- | :---: | :--- |
| **YARP Reverse Proxy Integration** | ✅ Completed | Request routing, path transforms, load balancing, and destination clusters. |
| **JWT Authentication & Bearer Validation** | ✅ Completed | Token validation with configurable Authority, Audience, and issuer checks. |
| **Scope & Policy Authorization** | ✅ Completed | Custom scope requirements (`ScopeRequirement`) and authorization policies. |
| **Strongly-Typed Configuration Options** | ✅ Completed | Validated options (`AuthenticationOptions`, `DownstreamServicesOptions`, `ReverseProxyOptions`, `RateLimitingOptions`, `TenantHeaderOptions`). |
| **Custom Gateway Middleware** | ✅ Completed | Correlation ID tracking, operational request/response logging, and tenant ID injection. |
| **Rate Limiting & Throttling** | ✅ Completed | Fixed-window / sliding-window rate limiting extensions and policy mapping. |
| **Observability & Telemetry Sanitization** | ✅ Completed | OpenTelemetry tracing, custom head sampling, log sanitization, and telemetry processors. |
| **Test Suite Coverage** | ✅ Completed | Comprehensive unit and integration tests covering auth, middleware, config, and routing. |
| **Distributed Tracing & Service Mesh Integration** | 🔄 Planned | Advanced distributed tracing propagation across downstream microservices. |

---

## Key Features & Architecture

- **Reverse Proxying via YARP**: Efficiently routes downstream traffic to microservices with configurable health checks and request transforms.
- **Security & Identity**: Validates incoming bearer tokens and enforces granular scope-based access control.
- **Modular Request Pipeline**:
  - **Correlation ID Middleware**: Assigns or propagates a unique correlation ID (`X-Correlation-ID`) for end-to-end distributed tracing.
  - **Operational Logging Middleware**: Logs gateway requests, responses, and execution durations safely.
  - **Tenant ID Injection Middleware**: Injects or validates tenant context (`X-Tenant-ID`) across downstream requests.
- **Rate Limiting**: Protects downstream microservices from traffic spikes and denial-of-service abuse using ASP.NET Core rate limiting extensions.
- **Observability & Diagnostics**:
  - **Gateway Head Sampler**: Tailored sampling strategy for high-throughput gateway telemetry.
  - **Log & Telemetry Sanitization**: Removes sensitive personal identifiable information (PII) and credentials from logs and telemetry spans.

---

## Project Structure

```text
personal-finance-tracker-api-gateway/
├── PersonalFinanceTrackerApi.sln      # Solution file supporting multi-target .NET builds
├── ApiGateway/                       # Main API Gateway application
│   ├── ApiGateway.csproj
│   ├── Program.cs                    # Service registration, YARP middleware, auth setup
│   ├── appsettings.json              # Base configuration (routes, clusters, auth, rate limiting)
│   ├── appsettings.Development.json  # Development overrides
│   ├── Authentication/               # Authentication handlers & setup
│   ├── Authorization/                # Custom authorization policies & scope requirements
│   │   ├── AuthorizationOptions.cs
│   │   └── ScopeRequirement.cs
│   ├── Configuration/                # Strongly-typed configuration options
│   │   ├── AuthenticationOptions.cs
│   │   ├── DownstreamServicesOptions.cs
│   │   ├── RateLimitingOptions.cs
│   │   ├── ReverseProxyOptions.cs
│   │   └── TenantHeaderOptions.cs
│   ├── Middleware/                   # Custom gateway middleware
│   │   ├── CorrelationIdMiddleware.cs
│   │   ├── OperationalLoggingMiddleware.cs
│   │   └── TenantIdInjectionMiddleware.cs
│   ├── Observability/                # Telemetry, sampling, and sanitization
│   │   ├── GatewayHeadSampler.cs
│   │   ├── LogSanitizer.cs
│   │   └── TelemetrySanitizationProcessor.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   └── RateLimiting/                 # Rate limiting policies & extensions
│       └── RateLimitingExtensions.cs
└── ApiGateway.Tests/                 # Unit and integration test suite
    ├── ApiGateway.Tests.csproj
    ├── AuthRegistrationTests.cs
    ├── AuthenticationTests.cs
    ├── AuthorizationTests.cs
    ├── ConfigurationTests.cs
    ├── IntegrationTests.cs
    ├── MiddlewareTests.cs
    ├── ObservabilityTests.cs
    ├── RateLimitingTests.cs
    └── TenantIdInjectionMiddlewareTests.cs
```

---

## Configuration Options

Configuration is managed via `appsettings.json` and environment overrides, validated at startup using `Microsoft.Extensions.Options.DataAnnotations`.

### 1. Reverse Proxy (`ReverseProxy`)
Configures YARP routes, path transforms, and destination clusters.

### 2. Authentication (`AuthenticationOptions`)
Defines token issuer validation, authority endpoint, and audience:
```json
"Authentication": {
  "Authority": "https://auth.example.com",
  "Audience": "finance-api",
  "ValidateIssuer": true,
  "ValidateAudience": true
}
```

### 3. Downstream Services (`DownstreamServicesOptions`)
Maps logical service names to physical base URLs.

---

## Getting Started

### Prerequisites
- [.NET 8.0 / .NET 11.0 SDK](https://dotnet.microsoft.com/download) (or later)

### Building the Solution
Clone the repository and build:
```bash
dotnet restore
dotnet build --no-restore
```

### Running Tests
Execute the comprehensive test suite:
```bash
dotnet test --no-build
```

### Running the API Gateway
Start the API Gateway development server:
```bash
cd ApiGateway
dotnet run
```

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
