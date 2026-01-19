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

using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Mediator.Middleware;
using NCode.Mediator.Wrappers;

namespace NCode.Mediator.Tests.Middleware;

public class StandardCommandResponseMiddlewareTests
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

    private class NonPrioritizedItem
    {
        public int Id { get; init; }
    }

    private class PrioritizedItem : ISupportMediatorPriority
    {
        public int Id { get; init; }
        public int MediatorPriority { get; init; }
    }

    private class TrackingPreProcessor(List<int> executionOrder, int processorId)
        : ICommandPreProcessor<TestCommand>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }

        public ValueTask PreProcessAsync(TestCommand command, CancellationToken cancellationToken)
        {
            executionOrder.Add(processorId);
            return ValueTask.CompletedTask;
        }
    }

    private class TrackingPostProcessor(List<int> executionOrder, int processorId)
        : ICommandResponsePostProcessor<TestCommand, string>, ISupportMediatorPriority
    {
        public int MediatorPriority { get; init; }
        public List<string> ReceivedResponses { get; } = [];

        public ValueTask PostProcessAsync(TestCommand command, string response, CancellationToken cancellationToken)
        {
            executionOrder.Add(processorId);
            ReceivedResponses.Add(response);
            return ValueTask.CompletedTask;
        }
    }

    private class ThrowingPreProcessor(Exception exception) : ICommandPreProcessor<TestCommand>
    {
        public ValueTask PreProcessAsync(TestCommand command, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private class ThrowingPostProcessor(Exception exception) : ICommandResponsePostProcessor<TestCommand, string>
    {
        public ValueTask PostProcessAsync(TestCommand command, string response, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    #endregion

    #region Sort Tests

    [Fact]
    public void Sort_WithEmptyCollection_ReturnsEmptyCollection()
    {
        IEnumerable<PrioritizedItem> items = [];

        var result = StandardCommandResponseMiddleware<TestCommand, string>.Sort(items).ToList();

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

        var result = StandardCommandResponseMiddleware<TestCommand, string>.Sort(items).ToList();

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

        var result = StandardCommandResponseMiddleware<TestCommand, string>.Sort(items).ToList();

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

        var result = StandardCommandResponseMiddleware<TestCommand, string>.Sort(items).ToList();

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

        var result = StandardCommandResponseMiddleware<TestCommand, string>.Sort(items).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }

    #endregion

    #region PreProcessAsync Tests

    [Fact]
    public async Task PreProcessAsync_WithNoPreProcessors_CompletesSuccessfully()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };

        await middleware.PreProcessAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task PreProcessAsync_WithSinglePreProcessor_InvokesPreProcessor()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var mockPreProcessor = new Mock<ICommandPreProcessor<TestCommand>>();
        var command = new TestCommand { Value = "test" };
        using var cts = new CancellationTokenSource();

        mockPreProcessor
            .Setup(x => x.PreProcessAsync(command, cts.Token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [mockPreProcessor.Object];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        await middleware.PreProcessAsync(command, cts.Token);

        mockPreProcessor.Verify();
    }

    [Fact]
    public async Task PreProcessAsync_WithMultiplePreProcessors_ExecutesInPriorityOrder()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var executionOrder = new List<int>();
        var preProcessor1 = new TrackingPreProcessor(executionOrder, 1) { MediatorPriority = 10 };
        var preProcessor2 = new TrackingPreProcessor(executionOrder, 2) { MediatorPriority = 100 };
        var preProcessor3 = new TrackingPreProcessor(executionOrder, 3) { MediatorPriority = 50 };

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [preProcessor1, preProcessor2, preProcessor3];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };

        await middleware.PreProcessAsync(command, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal(2, executionOrder[0]);
        Assert.Equal(3, executionOrder[1]);
        Assert.Equal(1, executionOrder[2]);
    }

    #endregion

    #region PostProcessAsync Tests

    [Fact]
    public async Task PostProcessAsync_WithNoPostProcessors_CompletesSuccessfully()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };

        await middleware.PostProcessAsync(command, "response", CancellationToken.None);
    }

    [Fact]
    public async Task PostProcessAsync_WithSinglePostProcessor_InvokesPostProcessor()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var mockPostProcessor = new Mock<ICommandResponsePostProcessor<TestCommand, string>>();
        var command = new TestCommand { Value = "test" };
        const string response = "test-response";
        using var cts = new CancellationTokenSource();

        mockPostProcessor
            .Setup(x => x.PostProcessAsync(command, response, cts.Token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [mockPostProcessor.Object];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        await middleware.PostProcessAsync(command, response, cts.Token);

        mockPostProcessor.Verify();
    }

    [Fact]
    public async Task PostProcessAsync_WithMultiplePostProcessors_ExecutesInPriorityOrder()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var executionOrder = new List<int>();
        var postProcessor1 = new TrackingPostProcessor(executionOrder, 1) { MediatorPriority = 10 };
        var postProcessor2 = new TrackingPostProcessor(executionOrder, 2) { MediatorPriority = 100 };
        var postProcessor3 = new TrackingPostProcessor(executionOrder, 3) { MediatorPriority = 50 };

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [postProcessor1, postProcessor2, postProcessor3];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };

        await middleware.PostProcessAsync(command, "response", CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal(2, executionOrder[0]);
        Assert.Equal(3, executionOrder[1]);
        Assert.Equal(1, executionOrder[2]);
    }

    [Fact]
    public async Task PostProcessAsync_PassesResponseToAllPostProcessors()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var executionOrder = new List<int>();
        var postProcessor1 = new TrackingPostProcessor(executionOrder, 1);
        var postProcessor2 = new TrackingPostProcessor(executionOrder, 2);

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [postProcessor1, postProcessor2];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };
        const string response = "shared-response";

        await middleware.PostProcessAsync(command, response, CancellationToken.None);

        Assert.Single(postProcessor1.ReceivedResponses);
        Assert.Equal("shared-response", postProcessor1.ReceivedResponses[0]);
        Assert.Single(postProcessor2.ReceivedResponses);
        Assert.Equal("shared-response", postProcessor2.ReceivedResponses[0]);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WithNoProcessors_InvokesNextDelegateAndReturnsResponse()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };
        var nextInvoked = false;

        var result = await middleware.HandleAsync(command, () =>
        {
            nextInvoked = true;
            return ValueTask.FromResult("next-response");
        }, CancellationToken.None);

        Assert.True(nextInvoked);
        Assert.Equal("next-response", result);
    }

    [Fact]
    public async Task HandleAsync_WithPreAndPostProcessors_ExecutesInCorrectOrder()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var executionOrder = new List<string>();

        var mockPreProcessor = new Mock<ICommandPreProcessor<TestCommand>>();
        mockPreProcessor
            .Setup(x => x.PreProcessAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add("Pre"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockPostProcessor = new Mock<ICommandResponsePostProcessor<TestCommand, string>>();
        mockPostProcessor
            .Setup(x => x.PostProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add("Post"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [mockPreProcessor.Object];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [mockPostProcessor.Object];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };

        var result = await middleware.HandleAsync(command, () =>
        {
            executionOrder.Add("Next");
            return ValueTask.FromResult("response");
        }, CancellationToken.None);

        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("Pre", executionOrder[0]);
        Assert.Equal("Next", executionOrder[1]);
        Assert.Equal("Post", executionOrder[2]);
        Assert.Equal("response", result);
        mockPreProcessor.Verify();
        mockPostProcessor.Verify();
    }

    [Fact]
    public async Task HandleAsync_PassesResponseFromNextToPostProcessors()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        string? receivedResponse = null;

        var mockPostProcessor = new Mock<ICommandResponsePostProcessor<TestCommand, string>>();
        mockPostProcessor
            .Setup(x => x.PostProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, string, CancellationToken>((_, r, _) => receivedResponse = r)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [mockPostProcessor.Object];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);
        var command = new TestCommand { Value = "test" };

        await middleware.HandleAsync(command, () => ValueTask.FromResult("handler-response"), CancellationToken.None);

        Assert.Equal("handler-response", receivedResponse);
        mockPostProcessor.Verify();
    }

    #endregion

    #region NotifyExceptionAsync Tests

    [Fact]
    public async Task NotifyExceptionAsync_ResolvesAndInvokesListenerWrapper()
    {
        var exception = new InvalidOperationException("test error");
        var command = new TestCommand { Value = "test" };
        using var cts = new CancellationTokenSource();

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<TestCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, cts.Token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>),
            _ => mockListenerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        await middleware.NotifyExceptionAsync(command, exception, cts.Token);

        mockListenerWrapper.Verify();
    }

    [Fact]
    public async Task NotifyExceptionAsync_WithDerivedExceptionType_ResolvesCorrectWrapper()
    {
        var exception = new ArgumentNullException("param");
        var command = new TestCommand { Value = "test" };

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<TestCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<TestCommand, ArgumentNullException>),
            _ => mockListenerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        await middleware.NotifyExceptionAsync(command, exception, CancellationToken.None);

        mockListenerWrapper.Verify();
    }

    #endregion

    #region HandleExceptionAsync Tests

    [Fact]
    public async Task HandleExceptionAsync_ResolvesAndInvokesHandlerWrapper()
    {
        var exception = new InvalidOperationException("test error");
        var command = new TestCommand { Value = "test" };
        using var cts = new CancellationTokenSource();

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<string>>(), cts.Token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        var state = await middleware.HandleExceptionAsync(command, exception, cts.Token);

        Assert.NotNull(state);
        Assert.False(state.IsHandled);
        mockHandlerWrapper.Verify();
    }

    [Fact]
    public async Task HandleExceptionAsync_WhenHandlerSetsHandled_ReturnsHandledStateWithResponse()
    {
        var exception = new InvalidOperationException("test error");
        var command = new TestCommand { Value = "test" };

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, Exception, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, _, s, _) => s.SetHandled("fallback-response"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        var state = await middleware.HandleExceptionAsync(command, exception, CancellationToken.None);

        Assert.True(state.IsHandled);
        Assert.Equal("fallback-response", state.Response);
        mockHandlerWrapper.Verify();
    }

    #endregion

    #region Exception Handling Integration Tests

    [Fact]
    public async Task HandleAsync_WhenPreProcessorThrows_NotifiesListenersAndInvokesHandlers()
    {
        var exception = new InvalidOperationException("pre-processor error");
        var command = new TestCommand { Value = "test" };
        var listenerInvoked = false;
        var handlerInvoked = false;

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<TestCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, It.IsAny<CancellationToken>()))
            .Callback(() => listenerInvoked = true)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => handlerInvoked = true)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>),
            _ => mockListenerWrapper.Object);
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        var throwingPreProcessor = new ThrowingPreProcessor(exception);
        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [throwingPreProcessor];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.HandleAsync(command, () => ValueTask.FromResult("response"), CancellationToken.None).AsTask());

        Assert.True(listenerInvoked);
        Assert.True(handlerInvoked);
        mockListenerWrapper.Verify();
        mockHandlerWrapper.Verify();
    }

    [Fact]
    public async Task HandleAsync_WhenNextThrows_NotifiesListenersAndInvokesHandlers()
    {
        var exception = new InvalidOperationException("next error");
        var command = new TestCommand { Value = "test" };
        var listenerInvoked = false;
        var handlerInvoked = false;

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<TestCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, It.IsAny<CancellationToken>()))
            .Callback(() => listenerInvoked = true)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => handlerInvoked = true)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>),
            _ => mockListenerWrapper.Object);
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.HandleAsync(command, () => throw exception, CancellationToken.None).AsTask());

        Assert.True(listenerInvoked);
        Assert.True(handlerInvoked);
        mockListenerWrapper.Verify();
        mockHandlerWrapper.Verify();
    }

    [Fact]
    public async Task HandleAsync_WhenPostProcessorThrows_NotifiesListenersAndInvokesHandlers()
    {
        var exception = new InvalidOperationException("post-processor error");
        var command = new TestCommand { Value = "test" };
        var listenerInvoked = false;
        var handlerInvoked = false;
        var nextInvoked = false;

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<TestCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, It.IsAny<CancellationToken>()))
            .Callback(() => listenerInvoked = true)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => handlerInvoked = true)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>),
            _ => mockListenerWrapper.Object);
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        var throwingPostProcessor = new ThrowingPostProcessor(exception);
        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [throwingPostProcessor];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.HandleAsync(command, () =>
            {
                nextInvoked = true;
                return ValueTask.FromResult("response");
            }, CancellationToken.None).AsTask());

        Assert.True(nextInvoked);
        Assert.True(listenerInvoked);
        Assert.True(handlerInvoked);
        mockListenerWrapper.Verify();
        mockHandlerWrapper.Verify();
    }

    [Fact]
    public async Task HandleAsync_WhenExceptionHandled_ReturnsFallbackResponse()
    {
        var exception = new InvalidOperationException("handled error");
        var command = new TestCommand { Value = "test" };

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<TestCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Callback<TestCommand, Exception, CommandResponseExceptionHandlerState<string>, CancellationToken>(
                (_, _, s, _) => s.SetHandled("fallback-response"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>),
            _ => mockListenerWrapper.Object);
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        var result = await middleware.HandleAsync(command, () => throw exception, CancellationToken.None);

        Assert.Equal("fallback-response", result);
        mockListenerWrapper.Verify();
        mockHandlerWrapper.Verify();
    }

    [Fact]
    public async Task HandleAsync_WhenExceptionNotHandled_Rethrows()
    {
        var exception = new InvalidOperationException("unhandled error");
        var command = new TestCommand { Value = "test" };

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<TestCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<TestCommand, string>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<string>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<TestCommand, InvalidOperationException>),
            _ => mockListenerWrapper.Object);
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<TestCommand, InvalidOperationException, string>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.HandleAsync(command, () => throw exception, CancellationToken.None).AsTask());

        Assert.Same(exception, thrownException);
        mockListenerWrapper.Verify();
        mockHandlerWrapper.Verify();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void Middleware_ImplementsICommandResponseMiddleware()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        IEnumerable<ICommandPreProcessor<TestCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<TestCommand, string>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<TestCommand, string>(serviceProvider, preProcessors, postProcessors);

        Assert.IsAssignableFrom<ICommandResponseMiddleware<TestCommand, string>>(middleware);
    }

    #endregion

    #region Response Type Tests

    [Fact]
    public async Task HandleAsync_WithValueTypeResponse_ReturnsCorrectly()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        IEnumerable<ICommandPreProcessor<IntResponseCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<IntResponseCommand, int>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<IntResponseCommand, int>(serviceProvider, preProcessors, postProcessors);
        var command = new IntResponseCommand { Input = 10 };

        var result = await middleware.HandleAsync(command, () => ValueTask.FromResult(42), CancellationToken.None);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task HandleAsync_WhenExceptionHandledWithValueTypeResponse_ReturnsFallback()
    {
        var exception = new InvalidOperationException("error");
        var command = new IntResponseCommand { Input = 10 };

        var mockListenerWrapper = new Mock<ICommandExceptionListenerWrapper<IntResponseCommand>>();
        mockListenerWrapper
            .Setup(x => x.ListenAsync(command, exception, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockHandlerWrapper = new Mock<ICommandResponseExceptionHandlerWrapper<IntResponseCommand, int>>();
        mockHandlerWrapper
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<int>>(), It.IsAny<CancellationToken>()))
            .Callback<IntResponseCommand, Exception, CommandResponseExceptionHandlerState<int>, CancellationToken>(
                (_, _, s, _) => s.SetHandled(-1))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(CommandExceptionListenerWrapper<IntResponseCommand, InvalidOperationException>),
            _ => mockListenerWrapper.Object);
        services.AddSingleton(typeof(CommandResponseExceptionHandlerWrapper<IntResponseCommand, InvalidOperationException, int>),
            _ => mockHandlerWrapper.Object);
        var serviceProvider = services.BuildServiceProvider();

        IEnumerable<ICommandPreProcessor<IntResponseCommand>> preProcessors = [];
        IEnumerable<ICommandResponsePostProcessor<IntResponseCommand, int>> postProcessors = [];
        var middleware = new StandardCommandResponseMiddleware<IntResponseCommand, int>(serviceProvider, preProcessors, postProcessors);

        var result = await middleware.HandleAsync(command, () => throw exception, CancellationToken.None);

        Assert.Equal(-1, result);
        mockListenerWrapper.Verify();
        mockHandlerWrapper.Verify();
    }

    #endregion
}
