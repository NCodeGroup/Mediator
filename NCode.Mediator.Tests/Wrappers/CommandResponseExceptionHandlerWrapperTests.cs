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

public class CommandResponseExceptionHandlerWrapperTests
{
    #region Test Types

    public readonly struct TestCommand : ICommand<string>
    {
        public string? Value { get; init; }
    }

    public readonly struct IntResponseCommand : ICommand<int>
    {
        public int Input { get; init; }
    }

    private class PrioritizedExceptionHandler
        : ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public List<InvalidOperationException> ReceivedExceptions { get; } = [];
        public bool ShouldSetHandled { get; init; }
        public string? ResponseToSet { get; init; }

        public ValueTask HandleAsync(
            TestCommand command,
            InvalidOperationException exception,
            CommandResponseExceptionHandlerState<string> state,
            CancellationToken cancellationToken)
        {
            ReceivedExceptions.Add(exception);
            if (ShouldSetHandled && ResponseToSet is not null)
            {
                state.SetHandled(ResponseToSet);
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

        var result = CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>.Sort(items).ToList();

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

        var result = CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>.Sort(items).ToList();

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

        var result = CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>.Sort(items).ToList();

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

        var result = CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>.Sort(items).ToList();

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

        var result = CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>.Sort(items).ToList();

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
        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);
        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();
        using var cts = new CancellationTokenSource();

        await wrapper.HandleAsync(command, exception, state, cts.Token);

        Assert.False(state.IsHandled);
        Assert.Null(state.Response);
    }

