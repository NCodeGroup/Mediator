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
using NCode.Mediator.Wrappers;

namespace NCode.Mediator;

/// <summary>
/// Provides the default implementation of <see cref="IMediator"/> that dispatches commands
/// to their handlers using the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// This implementation resolves handler wrappers from the <see cref="IServiceProvider"/> to
/// process commands. The wrappers encapsulate the middleware pipeline, pre/post processors,
/// exception handlers, and the actual command handlers.
/// </para>
/// <para>
/// The generic <see cref="SendAsync{TCommand}"/> and <see cref="SendAsync{TCommand,TResponse}"/>
/// methods use compile-time type information to resolve handlers, making them AOT-compatible.
/// </para>
/// <para>
/// The polymorphic <see cref="PolymorphicSendAsync"/> and <see cref="PolymorphicSendAsync{TResponse}"/>
/// methods use reflection to resolve handlers based on the runtime type of the command, which
/// requires dynamic code generation and is not AOT-compatible.
/// </para>
/// </remarks>
/// <param name="serviceProvider">The service provider used to resolve command handler wrappers.</param>
/// <seealso cref="IMediator"/>
public class DefaultMediator(
    IServiceProvider serviceProvider
) : IMediator
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public ValueTask SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : struct, ICommand
    {
        var wrapper = ServiceProvider.GetRequiredService<ICommandHandlerWrapper<TCommand>>();
        return wrapper.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : struct, ICommand<TResponse>
    {
        var wrapper = ServiceProvider.GetRequiredService<ICommandResponseHandlerWrapper<TCommand, TResponse>>();
        return wrapper.HandleAsync(command, cancellationToken);
    }

    //

    /// <inheritdoc />
    public ValueTask PolymorphicSendAsync(
        ICommand command,
        CancellationToken cancellationToken
    )
    {
        var wrapperType = typeof(ICommandHandlerWrapper<>).MakeGenericType(command.GetType());
        var wrapper = (ICommandHandlerWrapper)ServiceProvider.GetRequiredService(wrapperType);
        return wrapper.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> PolymorphicSendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken
    )
    {
        var wrapperType = typeof(ICommandResponseHandlerWrapper<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        var wrapper = (ICommandResponseHandlerWrapper<TResponse>)ServiceProvider.GetRequiredService(wrapperType);
        return wrapper.HandleAsync(command, cancellationToken);
    }
}
