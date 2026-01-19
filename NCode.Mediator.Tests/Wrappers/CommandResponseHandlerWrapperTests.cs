#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Moq;
using NCode.Mediator.Middleware;
using NCode.Mediator.Wrappers;

namespace NCode.Mediator.Tests.Wrappers;

public class CommandResponseHandlerWrapperTests
{
    #region Test Types

    public readonly struct TestCommand : ICommand<string>
    {
        public string? Value { get; init; }
    }

    public readonly struct IntCommand : ICommand<int>
    {
        public int Input { get; init; }
    }

    private class NonPrioritizedItem
    {
        public int Id { get; init; }
    }

    private class PrioritizedItem : ISupportMediatorPriority
    {
        public int Id { get; init; }
        public int MediatorPriority { get; init; }
    }

    private class PrioritizedHandler : ICommandResponseHandler<TestCommand, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public int HandlerId { get; init; }
        public string ResponseToReturn { get; init; } = "default";
        public List<TestCommand> ReceivedCommands { get; } = [];

        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            ReceivedCommands.Add(command);
            return ValueTask.FromResult(ResponseToReturn);
        }
    }

    private class PrioritizedMiddleware(List<string> executionLog)
        : ICommandResponseMiddleware<TestCommand, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public int MiddlewareId { get; init; }

        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandResponseMiddlewareDelegate<string> next,
            CancellationToken cancellationToken)
        {
            executionLog.Add($"Before-{MiddlewareId}");
            var result = await next();
            executionLog.Add($"After-{MiddlewareId}");
            return result;
        }
    }

    private class TrackingMiddleware(List<int> executionOrder, int middlewareId)
        : ICommandResponseMiddleware<TestCommand, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandResponseMiddlewareDelegate<string> next,
            CancellationToken cancellationToken)
        {
            executionOrder.Add(middlewareId);
            var result = await next();
            executionOrder.Add(-middlewareId);
            return result;
        }
    }

    private class LoggingHandler(List<string> executionLog, string response)
        : ICommandResponseHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            executionLog.Add("Handler");
            return ValueTask.FromResult(response);
        }
    }

    private class ShortCircuitMiddleware(string response)
        : ICommandResponseMiddleware<TestCommand, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public ValueTask<string> HandleAsync(
            TestCommand command,
            CommandResponseMiddlewareDelegate<string> next,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(response);
        }
    }

    private class ResponseModifyingMiddleware(string prefix, string suffix)
        : ICommandResponseMiddleware<TestCommand, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandResponseMiddlewareDelegate<string> next,
            CancellationToken cancellationToken)
        {
            var result = await next();
            return $"{prefix}{result}{suffix}";
        }
    }

    #endregion

    #region Sort Tests

    [Fact]
    public void Sort_WithEmptyCollection_ReturnsEmptyCollection()
    {
        IEnumerable<PrioritizedItem> items = [];

        var result = CommandResponseHandlerWrapper<TestCommand, string>.Sort(items).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Sort_WithNonPrioritizedItems_ReturnsOriginalOrder()
    {
        IEnumerable<NonPrioritizedItem> items =
        [
            new NonPrioritizedItem { Id = 1 },
            new NonPrioritizedItem { Id = 2 },
            new NonPrioritizedItem { Id = 3 }
        ];

        var result = CommandResponseHandlerWrapper<TestCommand, string>.Sort(items).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(2, result[1].Id);
        Assert.Equal(3, result[2].Id);
    }

    [Fact]
    public void Sort_WithPrioritizedItems_SortsDescendingByPriority()
    {
        IEnumerable<PrioritizedItem> items =
        [
            new PrioritizedItem { Id = 1, MediatorPriority = 10 },
            new PrioritizedItem { Id = 2, MediatorPriority = 100 },
            new PrioritizedItem { Id = 3, MediatorPriority = 50 }
        ];

        var result = CommandResponseHandlerWrapper<TestCommand, string>.Sort(items).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }

    [Fact]
    public void Sort_WithMixedPrioritizedAndNonPrioritizedItems_TreatsNonPrioritizedAsZero()
    {
        var items = new object[]
        {
            new NonPrioritizedItem { Id = 1 },
            new PrioritizedItem { Id = 2, MediatorPriority = 100 },
            new NonPrioritizedItem { Id = 3 },
            new PrioritizedItem { Id = 4, MediatorPriority = -50 }
        };

        var result = CommandResponseHandlerWrapper<TestCommand, string>.Sort(items).ToList();

        Assert.Equal(4, result.Count);
        Assert.Equal(2, ((dynamic)result[0]).Id);
        Assert.Equal(1, ((dynamic)result[1]).Id);
        Assert.Equal(3, ((dynamic)result[2]).Id);
        Assert.Equal(4, ((dynamic)result[3]).Id);
    }

    [Fact]
    public void Sort_WithNegativePriorities_SortsCorrectly()
    {
        IEnumerable<PrioritizedItem> items =
        [
            new PrioritizedItem { Id = 1, MediatorPriority = -100 },
            new PrioritizedItem { Id = 2, MediatorPriority = 0 },
            new PrioritizedItem { Id = 3, MediatorPriority = -50 }
        ];

        var result = CommandResponseHandlerWrapper<TestCommand, string>.Sort(items).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }

    #endregion

    #region WrapMiddleware Tests

    [Fact]
    public async Task WrapMiddleware_CreatesWorkingDelegate()
    {
        var executionLog = new List<string>();
        var middleware = new PrioritizedMiddleware(executionLog) { MiddlewareId = 1 };

        var factory = CommandResponseHandlerWrapper<TestCommand, string>.WrapMiddleware(middleware);
        var wrappedDelegate = factory((_, _) => ValueTask.FromResult("inner-result"));

        var command = new TestCommand { Value = "test" };
        var result = await wrappedDelegate(command, CancellationToken.None);

        Assert.Equal("inner-result", result);
        Assert.Equal(2, executionLog.Count);
        Assert.Equal("Before-1", executionLog[0]);
        Assert.Equal("After-1", executionLog[1]);
    }

    [Fact]
    public async Task WrapMiddleware_PassesCommandToMiddleware()
    {
        TestCommand? receivedCommand = null;
        var mockMiddleware = new Mock<ICommandResponseMiddleware<TestCommand, string>>();

        mockMiddleware
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CommandResponseMiddlewareDelegate<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CommandResponseMiddlewareDelegate<string>, CancellationToken>(
                (cmd, _, _) => receivedCommand = cmd)
            .Returns(ValueTask.FromResult("result"))
            .Verifiable();

        var factory = CommandResponseHandlerWrapper<TestCommand, string>.WrapMiddleware(mockMiddleware.Object);
        var wrappedDelegate = factory((_, _) => ValueTask.FromResult("inner-result"));

        var command = new TestCommand { Value = "unique-value" };
        await wrappedDelegate(command, CancellationToken.None);

        Assert.NotNull(receivedCommand);
        Assert.Equal("unique-value", receivedCommand.Value.Value);
        mockMiddleware.Verify();
    }

    [Fact]
    public async Task WrapMiddleware_PassesCancellationTokenToMiddleware()
    {
        CancellationToken receivedToken = default;
        var mockMiddleware = new Mock<ICommandResponseMiddleware<TestCommand, string>>();
        using var cts = new CancellationTokenSource();

        mockMiddleware
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CommandResponseMiddlewareDelegate<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CommandResponseMiddlewareDelegate<string>, CancellationToken>(
                (_, _, token) => receivedToken = token)
            .Returns(ValueTask.FromResult("result"))
            .Verifiable();

        var factory = CommandResponseHandlerWrapper<TestCommand, string>.WrapMiddleware(mockMiddleware.Object);
        var wrappedDelegate = factory((_, _) => ValueTask.FromResult("inner-result"));

        var command = new TestCommand { Value = "test" };
        await wrappedDelegate(command, cts.Token);

        Assert.Equal(cts.Token, receivedToken);
        mockMiddleware.Verify();
    }

    [Fact]
    public async Task WrapMiddleware_NextDelegateInvokesInnerDelegate()
    {
        var innerInvoked = false;
        var mockMiddleware = new Mock<ICommandResponseMiddleware<TestCommand, string>>();

        mockMiddleware
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CommandResponseMiddlewareDelegate<string>>(), It.IsAny<CancellationToken>()))
            .Returns<TestCommand, CommandResponseMiddlewareDelegate<string>, CancellationToken>(
                async (_, next, _) => await next())
            .Verifiable();

        var factory = CommandResponseHandlerWrapper<TestCommand, string>.WrapMiddleware(mockMiddleware.Object);
        var wrappedDelegate = factory((_, _) =>
        {
            innerInvoked = true;
            return ValueTask.FromResult("inner-result");
        });

        var command = new TestCommand { Value = "test" };
        var result = await wrappedDelegate(command, CancellationToken.None);

        Assert.True(innerInvoked);
        Assert.Equal("inner-result", result);
        mockMiddleware.Verify();
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WithNoMiddleware_InvokesHandlerDirectly()
    {
        var handler = new PrioritizedHandler { ResponseToReturn = "handler-response" };
        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);
        var command = new TestCommand { Value = "test" };

        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal("handler-response", result);
        Assert.Single(handler.ReceivedCommands);
    }

    [Fact]
    public async Task HandleAsync_WithSingleMiddleware_InvokesMiddlewareAndHandler()
    {
        var executionLog = new List<string>();
        var middleware = new PrioritizedMiddleware(executionLog) { MiddlewareId = 1 };
        var handler = new LoggingHandler(executionLog, "handler-response");
        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [middleware];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);
        var command = new TestCommand { Value = "test" };

        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal("handler-response", result);
        Assert.Equal(3, executionLog.Count);
        Assert.Equal("Before-1", executionLog[0]);
        Assert.Equal("Handler", executionLog[1]);
        Assert.Equal("After-1", executionLog[2]);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleMiddlewares_ExecutesInCorrectOrder()
    {
        var executionLog = new List<string>();
        var middleware1 = new PrioritizedMiddleware(executionLog) { MiddlewareId = 1, MediatorPriority = 100 };
        var middleware2 = new PrioritizedMiddleware(executionLog) { MiddlewareId = 2, MediatorPriority = 50 };
        var handler = new LoggingHandler(executionLog, "handler-response");

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [middleware1, middleware2];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);
        var command = new TestCommand { Value = "test" };

        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        // Aggregate wraps middlewares in sorted order (descending by priority),
        // so higher priority middleware wraps inner (executes last as outer wrapper)
        // Execution order: middleware2 (lower priority, outermost) → middleware1 (higher priority) → handler
        Assert.Equal("handler-response", result);
        Assert.Equal(5, executionLog.Count);
        Assert.Equal("Before-2", executionLog[0]);
        Assert.Equal("Before-1", executionLog[1]);
        Assert.Equal("Handler", executionLog[2]);
        Assert.Equal("After-1", executionLog[3]);
        Assert.Equal("After-2", executionLog[4]);
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectCommandToHandler()
    {
        TestCommand? receivedCommand = null;
        var mockHandler = new Mock<ICommandResponseHandler<TestCommand, string>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CancellationToken>((cmd, _) => receivedCommand = cmd)
            .ReturnsAsync("response")
            .Verifiable();

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(mockHandler.Object, middlewares);

        var command = new TestCommand { Value = "unique-command-value" };
        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(receivedCommand);
        Assert.Equal("unique-command-value", receivedCommand.Value.Value);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectCancellationToken()
    {
        CancellationToken receivedToken = default;
        var mockHandler = new Mock<ICommandResponseHandler<TestCommand, string>>();
        using var cts = new CancellationTokenSource();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CancellationToken>((_, token) => receivedToken = token)
            .ReturnsAsync("response")
            .Verifiable();

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(mockHandler.Object, middlewares);

        var command = new TestCommand { Value = "test" };
        await wrapper.HandleAsync(command, cts.Token);

        Assert.Equal(cts.Token, receivedToken);
        mockHandler.Verify();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void Wrapper_ImplementsICommandResponseHandlerWrapperGeneric()
    {
        var mockHandler = new Mock<ICommandResponseHandler<TestCommand, string>>();
        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(mockHandler.Object, middlewares);

        Assert.IsAssignableFrom<ICommandResponseHandlerWrapper<TestCommand, string>>(wrapper);
    }

    [Fact]
    public void Wrapper_ImplementsICommandResponseHandlerWrapperNonGeneric()
    {
        var mockHandler = new Mock<ICommandResponseHandler<TestCommand, string>>();
        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(mockHandler.Object, middlewares);

        Assert.IsAssignableFrom<ICommandResponseHandlerWrapper<string>>(wrapper);
    }

    [Fact]
    public async Task PolymorphicDispatch_ThroughNonGenericInterface_WorksCorrectly()
    {
        var handler = new PrioritizedHandler { ResponseToReturn = "polymorphic-response" };
        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        ICommandResponseHandlerWrapper<string> wrapper =
            new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);

        ICommand<string> command = new TestCommand { Value = "test" };
        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal("polymorphic-response", result);
        Assert.Single(handler.ReceivedCommands);
    }

    [Fact]
    public async Task StronglyTypedDispatch_ThroughGenericInterface_WorksCorrectly()
    {
        var handler = new PrioritizedHandler { ResponseToReturn = "typed-response" };
        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        ICommandResponseHandlerWrapper<TestCommand, string> wrapper =
            new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);

        var command = new TestCommand { Value = "test" };
        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal("typed-response", result);
        Assert.Single(handler.ReceivedCommands);
    }

    #endregion

    #region Middleware Pipeline Tests

    [Fact]
    public async Task HandleAsync_MultipleMiddlewares_ExecuteInPriorityOrder()
    {
        var executionOrder = new List<int>();
        var middleware1 = new TrackingMiddleware(executionOrder, 1) { MediatorPriority = 10 };
        var middleware2 = new TrackingMiddleware(executionOrder, 2) { MediatorPriority = 100 };
        var middleware3 = new TrackingMiddleware(executionOrder, 3) { MediatorPriority = 50 };
        var handler = new PrioritizedHandler { ResponseToReturn = "result" };

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [middleware1, middleware2, middleware3];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        // Sorted descending: [middleware2(100), middleware3(50), middleware1(10)]
        // Aggregate wraps in order, so lower priority becomes outermost wrapper
        // Execution: middleware1 (outermost) → middleware3 → middleware2 (innermost) → handler
        Assert.Equal(6, executionOrder.Count);
        Assert.Equal(1, executionOrder[0]);
        Assert.Equal(3, executionOrder[1]);
        Assert.Equal(2, executionOrder[2]);
        Assert.Equal(-2, executionOrder[3]);
        Assert.Equal(-3, executionOrder[4]);
        Assert.Equal(-1, executionOrder[5]);
    }

    [Fact]
    public async Task HandleAsync_MiddlewareShortCircuits_HandlerNotInvoked()
    {
        var executionLog = new List<string>();
        var shortCircuitMiddleware = new ShortCircuitMiddleware("short-circuit-response") { MediatorPriority = 100 };
        var handler = new LoggingHandler(executionLog, "handler-response");

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [shortCircuitMiddleware];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);
        var command = new TestCommand { Value = "test" };

        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal("short-circuit-response", result);
        Assert.Empty(executionLog);
    }

    [Fact]
    public async Task HandleAsync_MiddlewareModifiesResponse()
    {
        var modifyingMiddleware = new ResponseModifyingMiddleware("[", "]") { MediatorPriority = 100 };
        var handler = new PrioritizedHandler { ResponseToReturn = "inner" };

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [modifyingMiddleware];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);
        var command = new TestCommand { Value = "test" };

        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal("[inner]", result);
    }

    [Fact]
    public async Task HandleAsync_MultipleMiddlewaresModifyResponse_ComposesCorrectly()
    {
        var outerMiddleware = new ResponseModifyingMiddleware("<", ">") { MediatorPriority = 10 };
        var innerMiddleware = new ResponseModifyingMiddleware("[", "]") { MediatorPriority = 100 };
        var handler = new PrioritizedHandler { ResponseToReturn = "core" };

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [outerMiddleware, innerMiddleware];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(handler, middlewares);
        var command = new TestCommand { Value = "test" };

        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        // Outer middleware (priority 10) wraps everything, inner middleware (priority 100) wraps handler
        // Execution: outerMiddleware → innerMiddleware → handler → innerMiddleware-post → outerMiddleware-post
        // innerMiddleware wraps "core" → "[core]"
        // outerMiddleware wraps "[core]" → "<[core]>"
        Assert.Equal("<[core]>", result);
    }

    #endregion

    #region Response Type Tests

    [Fact]
    public async Task HandleAsync_WithValueTypeResponse_ReturnsCorrectly()
    {
        var mockHandler = new Mock<ICommandResponseHandler<IntCommand, int>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<IntCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42)
            .Verifiable();

        IEnumerable<ICommandResponseMiddleware<IntCommand, int>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<IntCommand, int>(mockHandler.Object, middlewares);

        var command = new IntCommand { Input = 10 };
        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal(42, result);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithNullableReferenceTypeResponse_ReturnsNull()
    {
        var mockHandler = new Mock<ICommandResponseHandler<TestCommand, string>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null!)
            .Verifiable();

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(mockHandler.Object, middlewares);

        var command = new TestCommand { Value = "test" };
        var result = await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
        mockHandler.Verify();
    }

    #endregion

    #region Exception Propagation Tests

    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_PropagatesException()
    {
        var mockHandler = new Mock<ICommandResponseHandler<TestCommand, string>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler error"))
            .Verifiable();

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(mockHandler.Object, middlewares);

        var command = new TestCommand { Value = "test" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => wrapper.HandleAsync(command, CancellationToken.None).AsTask());

        Assert.Equal("Handler error", exception.Message);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WhenMiddlewareThrows_PropagatesException()
    {
        var mockMiddleware = new Mock<ICommandResponseMiddleware<TestCommand, string>>();
        var mockHandler = new Mock<ICommandResponseHandler<TestCommand, string>>();

        mockMiddleware
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CommandResponseMiddlewareDelegate<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Middleware error"))
            .Verifiable();

        IEnumerable<ICommandResponseMiddleware<TestCommand, string>> middlewares = [mockMiddleware.Object];
        var wrapper = new CommandResponseHandlerWrapper<TestCommand, string>(mockHandler.Object, middlewares);

        var command = new TestCommand { Value = "test" };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => wrapper.HandleAsync(command, CancellationToken.None).AsTask());

        Assert.Equal("Middleware error", exception.Message);
        mockMiddleware.Verify();
    }

    #endregion
}
