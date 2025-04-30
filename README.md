# BenchmarkingWithOtel

A comprehensive benchmarking solution for testing deployment environments with OpenTelemetry instrumentation. This project provides a Server component that connects to an Oracle database and a Client component that generates various types of load patterns to benchmark performance.

## Architecture Overview

The solution consists of these key components:

- **Server Component**: An ASP.NET Core minimal API service that connects to Oracle DB, with horizontal scaling capabilities
- **Client Component**: A .NET Worker Service that generates configurable load patterns against the server
- **Oracle Database**: Stores the benchmark data
- **.NET Aspire**: Provides local orchestration, service discovery, and connection string management
- **OpenTelemetry**: Provides comprehensive observability across the entire stack

### Architectural Diagram

```
┌───────────────┐     ┌──────────────────┐     ┌────────────────┐
│               │     │                  │     │                │
│  Client (1x)  │────▶│  Server (N+1)   │────▶│   Oracle DB    │
│               │     │                  │     │                │
└───────────────┘     └──────────────────┘     └────────────────┘
        │                     │                        │
        │                     │                        │
        ▼                     ▼                        ▼
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│                    OpenTelemetry Traces                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Setup and Configuration

### Prerequisites

- .NET 9.0 SDK
- Oracle Database (can be containerized or external)
- Docker (optional, for containerization)

### Development Environment Setup

1. Clone the repository:
   ```
   git clone [repository-url]
   cd BenchmarkingWithOtel
   ```

2. Configure Oracle Connection:
   - The connection string is automatically managed by Aspire
   - For local development, update `Main/Main.AppHost/Program.cs` with your Oracle settings

3. Restore dependencies:
   ```
   dotnet restore Main/Main.sln
   ```

4. Build the solution:
   ```
   dotnet build Main/Main.sln
   ```

## Running the Application

### Using Aspire Dashboard (Development)

1. Run the application with Aspire Dashboard:
   ```
   dotnet run --project Main/Main.AppHost/Main.AppHost.csproj
   ```

2. The Aspire Dashboard will open automatically, typically at `http://localhost:15888`.

3. In the dashboard, you can:
   - See all services and their health
   - View logs from each component
   - Monitor trace data
   - Check resource usage

### Benchmark Configuration

The client benchmark settings can be configured in `Main/BenchmarkingWithOtel.Client/appsettings.json`:

```json
"Benchmark": {
  "OperationCount": 1000,  // Number of operations per test
  "Iterations": 3,         // Number of benchmark iterations
  "DelayBetweenRuns": 5000 // Delay between tests in milliseconds
}
```

## Deployment Guide

Since Aspire is primarily designed for local development, deploying to production requires some additional steps.

### Production Deployment Options

#### 1. Kubernetes Deployment

1. **Create Kubernetes Manifests**:
   
   Create deployment YAML files for each component:
   
   - Client Deployment (1 replica)
   - Server Deployment (N replicas)
   - Database Service (if using in-cluster DB)
   
2. **Connection String Management**:
   
   Replace Aspire's connection string management with Kubernetes Secrets:
   
   ```yaml
   apiVersion: v1
   kind: Secret
   metadata:
     name: oracle-connection
   type: Opaque
   stringData:
     connectionString: "User Id=username;Password=password;Data Source=//hostname:1521/servicename;"
   ```
   
   Mount this secret as an environment variable:
   
   ```yaml
   env:
     - name: ConnectionStrings__oracledb
       valueFrom:
         secretKeyRef:
           name: oracle-connection
           key: connectionString
   ```

3. **Service Discovery**:
   
   Use Kubernetes service DNS instead of Aspire service discovery:
   
   ```yaml
   env:
     - name: ServerUrl
       value: "http://benchmarkingwithotel-server-service"
   ```

#### 2. Azure App Service Deployment

1. **Create Azure Resources**:
   - App Service Plans for both client and server
   - App Services for both components
   - Azure Database (or connect to external Oracle)

2. **Connection String Configuration**:
   - Add the connection string to Application Settings
   - Set `ConnectionStrings__oracledb` with your Oracle connection string

3. **Service Communication**:
   - Configure the client with the server's URL:
   - Set `ServerUrl` to your server App Service URL

