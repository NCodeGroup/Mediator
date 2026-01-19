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

using JetBrains.Annotations;

namespace NCode.Mediator;

/// <summary>
/// Defines a handler that processes commands of type <typeparamref name="TCommand"/> and returns a response
/// of type <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create handlers for commands that implement <see cref="ICommand{TResponse}"/>.
/// Handlers are invoked by the <see cref="IMediator"/> when a matching command is dispatched
/// via <see cref="IMediator.SendAsync{TCommand,TResponse}"/> or <see cref="IMediator.PolymorphicSendAsync{TResponse}"/>.
/// </para>
/// <para>
/// Unlike <see cref="ICommandHandler{TCommand}"/>, exactly one handler should be registered for each
/// command-response pair. This ensures that a single, deterministic response is returned to the caller.
/// </para>
/// <para>
/// Handlers are typically registered with the dependency injection container and resolved
/// by the mediator at dispatch time.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to handle. Must implement <see cref="ICommand{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <example>
/// <code>
/// public class GetUserHandler : ICommandResponseHandler&lt;GetUserQuery, User&gt;
/// {
///     public async ValueTask&lt;User&gt; HandleAsync(GetUserQuery command, CancellationToken cancellationToken)
///     {
///         // Query and return the user
///         return new User { Id = command.UserId, Name = "John Doe" };
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommand{TResponse}"/>
/// <seealso cref="IMediator.SendAsync{TCommand,TResponse}"/>
/// <seealso cref="ICommandHandler{TCommand}"/>
[PublicAPI]
public interface ICommandResponseHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Asynchronously handles the specified command and returns a response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is invoked by the mediator when a command of type <typeparamref name="TCommand"/>
    /// is dispatched. The implementation should perform the necessary operations to process the command
    /// and return an appropriate response of type <typeparamref name="TResponse"/>.
    /// </para>
    /// <para>
    /// If the operation is canceled via the <paramref name="cancellationToken"/>, the implementation
    /// should throw an <see cref="OperationCanceledException"/> or allow the cancellation to propagate.
    /// </para>
    /// </remarks>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous handling operation,
    /// containing the response from the handler.</returns>
    ValueTask<TResponse> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken
    );
}