    [Fact]
    public async Task HandleAsync_WithSingleHandler_InvokesHandler()
    {
        var mockHandler = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();
        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();
        using var cts = new CancellationTokenSource();

        mockHandler
            .Setup(x => x.HandleAsync(command, exception, state, cts.Token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        await wrapper.HandleAsync(command, exception, state, cts.Token);

        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithMultipleHandlers_InvokesAllHandlersInPriorityOrder()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 10 };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 100 };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 50 };

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Single(handler1.ReceivedExceptions);
        Assert.Single(handler2.ReceivedExceptions);
        Assert.Single(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerSetsHandled_StopsProcessingRemainingHandlers()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 100, ShouldSetHandled = true, ResponseToSet = "fallback1" };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 50 };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 10 };

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Equal("fallback1", state.Response);
        Assert.Single(handler1.ReceivedExceptions);
        Assert.Empty(handler2.ReceivedExceptions);
        Assert.Empty(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_WhenMiddleHandlerSetsHandled_StopsAtMiddle()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 100 };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 50, ShouldSetHandled = true, ResponseToSet = "middle-response" };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 10 };

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Equal("middle-response", state.Response);
        Assert.Single(handler1.ReceivedExceptions);
        Assert.Single(handler2.ReceivedExceptions);
        Assert.Empty(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_WhenLastHandlerSetsHandled_AllHandlersInvoked()
    {
        var handler1 = new PrioritizedExceptionHandler { MediatorPriority = 100 };
        var handler2 = new PrioritizedExceptionHandler { MediatorPriority = 50 };
        var handler3 = new PrioritizedExceptionHandler { MediatorPriority = 10, ShouldSetHandled = true, ResponseToSet = "last-response" };

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Equal("last-response", state.Response);
        Assert.Single(handler1.ReceivedExceptions);
        Assert.Single(handler2.ReceivedExceptions);
        Assert.Single(handler3.ReceivedExceptions);
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectCommandToHandlers()
    {
        TestCommand? receivedCommand = null;
        var mockHandler = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<InvalidOperationException>(), It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (cmd, _, _, _) => receivedCommand = cmd)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "unique-test-value" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.NotNull(receivedCommand);
        Assert.Equal("unique-test-value", receivedCommand.Value.Value);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectCancellationToken()
    {
        CancellationToken receivedToken = default;
        var mockHandler = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();
        using var cts = new CancellationTokenSource();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<InvalidOperationException>(), It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, _, _, token) => receivedToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, cts.Token);

        Assert.Equal(cts.Token, receivedToken);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectStateToHandlers()
    {
        CommandResponseExceptionHandlerState<string>? receivedState = null;
        var mockHandler = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<InvalidOperationException>(), It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, _, s, _) => receivedState = s)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Same(state, receivedState);
        mockHandler.Verify();
    }

    #endregion

    #region Exception Casting Tests

    [Fact]
    public async Task HandleAsync_CastsExceptionToCorrectType()
    {
        InvalidOperationException? receivedException = null;
        var mockHandler = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<InvalidOperationException>(), It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, ex, _, _) => receivedException = ex)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        Exception exception = new InvalidOperationException("specific error message");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.NotNull(receivedException);
        Assert.Equal("specific error message", receivedException.Message);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithDerivedExceptionType_CastsCorrectly()
    {
        ArgumentNullException? receivedException = null;
        var mockHandler = new Mock<ICommandResponseExceptionHandler<TestCommand, ArgumentNullException, string>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<ArgumentNullException>(), It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, ArgumentNullException, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, ex, _, _) => receivedException = ex)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, ArgumentNullException, string>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, ArgumentNullException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        Exception exception = new ArgumentNullException("paramName", "Argument cannot be null");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.NotNull(receivedException);
        Assert.Equal("paramName", receivedException.ParamName);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_WithInvalidExceptionType_ThrowsInvalidCastException()
    {
        var mockHandler = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        Exception exception = new ArgumentException("wrong type");
        var state = new CommandResponseExceptionHandlerState<string>();

        await Assert.ThrowsAsync<InvalidCastException>(() =>
            wrapper.HandleAsync(command, exception, state, CancellationToken.None).AsTask());
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void Wrapper_ImplementsICommandResponseExceptionHandlerWrapper()
    {
        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        Assert.IsAssignableFrom<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>(wrapper);
    }

    [Fact]
    public async Task PolymorphicDispatch_ThroughInterface_WorksCorrectly()
    {
        var handler = new PrioritizedExceptionHandler { ShouldSetHandled = true, ResponseToSet = "handled-response" };

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [handler];
        ICommandResponseExceptionHandlerWrapper<TestCommand, string> wrapper =
            new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        Exception exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Equal("handled-response", state.Response);
        Assert.Single(handler.ReceivedExceptions);
    }

    #endregion

    #region Handler Priority Verification Tests

    [Fact]
    public async Task HandleAsync_VerifyExactExecutionOrder()
    {
        var executionOrder = new List<int>();

        var handler1 = new OrderTrackingHandler(executionOrder, 1) { MediatorPriority = 10 };
        var handler2 = new OrderTrackingHandler(executionOrder, 2) { MediatorPriority = 100 };
        var handler3 = new OrderTrackingHandler(executionOrder, 3) { MediatorPriority = 50 };

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal(2, executionOrder[0]);
        Assert.Equal(3, executionOrder[1]);
        Assert.Equal(1, executionOrder[2]);
    }

    private class OrderTrackingHandler(List<int> executionOrder, int handlerId)
        : ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public ValueTask HandleAsync(
            TestCommand command,
            InvalidOperationException exception,
            CommandResponseExceptionHandlerState<string> state,
            CancellationToken cancellationToken)
        {
            executionOrder.Add(handlerId);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task HandleAsync_WithEqualPriorities_MaintainsRegistrationOrder()
    {
        var executionOrder = new List<int>();

        var handler1 = new OrderTrackingHandler(executionOrder, 1) { MediatorPriority = 50 };
        var handler2 = new OrderTrackingHandler(executionOrder, 2) { MediatorPriority = 50 };
        var handler3 = new OrderTrackingHandler(executionOrder, 3) { MediatorPriority = 50 };

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [handler1, handler2, handler3];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal(1, executionOrder[0]);
        Assert.Equal(2, executionOrder[1]);
        Assert.Equal(3, executionOrder[2]);
    }

    #endregion

    #region Response Type Tests

    [Fact]
    public async Task HandleAsync_WithValueTypeResponse_SetsResponseCorrectly()
    {
        var mockHandler = new Mock<ICommandResponseExceptionHandler<IntResponseCommand, InvalidOperationException, int>>();

        mockHandler
            .Setup(x => x.HandleAsync(It.IsAny<IntResponseCommand>(), It.IsAny<InvalidOperationException>(), It.IsAny<CommandResponseExceptionHandlerState<int>>(), It.IsAny<CancellationToken>()))
            .Callback<IntResponseCommand, InvalidOperationException, CommandResponseExceptionHandlerState<int>, CancellationToken>(
                (_, _, s, _) => s.SetHandled(42))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<IntResponseCommand, InvalidOperationException, int>> handlers = [mockHandler.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<IntResponseCommand, InvalidOperationException, int>(handlers);

        var command = new IntResponseCommand { Input = 10 };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<int>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Equal(42, state.Response);
        mockHandler.Verify();
    }

    [Fact]
    public async Task HandleAsync_StateSharedAcrossHandlers()
    {
        var mockHandler1 = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();
        var mockHandler2 = new Mock<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>>();
        CommandResponseExceptionHandlerState<string>? state1 = null;
        CommandResponseExceptionHandlerState<string>? state2 = null;

        mockHandler1
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<InvalidOperationException>(), It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, _, s, _) => state1 = s)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockHandler2
            .Setup(x => x.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<InvalidOperationException>(), It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, _, s, _) => state2 = s)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandResponseExceptionHandler<TestCommand, InvalidOperationException, string>> handlers = [mockHandler1.Object, mockHandler2.Object];
        var wrapper = new CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>(handlers);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        var state = new CommandResponseExceptionHandlerState<string>();

        await wrapper.HandleAsync(command, exception, state, CancellationToken.None);

        Assert.Same(state, state1);
        Assert.Same(state, state2);
        Assert.Same(state1, state2);
        mockHandler1.Verify();
        mockHandler2.Verify();
    }

    #endregion
}