### OpenTelemetry in Production

To collect OpenTelemetry data in production:

1. **Set up an OpenTelemetry Collector**:
   - Deploy the OpenTelemetry Collector using Helm or operator
   - Configure it to forward data to your observability backend (Jaeger, Honeycomb, New Relic, etc.)

2. **Configure Environment Variables**:
   Add these environment variables to your deployments:
   
   ```
   OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
   OTEL_RESOURCE_ATTRIBUTES=service.name=benchmarkingwithotel-server,service.namespace=benchmark,deployment.environment=production
   ```

## Monitoring and Diagnostics

### Using Aspire Dashboard with Production Deployments

While Aspire Dashboard is primarily for local development, you can use it to connect to production services for diagnostics:

1. **Run Aspire Dashboard in Standalone Mode**:
   ```
   dotnet run --project Main/Main.AppHost/Main.AppHost.csproj --urls=http://localhost:15888 -- --publisher-urls http://api.example.com
   ```

2. **Configure Production Services**:
   Add the Aspire telemetry publisher endpoints to your production services:
   
   ```
   DOTNET_ASPIRE_PUBLISHER_NAME=BenchmarkingServer
   DOTNET_ASPIRE_PUBLISHER_URLS=http://your-aspire-publisher:15390
   ```
   
3. **Secure the Connection**:
   - Use API keys or authorization headers
   - Implement network security (VPN, private networks)
   - Consider proxying through secured channels

### OpenTelemetry Trace Analysis

The application generates distinct traces for each operation:

1. **Client Traces**:
   - Each API call from client to server creates a separate trace
   - Traces include HTTP request/response details

2. **Server Traces**:
   - API endpoint execution
   - Database operations with SQL queries
   - Error details and status codes

3. **Database Traces**:
   - EF Core operations
   - SQL query execution

### Benchmark Performance Analysis

Analyze benchmark results through:

1. **Console Output**:
   - Operations per second
   - Response times
   - Success/failure rates

2. **Trace Data**:
   - Detailed performance breakdowns by component
   - Bottleneck identification
   - Error patterns

## Troubleshooting

### Common Issues

1. **Connection Issues**:
   - Check Oracle connection string
   - Ensure network connectivity between components
   - Verify service discovery is working

2. **Performance Degradation**:
   - Monitor CPU and memory usage
   - Check for database locks or slowdowns
   - Examine trace data for bottlenecks

3. **Trace Data Not Appearing**:
   - Ensure OpenTelemetry exporter endpoints are correct
   - Check for firewall rules blocking exporter traffic
   - Verify collector is properly configured

### Deployment Checklist

- [ ] Oracle connection string configured
- [ ] Server component can scale horizontally
- [ ] Client benchmark parameters properly tuned
- [ ] OpenTelemetry collection endpoints configured
- [ ] Network security allows component communication
- [ ] Resource allocations sufficient for expected load

## Advanced Configuration

### Scaling the Server Component

The server component is designed to scale horizontally. In production environments:

1. **Kubernetes**:
   ```yaml
   apiVersion: apps/v1
   kind: Deployment
   metadata:
     name: benchmarkingwithotel-server
   spec:
     replicas: 3  # Adjust based on load
   ```

2. **Azure App Service**:
   - Enable scale out in App Service settings
   - Configure auto-scaling rules based on CPU or request metrics

### Custom Benchmark Patterns

To implement custom benchmark patterns:

1. Modify `BenchmarkRunner.cs` to implement your specific pattern
2. Update the worker execution in `Worker.cs`
3. Add new pattern configuration to `appsettings.json`

### Database Considerations

1. **Connection Pooling**:
   - Oracle connection pooling is enabled by default
   - For high throughput, adjust pool settings:
   ```
   Min Pool Size=5;Max Pool Size=100;Connection Timeout=60;
   ```

2. **Resilience**:
   - The application handles common database errors
   - Add retry policies for production:
   ```csharp
   services.AddDbContext<BenchmarkDbContext>((provider, options) => {
       options.UseOracle(connectionString)
              .EnableRetryOnFailure(3);
   });
   ```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.