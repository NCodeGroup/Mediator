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
using NCode.Mediator.Wrappers;

namespace NCode.Mediator.Tests;

public class DefaultMediatorTests
{
    #region Test Commands

    public readonly struct TestCommand : ICommand
    {
        public string? Value { get; init; }
    }

    public readonly struct TestCommandWithResponse : ICommand<string>
    {
        public string Input { get; init; }
    }

    public readonly struct AnotherTestCommand : ICommand
    {
        public int Number { get; init; }
    }

    public readonly struct AnotherTestCommandWithResponse : ICommand<int>
    {
        public int Multiplier { get; init; }
    }

    #endregion

    #region Test Handlers

    private class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public List<TestCommand> ReceivedCommands { get; } = [];
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public ValueTask HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            ReceivedCommands.Add(command);
            ReceivedCancellationToken = cancellationToken;
            return ValueTask.CompletedTask;
        }
    }

    private class TestCommandWithResponseHandler : ICommandResponseHandler<TestCommandWithResponse, string>
    {
        public List<TestCommandWithResponse> ReceivedCommands { get; } = [];
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public string ResponseToReturn { get; set; } = "default response";

        public ValueTask<string> HandleAsync(TestCommandWithResponse command, CancellationToken cancellationToken)
        {
            ReceivedCommands.Add(command);
            ReceivedCancellationToken = cancellationToken;
            return ValueTask.FromResult(ResponseToReturn);
        }
    }

    private class AnotherTestCommandHandler : ICommandHandler<AnotherTestCommand>
    {
        public List<AnotherTestCommand> ReceivedCommands { get; } = [];

        public ValueTask HandleAsync(AnotherTestCommand command, CancellationToken cancellationToken)
        {
            ReceivedCommands.Add(command);
            return ValueTask.CompletedTask;
        }
    }

    private class AnotherTestCommandWithResponseHandler : ICommandResponseHandler<AnotherTestCommandWithResponse, int>
    {
        public List<AnotherTestCommandWithResponse> ReceivedCommands { get; } = [];

        public ValueTask<int> HandleAsync(AnotherTestCommandWithResponse command, CancellationToken cancellationToken)
        {
            ReceivedCommands.Add(command);
            return ValueTask.FromResult(command.Multiplier * 10);
        }
    }

    #endregion

    #region Helper Methods

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddMediator();
        configure(services);
        return services.BuildServiceProvider();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidServiceProvider_CreatesInstance()
    {
        var serviceProvider = new Mock<IServiceProvider>().Object;

        var mediator = new DefaultMediator(serviceProvider);

        Assert.NotNull(mediator);
    }

    [Fact]
    public void DefaultMediator_ImplementsIMediator()
    {
        var serviceProvider = new Mock<IServiceProvider>().Object;

        var mediator = new DefaultMediator(serviceProvider);

        Assert.IsAssignableFrom<IMediator>(mediator);
    }

    #endregion

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithCommand_InvokesHandler()
    {
        var handler = new TestCommandHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommand { Value = "test" };

        await mediator.SendAsync(command, CancellationToken.None);

        Assert.Single(handler.ReceivedCommands);
        Assert.Equal("test", handler.ReceivedCommands[0].Value);
    }

    [Fact]
    public async Task SendAsync_WithDifferentCommands_InvokesCorrectHandlers()
    {
        var handler1 = new TestCommandHandler();
        var handler2 = new AnotherTestCommandHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(handler1);
            services.AddSingleton<ICommandHandler<AnotherTestCommand>>(handler2);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command1 = new TestCommand { Value = "first" };
        var command2 = new AnotherTestCommand { Number = 42 };

        await mediator.SendAsync(command1, CancellationToken.None);
        await mediator.SendAsync(command2, CancellationToken.None);

        Assert.Single(handler1.ReceivedCommands);
        Assert.Equal("first", handler1.ReceivedCommands[0].Value);
        Assert.Single(handler2.ReceivedCommands);
        Assert.Equal(42, handler2.ReceivedCommands[0].Number);
    }

    [Fact]
    public async Task SendAsync_WhenNoHandlersRegistered_CompletesWithoutException()
    {
        using var serviceProvider = CreateServiceProvider(_ => { });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommand { Value = "test" };

        var exception = await Record.ExceptionAsync(
            () => mediator.SendAsync(command, CancellationToken.None).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task SendAsync_PassesCancellationToken()
    {
        var handler = new TestCommandHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommand { Value = "test" };
        using var cts = new CancellationTokenSource();

        await mediator.SendAsync(command, cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedCancellationToken);
    }

    [Fact]
    public async Task SendAsync_WithCancelledToken_PassesCancelledToken()
    {
        var handler = new TestCommandHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommand { Value = "test" };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await mediator.SendAsync(command, cts.Token);

        Assert.True(handler.ReceivedCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task SendAsync_WithMultipleHandlers_InvokesAllHandlers()
    {
        var handler1 = new TestCommandHandler();
        var handler2 = new TestCommandHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(handler1);
            services.AddSingleton<ICommandHandler<TestCommand>>(handler2);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommand { Value = "test" };

        await mediator.SendAsync(command, CancellationToken.None);

        Assert.Single(handler1.ReceivedCommands);
        Assert.Single(handler2.ReceivedCommands);
    }

    #endregion

    #region SendAsync with Response Tests

    [Fact]
    public async Task SendAsyncWithResponse_WithCommand_InvokesHandlerAndReturnsResponse()
    {
        var handler = new TestCommandWithResponseHandler { ResponseToReturn = "hello world" };
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandResponseHandler<TestCommandWithResponse, string>>(handler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommandWithResponse { Input = "test" };

        var result = await mediator.SendAsync<TestCommandWithResponse, string>(command, CancellationToken.None);

        Assert.Equal("hello world", result);
        Assert.Single(handler.ReceivedCommands);
        Assert.Equal("test", handler.ReceivedCommands[0].Input);
    }

    [Fact]
    public async Task SendAsyncWithResponse_WithDifferentResponseTypes_ReturnsCorrectTypes()
    {
        var stringHandler = new TestCommandWithResponseHandler { ResponseToReturn = "string result" };
        var intHandler = new AnotherTestCommandWithResponseHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandResponseHandler<TestCommandWithResponse, string>>(stringHandler);
            services.AddSingleton<ICommandResponseHandler<AnotherTestCommandWithResponse, int>>(intHandler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var stringResult = await mediator.SendAsync<TestCommandWithResponse, string>(
            new TestCommandWithResponse { Input = "test" }, CancellationToken.None);

        var intResult = await mediator.SendAsync<AnotherTestCommandWithResponse, int>(
            new AnotherTestCommandWithResponse { Multiplier = 5 }, CancellationToken.None);

        Assert.Equal("string result", stringResult);
        Assert.Equal(50, intResult);
    }

    [Fact]
    public async Task SendAsyncWithResponse_WhenHandlerNotRegistered_ThrowsInvalidOperationException()
    {
        using var serviceProvider = CreateServiceProvider(_ => { });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommandWithResponse { Input = "test" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync<TestCommandWithResponse, string>(command, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task SendAsyncWithResponse_PassesCancellationToken()
    {
        var handler = new TestCommandWithResponseHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandResponseHandler<TestCommandWithResponse, string>>(handler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var command = new TestCommandWithResponse { Input = "test" };
        using var cts = new CancellationTokenSource();

        await mediator.SendAsync<TestCommandWithResponse, string>(command, cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedCancellationToken);
    }

    #endregion

    #region PolymorphicSendAsync Tests

    [Fact]
    public async Task PolymorphicSendAsync_WithCommand_InvokesHandler()
    {
        var handler = new TestCommandHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        ICommand command = new TestCommand { Value = "polymorphic" };

        await mediator.PolymorphicSendAsync(command, CancellationToken.None);

        Assert.Single(handler.ReceivedCommands);
        Assert.Equal("polymorphic", handler.ReceivedCommands[0].Value);
    }

    [Fact]
    public async Task PolymorphicSendAsync_ResolvesHandlerByRuntimeType()
    {
        var testHandler = new TestCommandHandler();
        var anotherHandler = new AnotherTestCommandHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(testHandler);
            services.AddSingleton<ICommandHandler<AnotherTestCommand>>(anotherHandler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        ICommand command1 = new TestCommand { Value = "first" };
        ICommand command2 = new AnotherTestCommand { Number = 100 };

        await mediator.PolymorphicSendAsync(command1, CancellationToken.None);
        await mediator.PolymorphicSendAsync(command2, CancellationToken.None);

        Assert.Single(testHandler.ReceivedCommands);
        Assert.Single(anotherHandler.ReceivedCommands);
    }

    [Fact]
    public async Task PolymorphicSendAsync_WhenNoHandlersRegistered_CompletesWithoutException()
    {
        using var serviceProvider = CreateServiceProvider(_ => { });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        ICommand command = new TestCommand { Value = "test" };

        var exception = await Record.ExceptionAsync(
            () => mediator.PolymorphicSendAsync(command, CancellationToken.None).AsTask());

        Assert.Null(exception);
    }

    #endregion

    #region PolymorphicSendAsync with Response Tests

    [Fact]
    public async Task PolymorphicSendAsyncWithResponse_WithCommand_InvokesHandlerAndReturnsResponse()
    {
        var handler = new TestCommandWithResponseHandler { ResponseToReturn = "polymorphic result" };
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandResponseHandler<TestCommandWithResponse, string>>(handler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        ICommand<string> command = new TestCommandWithResponse { Input = "test" };

        var result = await mediator.PolymorphicSendAsync(command, CancellationToken.None);

        Assert.Equal("polymorphic result", result);
        Assert.Single(handler.ReceivedCommands);
    }

    [Fact]
    public async Task PolymorphicSendAsyncWithResponse_ResolvesHandlerByRuntimeType()
    {
        var stringHandler = new TestCommandWithResponseHandler { ResponseToReturn = "string" };
        var intHandler = new AnotherTestCommandWithResponseHandler();
        using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<ICommandResponseHandler<TestCommandWithResponse, string>>(stringHandler);
            services.AddSingleton<ICommandResponseHandler<AnotherTestCommandWithResponse, int>>(intHandler);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        ICommand<string> stringCommand = new TestCommandWithResponse { Input = "test" };
        ICommand<int> intCommand = new AnotherTestCommandWithResponse { Multiplier = 7 };

        var stringResult = await mediator.PolymorphicSendAsync(stringCommand, CancellationToken.None);
        var intResult = await mediator.PolymorphicSendAsync(intCommand, CancellationToken.None);

        Assert.Equal("string", stringResult);
        Assert.Equal(70, intResult);
    }

    [Fact]
    public async Task PolymorphicSendAsyncWithResponse_WhenHandlerNotRegistered_ThrowsInvalidOperationException()
    {
        using var serviceProvider = CreateServiceProvider(_ => { });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        ICommand<string> command = new TestCommandWithResponse { Input = "test" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.PolymorphicSendAsync(command, CancellationToken.None).AsTask());
    }

    #endregion

    #region Service Resolution Tests

    [Fact]
    public void IMediator_CanBeResolvedFromServiceProvider()
    {
        using var serviceProvider = CreateServiceProvider(_ => { });

        var mediator = serviceProvider.GetRequiredService<IMediator>();

        Assert.NotNull(mediator);
        Assert.IsType<DefaultMediator>(mediator);
    }

    [Fact]
    public void IMediator_IsScopedService()
    {
        using var serviceProvider = CreateServiceProvider(_ => { });
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var mediator1 = scope1.ServiceProvider.GetRequiredService<IMediator>();
        var mediator2 = scope1.ServiceProvider.GetRequiredService<IMediator>();
        var mediator3 = scope2.ServiceProvider.GetRequiredService<IMediator>();

        Assert.Same(mediator1, mediator2);
        Assert.NotSame(mediator1, mediator3);
    }

    #endregion

    #region Unit Tests with Mocked Wrappers

    [Fact]
    public async Task SendAsync_WithMockedWrapper_InvokesWrapper()
    {
        var command = new TestCommand { Value = "unit test" };
        var cancellationToken = new CancellationToken();
        var mockWrapper = new Mock<ICommandHandlerWrapper<TestCommand>>();
        mockWrapper
            .Setup(x => x.HandleAsync(command, cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(mockWrapper.Object);
        using var serviceProvider = services.BuildServiceProvider();
        var mediator = new DefaultMediator(serviceProvider);

        await mediator.SendAsync(command, cancellationToken);

        mockWrapper.Verify(x => x.HandleAsync(command, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task SendAsyncWithResponse_WithMockedWrapper_InvokesWrapperAndReturnsResponse()
    {
        var command = new TestCommandWithResponse { Input = "unit test" };
        var cancellationToken = new CancellationToken();
        const string expectedResponse = "mocked response";
        var mockWrapper = new Mock<ICommandResponseHandlerWrapper<TestCommandWithResponse, string>>();
        mockWrapper
            .Setup(x => x.HandleAsync(command, cancellationToken))
            .ReturnsAsync(expectedResponse)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(mockWrapper.Object);
        using var serviceProvider = services.BuildServiceProvider();
        var mediator = new DefaultMediator(serviceProvider);

        var result = await mediator.SendAsync<TestCommandWithResponse, string>(command, cancellationToken);

        Assert.Equal(expectedResponse, result);
        mockWrapper.Verify(x => x.HandleAsync(command, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task PolymorphicSendAsync_WithMockedWrapper_InvokesNonGenericInterface()
    {
        var command = new TestCommand { Value = "polymorphic unit test" };
        var cancellationToken = new CancellationToken();

        var mockWrapper = new Mock<ICommandHandlerWrapper<TestCommand>>();
        mockWrapper
            .As<ICommandHandlerWrapper>()
            .Setup(x => x.HandleAsync(command, cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandlerWrapper<TestCommand>>(mockWrapper.Object);
        using var serviceProvider = services.BuildServiceProvider();
        var mediator = new DefaultMediator(serviceProvider);

        await mediator.PolymorphicSendAsync(command, cancellationToken);

        mockWrapper.As<ICommandHandlerWrapper>()
            .Verify(x => x.HandleAsync(command, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task PolymorphicSendAsyncWithResponse_WithMockedWrapper_InvokesNonGenericInterface()
    {
        var command = new TestCommandWithResponse { Input = "polymorphic unit test" };
        var cancellationToken = new CancellationToken();
        const string expectedResponse = "polymorphic mocked response";

        var mockWrapper = new Mock<ICommandResponseHandlerWrapper<TestCommandWithResponse, string>>();
        mockWrapper
            .As<ICommandResponseHandlerWrapper<string>>()
            .Setup(x => x.HandleAsync(command, cancellationToken))
            .ReturnsAsync(expectedResponse)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton<ICommandResponseHandlerWrapper<TestCommandWithResponse, string>>(mockWrapper.Object);
        using var serviceProvider = services.BuildServiceProvider();
        var mediator = new DefaultMediator(serviceProvider);

        var result = await mediator.PolymorphicSendAsync<string>(command, cancellationToken);

        Assert.Equal(expectedResponse, result);
        mockWrapper.As<ICommandResponseHandlerWrapper<string>>()
            .Verify(x => x.HandleAsync(command, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithMockedWrapper_PassesCancellationTokenCorrectly()
    {
        var command = new TestCommand { Value = "cancel test" };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        CancellationToken capturedToken = default;
        var mockWrapper = new Mock<ICommandHandlerWrapper<TestCommand>>();
        mockWrapper
            .Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .Callback<TestCommand, CancellationToken>((_, ct) => capturedToken = ct)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(mockWrapper.Object);
        using var serviceProvider = services.BuildServiceProvider();
        var mediator = new DefaultMediator(serviceProvider);

        await mediator.SendAsync(command, cts.Token);

        Assert.True(capturedToken.IsCancellationRequested);
    }

    [Fact]
    public async Task SendAsyncWithResponse_WithMockedWrapper_PassesCancellationTokenCorrectly()
    {
        var command = new TestCommandWithResponse { Input = "cancel test" };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        CancellationToken capturedToken = default;
        var mockWrapper = new Mock<ICommandResponseHandlerWrapper<TestCommandWithResponse, string>>();
        mockWrapper
            .Setup(x => x.HandleAsync(command, It.IsAny<CancellationToken>()))
            .Callback<TestCommandWithResponse, CancellationToken>((_, ct) => capturedToken = ct)
            .ReturnsAsync("response")
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(mockWrapper.Object);
        using var serviceProvider = services.BuildServiceProvider();
        var mediator = new DefaultMediator(serviceProvider);

        await mediator.SendAsync<TestCommandWithResponse, string>(command, cts.Token);

        Assert.True(capturedToken.IsCancellationRequested);
    }

    #endregion
}
