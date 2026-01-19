# NCode.Mediator

A lightweight, high-performance mediator library for .NET that implements the mediator pattern with support for commands, queries, middleware pipelines, and comprehensive exception handling.

## Features

### Core Functionality
- **Command Dispatching** - Send commands to handlers through a central mediator
- **Command-Only Pipeline** - Commands without responses (zero or more handlers)
- **Command-Response Pipeline** - Commands that return a response (exactly one handler)
- **AOT Compatible** - Generic methods use compile-time types for AOT compatibility
- **Polymorphic Dispatch** - Runtime type resolution when command type is only known at runtime

### Middleware & Processing Pipeline
- **Pre-Processors** - Execute logic before command handling (validation, logging, authorization)
- **Post-Processors** - Execute logic after command handling (audit trails, notifications, caching)
- **Middleware** - Full pipeline control with ability to short-circuit, modify responses, or wrap execution
- **Priority-Based Ordering** - Control execution order via `ISupportMediatorPriority`

### Exception Handling
- **Exception Listeners** - Observe exceptions without handling (logging, telemetry)
- **Exception Handlers** - Handle exceptions and optionally suppress or provide fallback responses
- **Type-Specific Handlers** - Register handlers for specific exception types
- **Fallback Responses** - Command-response pipelines can return fallback values when exceptions are handled

### Architecture
- **Struct-Based Commands** - Commands are value types to avoid heap allocations
- **Dependency Injection** - Full integration with `Microsoft.Extensions.DependencyInjection`
- **Scoped Lifetime** - All services registered with scoped lifetime by default
- **Extensible** - Services use `TryAdd` allowing custom implementations
- **Separate Abstractions Package** - Use `NCode.Mediator.Abstractions` to define commands and handlers in assemblies without taking a dependency on the full mediator implementation

## Installation

```shell
dotnet add package NCode.Mediator
```

Or reference abstractions only (for domain/application layer assemblies):

```shell
dotnet add package NCode.Mediator.Abstractions
```

## Quick Start

### 1. Register Services

```csharp
services.AddMediator();
```

### 2. Define Commands

```csharp
// Command without response
public readonly struct SendEmailCommand : ICommand
{
    public required string To { get; init; }
    public required string Subject { get; init; }
}

// Command with response
public readonly struct GetUserQuery : ICommand<User>
{
    public required int UserId { get; init; }
}
```

### 3. Implement Handlers

```csharp
// Handler for command without response
public class SendEmailHandler : ICommandHandler<SendEmailCommand>
{
    public ValueTask HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
    {
        // Send email...
        return ValueTask.CompletedTask;
    }
}

// Handler for command with response
public class GetUserHandler : ICommandResponseHandler<GetUserQuery, User>
{
    public ValueTask<User> HandleAsync(GetUserQuery command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new User { Id = command.UserId });
    }
}
```

### 4. Dispatch Commands

```csharp
public class MyService(IMediator mediator)
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        // Command without response
        await mediator.SendAsync(new SendEmailCommand 
        { 
            To = "user@example.com", 
            Subject = "Hello" 
        }, cancellationToken);

        // Command with response
        var user = await mediator.SendAsync<GetUserQuery, User>(
            new GetUserQuery { UserId = 123 }, 
            cancellationToken);
    }
}
```

## Advanced Features

### Pre/Post Processors

```csharp
public class ValidationPreProcessor<TCommand> : ICommandPreProcessor<TCommand>
{
    public ValueTask PreProcessAsync(TCommand command, CancellationToken cancellationToken)
    {
        // Validate command before handling
        return ValueTask.CompletedTask;
    }
}

public class AuditPostProcessor<TCommand> : ICommandPostProcessor<TCommand>
{
    public ValueTask PostProcessAsync(TCommand command, CancellationToken cancellationToken)
    {
        // Log command completion
        return ValueTask.CompletedTask;
    }
}
```

### Exception Handling

```csharp
// Listen to exceptions (logging, telemetry)
public class ExceptionLogger : ICommandExceptionListener<MyCommand, Exception>
{
    public ValueTask ListenAsync(MyCommand command, Exception exception, CancellationToken cancellationToken)
    {
        // Log the exception
        return ValueTask.CompletedTask;
    }
}

// Handle exceptions with fallback response
public class NotFoundHandler : ICommandResponseExceptionHandler<GetUserQuery, NotFoundException, User?>
{
    public ValueTask HandleAsync(GetUserQuery command, NotFoundException exception, 
        CommandResponseExceptionHandlerState<User?> state, CancellationToken cancellationToken)
    {
        state.SetHandled(null); // Return null instead of throwing
        return ValueTask.CompletedTask;
    }
}
```

### Priority Ordering

```csharp
public class HighPriorityMiddleware : ICommandMiddleware<MyCommand>, ISupportMediatorPriority
{
    public int MediatorPriority => 100; // Higher values execute first
    
    public async ValueTask HandleAsync(MyCommand command, CommandMiddlewareDelegate next, 
        CancellationToken cancellationToken)
    {
        // Pre-processing
        await next();
        // Post-processing
    }
}
```

## License

Licensed under the Apache License, Version 2.0. See [LICENSE.txt](LICENSE.txt) for details.

## Target Frameworks

- .NET 8.0
- .NET 10.0

## Release Notes
* v1.0.0 - Initial release
