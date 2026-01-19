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

public class CommandExceptionHandlerWrapperTests
{
    #region Test Types

    public readonly struct TestCommand : ICommand
    {
        public string? Value { get; init; }
    }

    private class PrioritizedExceptionHandler
        : ICommandExceptionHandler<TestCommand, InvalidOperationException>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public List<InvalidOperationException> ReceivedExceptions { get; } = [];
        public bool ShouldSetHandled { get; init; }

        public ValueTask HandleAsync(
            TestCommand command,
            InvalidOperationException exception,
            CommandExceptionHandlerState state,
            CancellationToken cancellationToken)
        {
            ReceivedExceptions.Add(exception);
            if (ShouldSetHandled)
            {
                state.SetHandled();
            }

            return ValueTask.CompletedTask;
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

        var result = CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WithNoHandlers_CompletesWithoutError()
    {
        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);
        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();
        using var cts = new CancellationTokenSource();

        await wrapper.HandleAsync(command, exception, state, cts.Token);

        Assert.False(state.IsHandled);
    }

    [Fact]
    public async Task HandleAsync_WithSingleHandler_InvokesHandler()
    {
        var mockHandler = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();
        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();
        using var cts = new CancellationTokenSource();

        mockHandler
            .Setup(x => x.HandleAsync(command, exception, state, cts.Token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [mockHandler.Object];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        await wrapper.HandleAsync(command, exception, state, cts.Token);

        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithMultipleHandlers_InvokesAllHandlersInPriorityOrder()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 10 };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 100 };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 50 };

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Single(handler1.ReceivedExceptions);
        Assert.Single(handler2.ReceivedExceptions);
        Assert.Single(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerSetsHandled_StopsProcessingRemainingHandlers()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 100, ShouldSetHandled = true };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 50 };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 10 };

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Single(handler1.ReceivedExceptions);
        Assert.Empty(handler2.ReceivedExceptions);
        Assert.Empty(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_WhenMiddleHandlerSetsHandled_StopsAtMiddle()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 100 };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 50, ShouldSetHandled = true };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 10 };

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Single(handler1.ReceivedExceptions);
        Assert.Single(handler2.ReceivedExceptions);
        Assert.Empty(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_WhenLastHandlerSetsHandled_AllHandlersInvoked()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 100 };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 50 };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 10, ShouldSetHandled = true };

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Single(handler1.ReceivedExceptions);
        Assert.Single(handler2.ReceivedExceptions);
        Assert.Single(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? receivedToken = null;

        var mockHandler = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandExceptionHandlerState, CancellationToken>(
                (_, _, _, token) => receivedToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [mockHandler.Object];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, cts.Token);

        Assert.NotNull(receivedToken);
        Assert.Equal(cts.Token, receivedToken.Value);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectCommandAndException()
    {
        TestCommand? receivedCommand = null;
        InvalidOperationException? receivedException = null;

        var mockHandler = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandExceptionHandlerState, CancellationToken>(
                (cmd, ex, _, _) =>
                {
                    receivedCommand = cmd;
                    receivedException = ex;
                })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [mockHandler.Object];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "unique-value" };
        var exception = new InvalidOperationException("unique-error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.NotNull(receivedCommand);
        Assert.Equal("unique-value", receivedCommand.Value.Value);
        Assert.NotNull(receivedException);
        Assert.Equal("unique-error", receivedException.Message);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithStateAlreadyHandled_StillInvokesFirstHandler()
    {
        var handler = new PrioritizedExceptionHandler { MediatorPriority = 100 };

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [handler];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();
        state.SetHandled();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Single(handler.ReceivedExceptions);
    }

    #endregion

    #region Exception Casting Tests

    [Fact]
    public async Task HandleAsync_CastsExceptionToCorrectType()
    {
        InvalidOperationException? receivedException = null;

        var mockHandler = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandExceptionHandlerState, CancellationToken>(
                (_, ex, _, _) => receivedException = ex)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [mockHandler.Object];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        Exception exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.NotNull(receivedException);
        Assert.IsType<InvalidOperationException>(receivedException);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithDerivedExceptionType_CastsSuccessfully()
    {
        Exception? receivedException = null;

        var mockHandler = new Mock<ICommandExceptionHandler<TestCommand, Exception>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<Exception>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, Exception, CommandExceptionHandlerState, CancellationToken>(
                (_, ex, _, _) => receivedException = ex)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionHandler<TestCommand, Exception>> handlers = [mockHandler.Object];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, Exception>(handlers);

        var command = new TestCommand { Value = "test" };
        Exception exception = new InvalidOperationException("derived error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.NotNull(receivedException);
        Assert.IsType<InvalidOperationException>(receivedException);
        Assert.Equal("derived error", receivedException.Message);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithIncompatibleExceptionType_ThrowsInvalidCastException()
    {
        var mockHandler = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();
        mockHandler
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [mockHandler.Object];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        Exception exception = new ArgumentException("wrong type");
        var state = new CommandExceptionHandlerState();

        await Assert.ThrowsAsync<InvalidCastException>(
            () => wrapper.HandleAsync(command, exception, state, CancellationToken.None).AsTask());
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void Wrapper_ImplementsICommandExceptionHandlerWrapper()
    {
        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        Assert.IsAssignableFrom<ICommandExceptionHandlerWrapper<TestCommand>>(wrapper);
    }

    [Fact]
    public async Task HandleAsync_ThroughInterface_WorksCorrectly()
    {
        var handler = new PrioritizedExceptionHandler { MediatorPriority = 100, ShouldSetHandled = true };
        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers = [handler];
        ICommandExceptionHandlerWrapper<TestCommand> wrapper =
            new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Single(handler.ReceivedExceptions);
    }

    #endregion

    #region Handler Priority Verification Tests

    [Fact]
    public async Task HandleAsync_HandlersWithEqualPriority_MaintainsRegistrationOrder()
    {
        var executionOrder = new List<int>();

        var mockHandler1 = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();
        var mockHandler2 = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();
        var mockHandler3 = new Mock<ICommandExceptionHandler<TestCommand, InvalidOperationException>>();

        mockHandler1
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(1))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockHandler2
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(2))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockHandler3
            .Setup(x => x.HandleAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CommandExceptionHandlerState>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(3))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers =
            [mockHandler1.Object, mockHandler2.Object, mockHandler3.Object];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal([1, 2, 3], executionOrder);
        mockHandler1.Verify();
        mockHandler2.Verify();
        mockHandler3.Verify();
    }

    [Fact]
    public async Task HandleAsync_VerifiesPriorityOrderExecution()
    {
        var executionOrder = new List<int>();

        var handler1 = new TrackingExceptionHandler(executionOrder, 1) { MediatorPriority = 10 };
        var handler2 = new TrackingExceptionHandler(executionOrder, 2) { MediatorPriority = 100 };
        var handler3 = new TrackingExceptionHandler(executionOrder, 3) { MediatorPriority = 50 };
        var handler4 = new TrackingExceptionHandler(executionOrder, 4) { MediatorPriority = 100 };

        IEnumerable<ICommandExceptionHandler<TestCommand, InvalidOperationException>> handlers =
            [handler1, handler2, handler3, handler4];
        var wrapper = new CommandExceptionHandlerWrapper<TestCommand, InvalidOperationException>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandExceptionHandlerState();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Equal(4, executionOrder.Count);
        Assert.Equal(2, executionOrder[0]);
        Assert.Equal(4, executionOrder[1]);
        Assert.Equal(3, executionOrder[2]);
        Assert.Equal(1, executionOrder[3]);
    }

    private class TrackingExceptionHandler(List<int> executionOrder, int handlerId)
        : ICommandExceptionHandler<TestCommand, InvalidOperationException>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public ValueTask HandleAsync(
            TestCommand command,
            InvalidOperationException exception,
            CommandExceptionHandlerState state,
            CancellationToken cancellationToken)
        {
            executionOrder.Add(handlerId);
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
