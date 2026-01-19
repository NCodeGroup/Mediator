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

public class CommandHandlerWrapperTests
{
    #region Test Types

    public readonly struct TestCommand : ICommand
    {
        public string? Value { get; init; }
    }

    private class PrioritizedHandler : ICommandHandler<TestCommand>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public List<TestCommand> ReceivedCommands { get; } = [];

        public ValueTask HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            ReceivedCommands.Add(command);
            return ValueTask.CompletedTask;
        }
    }

    private class PrioritizedMiddleware : ICommandMiddleware<TestCommand>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public List<string> ExecutionLog { get; }
        public int MiddlewareId { get; init; }

        public PrioritizedMiddleware(List<string> executionLog)
        {
            ExecutionLog = executionLog;
        }

        public async ValueTask HandleAsync(
            TestCommand command,
            CommandMiddlewareDelegate next,
            CancellationToken cancellationToken)
        {
            ExecutionLog.Add($"Before-{MiddlewareId}");
            await next();
            ExecutionLog.Add($"After-{MiddlewareId}");
        }
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

    #endregion

    #region Sort Tests

    [Fact]
    public void Sort_WithEmptyCollection_ReturnsEmptyCollection()
    {
        IEnumerable<PrioritizedItem> items = [];

        var result = CommandHandlerWrapper<TestCommand>.Sort(items).ToList();

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

        var result = CommandHandlerWrapper<TestCommand>.Sort(items).ToList();

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

        var result = CommandHandlerWrapper<TestCommand>.Sort(items).ToList();

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

        var result = CommandHandlerWrapper<TestCommand>.Sort(items).ToList();

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

        var result = CommandHandlerWrapper<TestCommand>.Sort(items).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }

    #endregion

    #region WrapRoot Tests

    [Fact]
    public async Task WrapRoot_WithNoHandlers_CompletesWithoutError()
    {
        IEnumerable<ICommandHandler<TestCommand>> handlers = [];
        var rootDelegate = CommandHandlerWrapper<TestCommand>.WrapRoot(handlers);
        var command = new TestCommand { Value = "test" };

        await rootDelegate(command, CancellationToken.None);
    }

    [Fact]
    public async Task WrapRoot_WithSingleHandler_InvokesHandler()
    {
        var handler = new PrioritizedHandler();
        IEnumerable<ICommandHandler<TestCommand>> handlers = [handler];
        var rootDelegate = CommandHandlerWrapper<TestCommand>.WrapRoot(handlers);
        var command = new TestCommand { Value = "test" };

        await rootDelegate(command, CancellationToken.None);

        Assert.Single(handler.ReceivedCommands);
        Assert.Equal("test", handler.ReceivedCommands[0].Value);
    }

    [Fact]
    public async Task WrapRoot_WithMultipleHandlers_InvokesAllInPriorityOrder()
    {
        var executionOrder = new List<int>();
        var handler1 = new TrackingHandler(executionOrder, 1) { MediatorPriority = 10 };
        var handler2 = new TrackingHandler(executionOrder, 2) { MediatorPriority = 100 };
        var handler3 = new TrackingHandler(executionOrder, 3) { MediatorPriority = 50 };

        IEnumerable<ICommandHandler<TestCommand>> handlers = [handler1, handler2, handler3];
        var rootDelegate = CommandHandlerWrapper<TestCommand>.WrapRoot(handlers);
        var command = new TestCommand { Value = "test" };

        await rootDelegate(command, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal(2, executionOrder[0]);
        Assert.Equal(3, executionOrder[1]);
        Assert.Equal(1, executionOrder[2]);
    }

    private class TrackingHandler(List<int> executionOrder, int handlerId)
        : ICommandHandler<TestCommand>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public ValueTask HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            executionOrder.Add(handlerId);
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region WrapMiddleware Tests

    [Fact]
    public async Task WrapMiddleware_InvokesMiddlewareAndNext()
    {
        var nextCalled = false;
        var middlewareCalled = false;

        var mockMiddleware = new Mock<ICommandMiddleware<TestCommand>>();
        mockMiddleware
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CommandMiddlewareDelegate>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CommandMiddlewareDelegate, CancellationToken>(
                async (_, next, _) =>
                {
                    middlewareCalled = true;
                    await next();
                })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var factory = CommandHandlerWrapper<TestCommand>.WrapMiddleware(mockMiddleware.Object);
        var wrappedDelegate = factory((_, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        });

        await wrappedDelegate(new TestCommand(), CancellationToken.None);

        Assert.True(middlewareCalled);
        Assert.True(nextCalled);
        mockMiddleware.Verify();
    }

    [Fact]
    public async Task WrapMiddleware_PassesCorrectCommandAndCancellationToken()
    {
        TestCommand? receivedCommand = null;
        CancellationToken? receivedToken = null;
        using var cts = new CancellationTokenSource();

        var mockMiddleware = new Mock<ICommandMiddleware<TestCommand>>();
        mockMiddleware
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CommandMiddlewareDelegate>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CommandMiddlewareDelegate, CancellationToken>(
                async (cmd, next, token) =>
                {
                    receivedCommand = cmd;
                    receivedToken = token;
                    await next();
                })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var factory = CommandHandlerWrapper<TestCommand>.WrapMiddleware(mockMiddleware.Object);
        var wrappedDelegate = factory((_, _) => ValueTask.CompletedTask);
        var command = new TestCommand { Value = "unique-value" };

        await wrappedDelegate(command, cts.Token);

        Assert.NotNull(receivedCommand);
        Assert.Equal("unique-value", receivedCommand.Value.Value);
        Assert.NotNull(receivedToken);
        Assert.Equal(cts.Token, receivedToken.Value);
        mockMiddleware.Verify();
    }

    [Fact]
    public async Task WrapMiddleware_MiddlewareCanShortCircuit()
    {
        var nextCalled = false;

        var mockMiddleware = new Mock<ICommandMiddleware<TestCommand>>();
        mockMiddleware
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CommandMiddlewareDelegate>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var factory = CommandHandlerWrapper<TestCommand>.WrapMiddleware(mockMiddleware.Object);
        var wrappedDelegate = factory((_, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        });

        await wrappedDelegate(new TestCommand(), CancellationToken.None);

        Assert.False(nextCalled);
        mockMiddleware.Verify();
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WithNoMiddlewareOrHandlers_CompletesWithoutError()
    {
        IEnumerable<ICommandHandler<TestCommand>> handlers = [];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WithHandlersOnly_InvokesAllHandlers()
    {
        var handler1 = new PrioritizedHandler { MediatorPriority = 10 };
        var handler2 = new PrioritizedHandler { MediatorPriority = 100 };
        IEnumerable<ICommandHandler<TestCommand>> handlers = [handler1, handler2];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Single(handler1.ReceivedCommands);
        Assert.Single(handler2.ReceivedCommands);
    }

    [Fact]
    public async Task HandleAsync_WithMiddlewareOnly_InvokesMiddleware()
    {
        var executionLog = new List<string>();
        var middleware = new PrioritizedMiddleware(executionLog) { MiddlewareId = 1 };
        IEnumerable<ICommandHandler<TestCommand>> handlers = [];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [middleware];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal(2, executionLog.Count);
        Assert.Equal("Before-1", executionLog[0]);
        Assert.Equal("After-1", executionLog[1]);
    }

    [Fact]
    public async Task HandleAsync_WithMiddlewareAndHandlers_ExecutesInCorrectOrder()
    {
        var executionLog = new List<string>();
        var middleware1 = new PrioritizedMiddleware(executionLog) { MiddlewareId = 1, MediatorPriority = 100 };
        var middleware2 = new PrioritizedMiddleware(executionLog) { MiddlewareId = 2, MediatorPriority = 50 };
        var handler = new LoggingHandler(executionLog);

        IEnumerable<ICommandHandler<TestCommand>> handlers = [handler];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [middleware1, middleware2];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        // Aggregate wraps middlewares in sorted order (descending by priority),
        // so higher priority middleware wraps inner (executes last as outer wrapper)
        // Execution order: middleware2 (lower priority, outermost) → middleware1 (higher priority) → handler
        Assert.Equal(5, executionLog.Count);
        Assert.Equal("Before-2", executionLog[0]);
        Assert.Equal("Before-1", executionLog[1]);
        Assert.Equal("Handler", executionLog[2]);
        Assert.Equal("After-1", executionLog[3]);
        Assert.Equal("After-2", executionLog[4]);
    }

    private class LoggingHandler(List<string> executionLog) : ICommandHandler<TestCommand>
    {
        public ValueTask HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            executionLog.Add("Handler");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? receivedToken = null;

        var mockHandler = new Mock<ICommandHandler<TestCommand>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CancellationToken>((_, token) => receivedToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandHandler<TestCommand>> handlers = [mockHandler.Object];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, cts.Token);

        Assert.NotNull(receivedToken);
        Assert.Equal(cts.Token, receivedToken.Value);
        mockHandler.Verify();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void Wrapper_ImplementsICommandHandlerWrapperGeneric()
    {
        IEnumerable<ICommandHandler<TestCommand>> handlers = [];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);

        Assert.IsAssignableFrom<ICommandHandlerWrapper<TestCommand>>(wrapper);
    }

    [Fact]
    public void Wrapper_ImplementsICommandHandlerWrapperNonGeneric()
    {
        IEnumerable<ICommandHandler<TestCommand>> handlers = [];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);

        Assert.IsAssignableFrom<ICommandHandlerWrapper>(wrapper);
    }

    [Fact]
    public async Task HandleAsync_ThroughGenericInterface_WorksCorrectly()
    {
        var handler = new PrioritizedHandler();
        IEnumerable<ICommandHandler<TestCommand>> handlers = [handler];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        ICommandHandlerWrapper<TestCommand> wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Single(handler.ReceivedCommands);
    }

    [Fact]
    public async Task HandleAsync_ThroughNonGenericInterface_WorksCorrectly()
    {
        var handler = new PrioritizedHandler();
        IEnumerable<ICommandHandler<TestCommand>> handlers = [handler];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        ICommandHandlerWrapper wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        ICommand command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Single(handler.ReceivedCommands);
    }

    [Fact]
    public async Task HandleAsync_ThroughNonGenericInterface_CastsCommandCorrectly()
    {
        TestCommand? receivedCommand = null;

        var mockHandler = new Mock<ICommandHandler<TestCommand>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CancellationToken>((cmd, _) => receivedCommand = cmd)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandHandler<TestCommand>> handlers = [mockHandler.Object];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        ICommandHandlerWrapper wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        ICommand command = new TestCommand { Value = "unique-value" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(receivedCommand);
        Assert.Equal("unique-value", receivedCommand.Value.Value);
        mockHandler.Verify();
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

        IEnumerable<ICommandHandler<TestCommand>> handlers = [];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [middleware1, middleware2, middleware3];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        // Sorted descending: [middleware2(100), middleware3(50), middleware1(10)]
        // Aggregate wraps in order, so lower priority becomes outermost wrapper
        // Execution: middleware1 (outermost) → middleware3 → middleware2 (innermost) → next
        Assert.Equal(6, executionOrder.Count);
        Assert.Equal(1, executionOrder[0]);
        Assert.Equal(3, executionOrder[1]);
        Assert.Equal(2, executionOrder[2]);
        Assert.Equal(-2, executionOrder[3]);
        Assert.Equal(-3, executionOrder[4]);
        Assert.Equal(-1, executionOrder[5]);
    }

    private class TrackingMiddleware(List<int> executionOrder, int middlewareId)
        : ICommandMiddleware<TestCommand>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public async ValueTask HandleAsync(
            TestCommand command,
            CommandMiddlewareDelegate next,
            CancellationToken cancellationToken)
        {
            executionOrder.Add(middlewareId);
            await next();
            executionOrder.Add(-middlewareId);
        }
    }

    [Fact]
    public async Task HandleAsync_MiddlewareShortCircuits_HandlersNotInvoked()
    {
        var handlerCalled = false;
        var shortCircuitMiddleware = new ShortCircuitMiddleware();

        var mockHandler = new Mock<ICommandHandler<TestCommand>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => handlerCalled = true)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandHandler<TestCommand>> handlers = [mockHandler.Object];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [shortCircuitMiddleware];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.False(handlerCalled);
    }

    private class ShortCircuitMiddleware : ICommandMiddleware<TestCommand>
    {
        public ValueTask HandleAsync(
            TestCommand command,
            CommandMiddlewareDelegate next,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Handler Priority Verification Tests

    [Fact]
    public async Task HandleAsync_HandlersWithEqualPriority_MaintainsRegistrationOrder()
    {
        var executionOrder = new List<int>();

        var mockHandler1 = new Mock<ICommandHandler<TestCommand>>();
        var mockHandler2 = new Mock<ICommandHandler<TestCommand>>();
        var mockHandler3 = new Mock<ICommandHandler<TestCommand>>();

        mockHandler1
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(1))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockHandler2
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(2))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockHandler3
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(3))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandHandler<TestCommand>> handlers =
            [mockHandler1.Object, mockHandler2.Object, mockHandler3.Object];
        IEnumerable<ICommandMiddleware<TestCommand>> middlewares = [];
        var wrapper = new CommandHandlerWrapper<TestCommand>(handlers, middlewares);
        var command = new TestCommand { Value = "test" };

        await wrapper.HandleAsync(command, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal([1, 2, 3], executionOrder);
        mockHandler1.Verify();
        mockHandler2.Verify();
        mockHandler3.Verify();
    }

    #endregion
}
