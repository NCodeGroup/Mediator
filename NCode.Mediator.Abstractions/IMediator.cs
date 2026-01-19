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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Mediator;

/// <summary>
/// Defines the contract for a mediator that dispatches commands to their corresponding handlers.
/// </summary>
/// <remarks>
/// <para>
/// The mediator pattern decouples command senders from command handlers, allowing for a clean separation
/// of concerns. Commands can be dispatched without the sender needing to know which handler(s) will
/// process them.
/// </para>
/// <para>
/// This interface provides two categories of methods:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Generic methods</b> (<see cref="SendAsync{TCommand}"/> and <see cref="SendAsync{TCommand,TResponse}"/>):
///       Use the compile-time type of the command for handler resolution. These are AOT-compatible and preferred
///       when the command type is known at compile time.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Polymorphic methods</b> (<see cref="PolymorphicSendAsync"/> and <see cref="PolymorphicSendAsync{TResponse}"/>):
///       Use the runtime type of the command for handler resolution. These require reflection and are not AOT-compatible,
///       but are useful when the command type is only known at runtime.
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
[PublicAPI]
public interface IMediator
{
    /// <summary>
    /// Asynchronously dispatches a command to zero or more handlers without returning a response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses the compile-time type <typeparamref name="TCommand"/> to resolve handlers,
    /// making it AOT-compatible. Since no response is expected, any number of handlers (including zero)
    /// may process the command.
    /// </para>
    /// <para>
    /// All registered <see cref="ICommandHandler{TCommand}"/> implementations for the specified command
    /// type will be invoked. Handlers are executed according to their configured priority order.
    /// </para>
    /// </remarks>
    /// <param name="command">The command to dispatch to the handler(s).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <typeparam name="TCommand">The type of the command. Must be a value type implementing <see cref="ICommand"/>.</typeparam>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous dispatch operation.</returns>
    ValueTask SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken
    )
        where TCommand : struct, ICommand;

    /// <summary>
    /// Asynchronously dispatches a command to a single handler and returns the handler's response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses the compile-time type <typeparamref name="TCommand"/> to resolve the handler,
    /// making it AOT-compatible. Exactly one handler must be registered for the command type.
    /// </para>
    /// <para>
    /// The registered <see cref="ICommandResponseHandler{TCommand,TResponse}"/> implementation for the
    /// specified command type will be invoked and its response returned.
    /// </para>
    /// </remarks>
    /// <param name="command">The command to dispatch to the handler.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <typeparam name="TCommand">The type of the command. Must be a value type implementing <see cref="ICommand{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation,
    /// containing the response from the handler.</returns>
    ValueTask<TResponse> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken
    )
        where TCommand : struct, ICommand<TResponse>;

    //

    /// <summary>
    /// Asynchronously dispatches a command to zero or more handlers without returning a response,
    /// using the runtime type of the command for handler resolution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses reflection to determine the runtime type of <paramref name="command"/> and
    /// resolve the appropriate handlers. This enables polymorphic dispatch but requires dynamic code
    /// generation and is not AOT-compatible.
    /// </para>
    /// <para>
    /// Use this method when the concrete command type is only known at runtime. Prefer
    /// <see cref="SendAsync{TCommand}"/> when the type is known at compile time for better performance
    /// and AOT compatibility.
    /// </para>
    /// </remarks>
    /// <param name="command">The command to dispatch to the handler(s).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous dispatch operation.</returns>
    [RequiresDynamicCode("This method uses reflection to determine the type of the command.")]
    ValueTask PolymorphicSendAsync(
        ICommand command,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Asynchronously dispatches a command to a single handler and returns the handler's response,
    /// using the runtime type of the command for handler resolution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses reflection to determine the runtime type of <paramref name="command"/> and
    /// resolve the appropriate handler. This enables polymorphic dispatch but requires dynamic code
    /// generation and is not AOT-compatible.
    /// </para>
    /// <para>
    /// Use this method when the concrete command type is only known at runtime. Prefer
    /// <see cref="SendAsync{TCommand,TResponse}"/> when the type is known at compile time for better
    /// performance and AOT compatibility.
    /// </para>
    /// </remarks>
    /// <param name="command">The command to dispatch to the handler.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation,
    /// containing the response from the handler.</returns>
    [RequiresDynamicCode("This method uses reflection to determine the type of the command.")]
    ValueTask<TResponse> PolymorphicSendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken
    );
}
