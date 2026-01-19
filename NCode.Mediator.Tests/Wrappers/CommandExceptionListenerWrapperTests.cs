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

public class CommandExceptionListenerWrapperTests
{
    #region Test Types

    public readonly struct TestCommand : ICommand
    {
        public string? Value { get; init; }
    }

    private class PrioritizedExceptionListener
        : ICommandExceptionListener<TestCommand, InvalidOperationException>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public List<InvalidOperationException> ReceivedExceptions { get; } = [];

        public ValueTask ListenAsync(
            TestCommand command,
            InvalidOperationException exception,
            CancellationToken cancellationToken)
        {
            ReceivedExceptions.Add(exception);
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

        var result = CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

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

        var result = CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>.Sort(items).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }

    #endregion

    #region ListenAsync Tests

    [Fact]
    public async Task ListenAsync_WithNoListeners_CompletesWithoutError()
    {
        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);
        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        using var cts = new CancellationTokenSource();

        await wrapper.ListenAsync(command, exception, cts.Token);
    }

    [Fact]
    public async Task ListenAsync_WithSingleListener_InvokesListener()
    {
        var mockListener = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();
        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");
        using var cts = new CancellationTokenSource();

        mockListener
            .Setup(x => x.ListenAsync(command, exception, cts.Token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [mockListener.Object];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        await wrapper.ListenAsync(command, exception, cts.Token);

        mockListener.Verify();
    }

    [Fact]
    public async Task ListenAsync_WithMultipleListeners_InvokesAllListenersInPriorityOrder()
    {
        var listener1 = new PrioritizedExceptionListener { MediatorPriority = 10 };
        var listener2 = new PrioritizedExceptionListener { MediatorPriority = 100 };
        var listener3 = new PrioritizedExceptionListener { MediatorPriority = 50 };

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [listener1, listener2, listener3];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.Single(listener1.ReceivedExceptions);
        Assert.Single(listener2.ReceivedExceptions);
        Assert.Single(listener3.ReceivedExceptions);
    }

    [Fact]
    public async Task ListenAsync_AlwaysInvokesAllListeners()
    {
        var listener1 = new PrioritizedExceptionListener { MediatorPriority = 100 };
        var listener2 = new PrioritizedExceptionListener { MediatorPriority = 50 };
        var listener3 = new PrioritizedExceptionListener { MediatorPriority = 10 };

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [listener1, listener2, listener3];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.Single(listener1.ReceivedExceptions);
        Assert.Single(listener2.ReceivedExceptions);
        Assert.Single(listener3.ReceivedExceptions);
    }

    [Fact]
    public async Task ListenAsync_PassesCorrectCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? receivedToken = null;

        var mockListener = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();
        mockListener
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CancellationToken>(
                (_, _, token) => receivedToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [mockListener.Object];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");

        await wrapper.ListenAsync(command, exception, cts.Token);

        Assert.NotNull(receivedToken);
        Assert.Equal(cts.Token, receivedToken.Value);
        mockListener.Verify();
    }

    [Fact]
    public async Task ListenAsync_PassesCorrectCommandAndException()
    {
        TestCommand? receivedCommand = null;
        InvalidOperationException? receivedException = null;

        var mockListener = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();
        mockListener
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CancellationToken>(
                (cmd, ex, _) =>
                {
                    receivedCommand = cmd;
                    receivedException = ex;
                })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [mockListener.Object];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "unique-value" };
        var exception = new InvalidOperationException("unique-error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.NotNull(receivedCommand);
        Assert.Equal("unique-value", receivedCommand.Value.Value);
        Assert.NotNull(receivedException);
        Assert.Equal("unique-error", receivedException.Message);
        mockListener.Verify();
    }

    #endregion

    #region Exception Casting Tests

    [Fact]
    public async Task ListenAsync_CastsExceptionToCorrectType()
    {
        InvalidOperationException? receivedException = null;

        var mockListener = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();
        mockListener
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, InvalidOperationException, CancellationToken>(
                (_, ex, _) => receivedException = ex)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [mockListener.Object];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        Exception exception = new InvalidOperationException("test error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.NotNull(receivedException);
        Assert.IsType<InvalidOperationException>(receivedException);
        mockListener.Verify();
    }

    [Fact]
    public async Task ListenAsync_WithDerivedExceptionType_CastsSuccessfully()
    {
        Exception? receivedException = null;

        var mockListener = new Mock<ICommandExceptionListener<TestCommand, Exception>>();
        mockListener
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<Exception>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestCommand, Exception, CancellationToken>(
                (_, ex, _) => receivedException = ex)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionListener<TestCommand, Exception>> listeners = [mockListener.Object];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, Exception>(listeners);

        var command = new TestCommand { Value = "test" };
        Exception exception = new InvalidOperationException("derived error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.NotNull(receivedException);
        Assert.IsType<InvalidOperationException>(receivedException);
        Assert.Equal("derived error", receivedException.Message);
        mockListener.Verify();
    }

    [Fact]
    public async Task ListenAsync_WithIncompatibleExceptionType_ThrowsInvalidCastException()
    {
        var mockListener = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();
        mockListener
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [mockListener.Object];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        Exception exception = new ArgumentException("wrong type");

        await Assert.ThrowsAsync<InvalidCastException>(
            () => wrapper.ListenAsync(command, exception, CancellationToken.None).AsTask());
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void Wrapper_ImplementsICommandExceptionListenerWrapper()
    {
        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        Assert.IsAssignableFrom<ICommandExceptionListenerWrapper<TestCommand>>(wrapper);
    }

    [Fact]
    public async Task ListenAsync_ThroughInterface_WorksCorrectly()
    {
        var listener = new PrioritizedExceptionListener { MediatorPriority = 100 };
        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners = [listener];
        ICommandExceptionListenerWrapper<TestCommand> wrapper =
            new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.Single(listener.ReceivedExceptions);
    }

    #endregion

    #region Listener Priority Verification Tests

    [Fact]
    public async Task ListenAsync_ListenersWithEqualPriority_MaintainsRegistrationOrder()
    {
        var executionOrder = new List<int>();

        var mockListener1 = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();
        var mockListener2 = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();
        var mockListener3 = new Mock<ICommandExceptionListener<TestCommand, InvalidOperationException>>();

        mockListener1
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(1))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockListener2
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(2))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockListener3
            .Setup(x => x.ListenAsync(
                It.IsAny<TestCommand>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add(3))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners =
            [mockListener1.Object, mockListener2.Object, mockListener3.Object];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal([1, 2, 3], executionOrder);
        mockListener1.Verify();
        mockListener2.Verify();
        mockListener3.Verify();
    }

    [Fact]
    public async Task ListenAsync_VerifiesPriorityOrderExecution()
    {
        var executionOrder = new List<int>();

        var listener1 = new TrackingExceptionListener(executionOrder, 1) { MediatorPriority = 10 };
        var listener2 = new TrackingExceptionListener(executionOrder, 2) { MediatorPriority = 100 };
        var listener3 = new TrackingExceptionListener(executionOrder, 3) { MediatorPriority = 50 };
        var listener4 = new TrackingExceptionListener(executionOrder, 4) { MediatorPriority = 100 };

        IEnumerable<ICommandExceptionListener<TestCommand, InvalidOperationException>> listeners =
            [listener1, listener2, listener3, listener4];
        var wrapper = new CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>(listeners);

        var command = new TestCommand { Value = "test" };
        var exception = new InvalidOperationException("test error");

        await wrapper.ListenAsync(command, exception, CancellationToken.None);

        Assert.Equal(4, executionOrder.Count);
        Assert.Equal(2, executionOrder[0]);
        Assert.Equal(4, executionOrder[1]);
        Assert.Equal(3, executionOrder[2]);
        Assert.Equal(1, executionOrder[3]);
    }

    private class TrackingExceptionListener(List<int> executionOrder, int listenerId)
        : ICommandExceptionListener<TestCommand, InvalidOperationException>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public ValueTask ListenAsync(
            TestCommand command,
            InvalidOperationException exception,
            CancellationToken cancellationToken)
        {
            executionOrder.Add(listenerId);
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
