- [C# Server SDK for Amazon GameLift Servers Metrics API](#c-server-sdk-for-amazon-gamelift-servers-metrics-api)
    - [Metrics Setup & Workflow](#metrics-setup--workflow)
      - [Step 1: Initialize the Metrics System](#step-1-initialize-the-metrics-system)
      - [Step 2: Create and Use Metrics](#step-2-create-and-use-metrics)
      - [Example Usage Patterns](#example-usage-patterns)
    - [Metrics Usage & Operations](#metrics-usage--operations)
      - [Gauges](#gauges)
      - [Counters](#counters)
      - [Timers](#timers)
      - [Derived Metrics](#derived-metrics)
      - [Samplers](#samplers)
      - [Tagging](#tagging)
    - [Appendix](#appendix)
      - [Choosing the Right Metric Type](#choosing-the-right-metric-type)
      - [Metric Interface Reference](#metrics-usage--operations)

# C# Server SDK for Amazon GameLift Servers Metrics API

The C# server SDK for Amazon GameLift Servers provides a comprehensive metrics system for collecting and sending
metrics from your game servers hosted on Amazon GameLift Server. These metrics can be integrated with various visualization
and aggregation tools including Amazon Managed Grafana, Amazon Managed Prometheus, Amazon CloudWatch, and other monitoring platforms.
This documentation provides explanations and instructions for advanced configuration and custom metrics implementation.
For a quick start and workflow setup, refer to [METRICS.md](METRICS.md).


## Metrics Setup & Workflow

### Step 1: Initialize the Metrics System

Initialize the metrics system once per application using one of two approaches:

#### Option 1: Initialize Metrics with Default Configuration
The simple approach is to call `InitMetrics()`. This method configures metrics using default environment variables.
See the example below for initializing the metrics system:

```csharp
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model.Metrics;

var metricsOutcome = GameLiftServerAPI.InitMetrics();
if (metricsOutcome.Success)
{
    var metrics = metricsOutcome.Result;
}
```

**Metrics Environment Variables**:
- **GAMELIFT_STATSD_HOST** - StatsD server host (default: localhost)
- **GAMELIFT_STATSD_PORT** - StatsD server port (default: 8125)
- **GAMELIFT_CRASH_REPORTER_HOST** - Crash reporter host (default: localhost)
- **GAMELIFT_CRASH_REPORTER_PORT** - Crash reporter port (default: 8126)
- **GAMELIFT_FLUSH_INTERVAL_MS** - Metrics flush interval in milliseconds (default: 10000)
- **GAMELIFT_MAX_PACKET_SIZE** - Maximum packet size in bytes (default: 512)


#### Option 2: Initialize Metrics with Custom Configuration

For applications requiring specific configuration parameters, use `InitMetrics()` with `MetricsParameters`:

```csharp
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model.Metrics;

// Initialize metrics with custom parameters
var metricsParams = new MetricsParameters("localhost", 8125, "crash-host", 9999, 1000, 1024);
var customMetricsOutcome = GameLiftServerAPI.InitMetrics(metricsParams);
if (customMetricsOutcome.Success)
{
    var metrics = customMetricsOutcome.Result;
}
```

**Metrics Parameters**:
```csharp
MetricsParameters(
    string statsdHost,        // StatsD server hostname
    int statsdPort,           // StatsD server port
    string crashReporterHost, // Crash reporter hostname
    int crashReporterPort,    // Crash reporter port
    int flushIntervalMs,      // Flush interval in milliseconds
    int maxPacketSize         // Maximum UDP packet size in bytes
)
```


### Step 2: Create and Use Metrics

```csharp
// Create metrics once during initialization
var playerJoinsCounter = metrics.NewCounter("player_joins").Build();
var activePlayersGauge = metrics.NewGauge("server_players").Build();
var operationTimer = metrics.NewTimer("operation_duration").Build();

// Use metrics throughout your application
playerJoinsCounter.Increment();           // Count events
activePlayersGauge.Set(42);               // Track current values
operationTimer.Set(125);                  // Record timing (milliseconds)
```

### Example Usage Patterns

```csharp
public class GameServer
{
    private MetricsManager metrics;
    private ICounter playerJoinsCounter;
    private IGauge activePlayersGauge;
    private ITimer matchDurationTimer;

    public void Initialize()
    {
        // Initialize metrics using GameLift Server API
        var metricsOutcome = GameLiftServerAPI.InitMetrics();
        if (metricsOutcome.Success)
        {
            metrics = metricsOutcome.Result;
        }

        // Create metrics
        playerJoinsCounter = metrics.NewCounter("player_joins").Build();
        activePlayersGauge = metrics.NewGauge("server_players").Build();
        matchDurationTimer = metrics.NewTimer("match_duration").Build();
    }

    public void OnPlayerJoin() => playerJoinsCounter.Increment();
    public void OnPlayerLeave() => activePlayersGauge.Decrement();
    public void OnMatchEnd(TimeSpan duration) => matchDurationTimer.Set(duration.TotalMilliseconds);
}
```


## Metrics Usage & Operations

Understanding when to use each metric type is crucial for effective monitoring. Each type serves different purposes and provides different insights into your game server's behavior.

### Gauges

Gauges represent metrics that track the current value of something over time. They maintain state and are ideal for
measurements like player count, memory usage, connection count, or any value that can go up and down.

**When to use:** For values that can go up and down, representing current state or levels. Perfect for resource monitoring and current conditions.

**Key characteristics:**

- Values can increase, decrease, or be set to specific values
- Represent "right now" snapshots
- Ideal for monitoring capacity, load, and current levels
- Best for answering "How much right now?" and "What's the current state?"

```csharp
var gauge = metrics.NewGauge("server_load").Build();
gauge.Set(75.5);           // Set absolute value
gauge.Increment();         // Add 1
gauge.Add(10);             // Add 10
gauge.Reset();             // Reset to 0
```

**Common use cases:**

```csharp
// Player and session tracking
var activePlayersGauge = metrics.NewGauge("server_players").Build();
var activeMatchesGauge = metrics.NewGauge("active_matches").Build();
var queueSizeGauge = metrics.NewGauge("matchmaking_queue_size").Build();

// Resource monitoring
var memoryUsageGauge = metrics.NewGauge("memory_usage_mb").Build();
var cpuUsageGauge = metrics.NewGauge("cpu_usage_percent").Build();
var diskSpaceGauge = metrics.NewGauge("disk_free_gb").Build();

// Game world state
var serverHealthGauge = metrics.NewGauge("server_health_score").Build();
var networkLatencyGauge = metrics.NewGauge("average_ping_ms").Build();
var frameRateGauge = metrics.NewGauge("current_fps").Build();
```

**Why gauges are essential:**

- **Capacity planning**: Monitor resource usage to prevent overload
- **Real-time status**: Know current state for operational decisions
- **Threshold alerting**: Alert when resources get too high/low
- **Load balancing**: Make decisions based on current load


### Counters

Counters represent metrics that track cumulative occurrences over time. Unlike gauges, counters only increase and are ideal for measuring
events like bytes sent, packets received, function calls, or any event that happens repeatedly. Counters accumulate values and never decrease.

**When to use:** For events that happen over time and values that only increase. Perfect for tracking totals, rates, and occurrences.

**Key characteristics:**

- Values only increase (never decrease)
- Ideal for calculating rates (events per second/minute)
- Automatically reset when your application restarts
- Best for answering "How many?" and "How often?"

```csharp
var counter = metrics.NewCounter("player_deaths").Build();
counter.Increment();        // Add 1
counter.Add(5);             // Add 5
```

**Common use cases:**

```csharp
// Player activity tracking
var playerJoinsCounter = metrics.NewCounter("player_joins").Build();
var playerDeathsCounter = metrics.NewCounter("player_deaths").Build();
var matchesStartedCounter = metrics.NewCounter("matches_started").Build();

// Performance and error tracking
var apiCallsCounter = metrics.NewCounter("api_calls_total").Build();
var errorCounter = metrics.NewCounter("errors_total").AddTag("error_type:network").Build();
var gcCollectionsCounter = metrics.NewCounter("garbage_collections").Build();

// Game-specific events
var itemsPickedUpCounter = metrics.NewCounter("items_picked_up").AddTag("item_type:weapon").Build();
var abilitiesUsedCounter = metrics.NewCounter("abilities_used").AddTag("ability:fireball").Build();
```

**Why counters are powerful:**

- **Rate calculation**: Monitoring systems can calculate "joins per minute" from total joins
- **Trend analysis**: Track growth patterns over time


### Timers
Timers represent duration measurements. They are ideal for tracking execution time, session duration, response times,
or any time-based metrics. Timers support derived metrics like mean, percentiles, and latest values for statistical analysis.

**When to use:** For measuring how long operations take. Essential for performance monitoring, SLA tracking, and identifying bottlenecks.

**Key characteristics:**

- Record duration of operations in milliseconds
- Perfect for performance analysis and optimization
- Best for answering "How long?" and "How fast?"

```csharp
var timer = metrics.NewTimer("api_response").Build();
timer.Set(250);            // Record 250ms
```

**Common use cases:**

```csharp
// API and network performance
var apiResponseTimer = metrics.NewTimer("api_response_time")
    .AddTag("endpoint:player_data").Build();
var databaseQueryTimer = metrics.NewTimer("database_query_duration")
    .AddTag("query_type:player_stats").Build();

// Game operations
var matchDurationTimer = metrics.NewTimer("match_duration_seconds").Build();
var frameTimeTimer = metrics.NewTimer("frame_processing_time_ms").Build();
var playerRespawnTimer = metrics.NewTimer("player_respawn_time_ms").Build();

// System operations
var startupTimer = metrics.NewTimer("server_startup_time").Build();
var saveGameTimer = metrics.NewTimer("save_game_duration").Build();
```

**Advanced timer usage with derived metrics:**

```csharp
// Comprehensive performance monitoring
var gameLoopTimer = metricsManager
    .NewTimer("game_loop_duration")
    .AddDerivedMetric(new Min())      // Fastest loop
    .AddDerivedMetric(new Max())      // Slowest loop
    .AddDerivedMetric(new Mean())     // Average performance
    .AddDerivedMetric(new Count())    // Total iterations
    .Build();

// Example usage in game loop
var stopwatch = Stopwatch.StartNew();
// ... game loop logic ...
gameLoopTimer.Set(stopwatch.ElapsedMilliseconds);
```

**Why timers are critical:**

- **Performance optimization**: Identify slow operations that need improvement
- **SLA monitoring**: Ensure response times meet requirements
- **Bottleneck detection**: Find what's slowing down your game
- **Trend analysis**: See if performance degrades over time


### Derived Metrics
Derived metrics automatically calculate statistical values from your metric samples.

**Available derived metrics:**
- Sum - Total of all values
- Latest - Most recent value
- Mean - Average of all values
- Min - Minimum value
- Max - Maximum value 
- Count - Number of samples
- Percentile - Percentile calculations

```csharp
// Add derived metrics to timer
var timer = metrics
    .NewTimer("api_calls")
    .AddDerivedMetric(new Min())
    .AddDerivedMetric(new Max())
    .AddDerivedMetric(new Mean())
    .Build();
```

#### Custom Derived Metrics

You can create custom derived metrics by implementing the `IDerivedMetric` interface:

```csharp
// Custom derived metric for calculating median
public class Median : BaseDerivedMetric, IDerivedMetric
{
    public override string Name => "median";

    public override double CalculateValue(IList<double> samples)
    {
        if (samples == null || samples.Count == 0)
        {
            return 0;
        }
        var sortedValues = samples.OrderBy(v => v).ToList();
        int mid = sortedValues.Count / 2;
        if (sortedValues.Count % 2 == 0)
        {
            return (sortedValues[mid - 1] + sortedValues[mid]) / 2.0;
        }
        return sortedValues[mid];
    }
}

// Using custom derived metrics
var responseTimeTimer = metrics
    .NewTimer("api_response_time")
    .AddDerivedMetric(new Max())          // Built-in
    .AddDerivedMetric(new Median())       // Custom
    .Build();
```

### Samplers
Samplers reduce metric volume by only recording a percentage of events. This is essential for high-frequency metrics to prevent overwhelming your monitoring system.

```csharp
// Use sampling to reduce metric volume
var highFreqCounter = metrics
    .NewCounter("bullet_fired")
    .SetSampleRate(new SampleFractional(0.1))  // Only sample 10% of events
    .Build();
    
// Sample all events (default)
var normalCounter = metrics
    .NewCounter("player_joins")
    .Build();
```

### Tagging

Tags add dimensions to your metrics, enabling filtering, grouping, and detailed analysis. Tags help organize metrics and provide context.

#### Individual Metric Tags
```csharp
// Tags help organize and filter metrics
var taggedCounter = metrics
    .NewCounter("player_actions")
    .AddTag("action_type:jump")
    .AddTag("level:tutorial")
    .Build();
```

#### Global Tags

Apply common dimensions to all metrics:

```csharp
// Configure comprehensive global tags
var metricsOutcome = GameLiftServerAPI.InitMetrics();
if (metricsOutcome.Success)
{
    var metrics = metricsOutcome.Result;
    
    // Add multiple global tags at once using a list
    var globalTags = new List<string>
    {
        "environment:production",
        "region:us-west-2",
        "server_type:dedicated",
        "game_version:2.1.0",
        "fleet_id:fleet-123",
        "instance_type:c5.large"
    };
    
    metrics.AddGlobalTags(globalTags);
}

// Or adding global tags individually
var metricsOutcome = GameLiftServerAPI.InitMetrics();
if (metricsOutcome.Success)
{
    var metrics = metricsOutcome.Result;
    
    // Add global tags individually
    metrics.AddGlobalTag("environment:production");
    metrics.AddGlobalTag("region:us-west-2");
    metrics.AddGlobalTag("server_type:dedicated");
    metrics.AddGlobalTag("game_version:2.1.0");
    metrics.AddGlobalTag("fleet_id:fleet-123");
}

// All metrics inherit global tags
var playerCounter = metrics
    .NewCounter("player_actions")
    .AddTag("action_type:jump")  // Combined with global tags
    .Build();
```


### Best Practices
1. **Initialize Once**: Call `GameLiftServerAPI.InitMetrics()` only once during application startup
2. **Metric Naming**: Use consistent, descriptive names with underscores (e.g., server_player, server_load)
3. **Strategic Tagging**: Add meaningful dimensions without over-tagging (keep cardinality reasonable)
4. **Sample Rates**: Use appropriate sampling for high-frequency events to reduce overhead
5. **Global Tags**: Configure common dimensions at the global level
6. **Performance**: Create metrics during initialization, not per-use, for better performance


## Appendix
### Choosing the Right Metric Type
The different metric types (gauge, counter, and timer) serve different purposes and have their own appropriate use cases.

| Scenario                | Metric Type   | Reason                           |
|-------------------------|---------------|----------------------------------|
| Player joins the server | Counter       | Event that accumulates over time |
| Current players online  | Gauge         | State that fluctuates up/down    |
| Time to process a match | Timer         | Duration measurement             |
| Total matches played    | Counter       | Accumulating count               |
| Server CPU usage        | Gauge         | Current resource level           |
| Database query speed    | Timer         | Performance measurement          |
| Errors encountered      | Counter       | Events to track and rate         |
| Memory consumption      | Gauge         | Current resource state           |
| Player session length   | Timer         | Duration measurement             |

**Pro tip:** Many scenarios benefit from multiple metric types:

```csharp
// Player connection monitoring
var connectionsCounter = metrics.NewCounter("connections_total").Build();     // How many total
var activeConnectionsGauge = metrics.NewGauge("active_connections").Build();  // How many now
var connectionTimeTimer = metrics.NewTimer("connection_time_ms").Build();     // How long to connect

public void OnPlayerConnect()
{
    var stopwatch = Stopwatch.StartNew();
    // ... connection logic ...

    connectionsCounter.Increment();           // Count the event
    activeConnectionsGauge.Increment();       // Update current state
    connectionTimeTimer.Set(stopwatch.ElapsedMilliseconds); // Record duration
}
```

### Metric Interface Reference

The following tables document the public interfaces for each metric type in this SDK.

#### ICounter Interface

| Method           | Parameters                                    | Return Type      | Description                                   |
|------------------|-----------------------------------------------|------------------|-----------------------------------------------|
| `Increment()`    | None                                          | `GenericOutcome` | Increments the counter by 1                   |
| `Add(int value)` | `value`: Amount to add (must be non-negative) | `GenericOutcome` | Increments the counter by the specified value |

**Usage Example:**

```csharp
var counter = metrics.NewCounter("player_joins").Build();
counter.Increment();        // Add 1
counter.Add(5);             // Add 5
```

#### IGauge Interface

| Method                   | Parameters                         | Return Type      | Description                                                |
|--------------------------|------------------------------------|------------------|------------------------------------------------------------|
| `Set(double value)`      | `value`: The absolute value to set | `GenericOutcome` | Sets the gauge to the specified value                      |
| `Add(double value)`      | `value`: Amount to add             | `GenericOutcome` | Adds the specified value to the current gauge value        |
| `Subtract(double value)` | `value`: Amount to subtract        | `GenericOutcome` | Subtracts the specified value from the current gauge value |
| `Increment()`            | None                               | `GenericOutcome` | Increments the gauge by 1                                  |
| `Decrement()`            | None                               | `GenericOutcome` | Decrements the gauge by 1                                  |
| `Reset()`                | None                               | `GenericOutcome` | Resets the gauge to zero                                   |

**Usage Example:**

```csharp
var gauge = metrics.NewGauge("active_players").Build();
gauge.Set(42);             // Set to 42
gauge.Add(5);              // Now 47
gauge.Subtract(2);         // Now 45
gauge.Increment();         // Now 46
gauge.Decrement();         // Now 45
gauge.Reset();             // Now 0
```

#### ITimer Interface

| Method                     | Parameters                                         | Return Type      | Description                  |
|----------------------------|----------------------------------------------------|------------------|------------------------------|
| `Set(double milliseconds)` | `milliseconds`: Duration in milliseconds to record | `GenericOutcome` | Records a timing measurement |

**Usage Example:**

```csharp
var timer = metrics.NewTimer("operation_duration").Build();
timer.Set(125.5);          // Record 125.5 milliseconds
```

#### Common Interface Methods

All metric types inherit from `MetricBase` and implement the following common interface:

| Method    | Parameters   | Return Type      | Description                                     |
|-----------|--------------|------------------|-------------------------------------------------|
| `Flush()` | None         | `GenericOutcome` | Flushes the metric data to the GameLift service |

#### Return Type: GenericOutcome

All metric operations return a `GenericOutcome` object that indicates success or failure:

- **Success**: `outcome.Success` is `true`
- **Failure**: `outcome.Success` is `false`, with error details in `outcome.Error`

**Error Handling Example:**

```csharp
var outcome = counter.Increment();
if (!outcome.Success)
{
    Console.WriteLine($"Failed to increment counter: {outcome.Error.ErrorMessage}");
}
```

#### Method Validation

- **All methods**: Return appropriate error outcomes for configuration or submission failures
- **Exception Safety**: All public methods catch exceptions and return error outcomes rather than throwing

