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
/// Marker interface that defines a command which does not return a response when handled.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface on value types (structs) that represent commands to be dispatched
/// through the <see cref="IMediator"/>. Commands implementing this interface are processed by
/// <see cref="ICommandHandler{TCommand}"/> implementations.
/// </para>
/// <para>
/// Since commands implementing this interface do not return a response, they may be processed
/// by zero, one, or multiple handlers. This is useful for notification-style commands where
/// the sender does not need confirmation of handling.
/// </para>
/// <para>
/// Commands should be implemented as structs to avoid heap allocations and improve performance.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public readonly struct SendEmailCommand : ICommand
/// {
///     public required string To { get; init; }
///     public required string Subject { get; init; }
///     public required string Body { get; init; }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommand{TResponse}"/>
/// <seealso cref="IMediator.SendAsync{TCommand}"/>
/// <seealso cref="ICommandHandler{TCommand}"/>
[PublicAPI]
public interface ICommand
{
    // nothing
}

/// <summary>
/// Marker interface that defines a command which returns a response of type <typeparamref name="TResponse"/> when handled.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface on value types (structs) that represent commands to be dispatched
/// through the <see cref="IMediator"/>. Commands implementing this interface are processed by
/// <see cref="ICommandResponseHandler{TCommand,TResponse}"/> implementations.
/// </para>
/// <para>
/// Since commands implementing this interface return a response, exactly one handler must be
/// registered to process each command type. This is useful for request-response style commands
/// where the sender expects a result.
/// </para>
/// <para>
/// Commands should be implemented as structs to avoid heap allocations and improve performance.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <example>
/// <code>
/// public readonly struct GetUserQuery : ICommand&lt;User&gt;
/// {
///     public required Guid UserId { get; init; }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommand"/>
/// <seealso cref="IMediator.SendAsync{TCommand,TResponse}"/>
/// <seealso cref="ICommandResponseHandler{TCommand,TResponse}"/>
[PublicAPI]
public interface ICommand<out TResponse>
{
    // nothing
}
