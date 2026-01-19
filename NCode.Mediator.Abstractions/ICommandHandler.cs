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
/// Defines a handler that processes commands of type <typeparamref name="TCommand"/> without returning a response.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create handlers for commands that implement <see cref="ICommand"/>.
/// Handlers are invoked by the <see cref="IMediator"/> when a matching command is dispatched
/// via <see cref="IMediator.SendAsync{TCommand}"/> or <see cref="IMediator.PolymorphicSendAsync"/>.
/// </para>
/// <para>
/// Multiple handlers can be registered for the same command type. When a command is dispatched,
/// all registered handlers will be invoked. The execution order of handlers can be controlled
/// by implementing <see cref="ISupportMediatorPriority"/>.
/// </para>
/// <para>
/// Handlers are typically registered with the dependency injection container and resolved
/// by the mediator at dispatch time.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to handle. Must implement <see cref="ICommand"/>.</typeparam>
/// <example>
/// <code>
/// public class SendEmailHandler : ICommandHandler&lt;SendEmailCommand&gt;
/// {
///     public async ValueTask HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
///     {
///         // Handle the command
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommand"/>
/// <seealso cref="IMediator.SendAsync{TCommand}"/>
/// <seealso cref="ISupportMediatorPriority"/>
[PublicAPI]
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Asynchronously handles the specified command.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is invoked by the mediator when a command of type <typeparamref name="TCommand"/>
    /// is dispatched. The implementation should perform the necessary operations to process the command.
    /// </para>
    /// <para>
    /// If the operation is canceled via the <paramref name="cancellationToken"/>, the implementation
    /// should throw an <see cref="OperationCanceledException"/> or allow the cancellation to propagate.
    /// </para>
    /// </remarks>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous handling operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        CancellationToken cancellationToken
    );
}
