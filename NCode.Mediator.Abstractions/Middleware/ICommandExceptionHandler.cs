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

namespace NCode.Mediator.Middleware;

/// <summary>
/// Defines an exception handler for command-only pipelines that can handle exceptions of type
/// <typeparamref name="TException"/> and optionally suppress them.
/// </summary>
/// <remarks>
/// <para>
/// Exception handlers provide a way to intercept and handle exceptions that occur during command
/// processing in command-only pipelines. When an exception of type <typeparamref name="TException"/>
/// is thrown, registered handlers are invoked in priority order (via <see cref="ISupportMediatorPriority"/>).
/// </para>
/// <para>
/// Handlers can choose to:
/// <list type="bullet">
///   <item><description>Handle the exception by calling <see cref="CommandExceptionHandlerState.SetHandled"/>,
///   which prevents the exception from propagating</description></item>
///   <item><description>Log or observe the exception without handling it, allowing subsequent handlers
///   or the exception to propagate</description></item>
///   <item><description>Throw a different exception to replace the original</description></item>
/// </list>
/// </para>
/// <para>
/// If no handler marks the exception as handled, the original exception is re-thrown after all
/// handlers have been invoked.
/// </para>
/// <para>
/// For command-response pipelines (commands that return a value), use
/// <see cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/> instead, which allows
/// providing a fallback response. For observation-only scenarios where you don't need to handle
/// the exception, consider <see cref="ICommandExceptionListener{TCommand,TException}"/>.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command. Must implement <see cref="ICommand"/>.</typeparam>
/// <typeparam name="TException">The type of the exception to handle. Must derive from <see cref="Exception"/>.</typeparam>
/// <example>
/// <code>
/// public class TransientExceptionHandler : ICommandExceptionHandler&lt;SendEmailCommand, TransientException&gt;
/// {
///     private readonly ILogger _logger;
///
///     public ValueTask HandleAsync(
///         SendEmailCommand command,
///         TransientException exception,
///         CommandExceptionHandlerState state,
///         CancellationToken cancellationToken)
///     {
///         _logger.LogWarning(exception, "Transient error sending email, suppressing...");
///         state.SetHandled();
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="CommandExceptionHandlerState"/>
/// <seealso cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/>
/// <seealso cref="ICommandExceptionListener{TCommand,TException}"/>
/// <seealso cref="ISupportMediatorPriority"/>
[PublicAPI]
public interface ICommandExceptionHandler<in TCommand, in TException>
    where TCommand : ICommand
    where TException : Exception
{
    /// <summary>
    /// Asynchronously handles an exception that occurred during command processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is invoked when an exception of type <typeparamref name="TException"/> is thrown
    /// during the processing of a command. The handler can inspect the command and exception to
    /// determine the appropriate action.
    /// </para>
    /// <para>
    /// To handle the exception and prevent it from propagating, call
    /// <see cref="CommandExceptionHandlerState.SetHandled"/> on the <paramref name="state"/> parameter.
    /// If the exception is not handled by any handler, it will be re-thrown after all handlers
    /// have been invoked.
    /// </para>
    /// <para>
    /// Multiple handlers may be registered for the same exception type. Once any handler marks the
    /// exception as handled, subsequent handlers will still be invoked but the exception will not
    /// be re-thrown.
    /// </para>
    /// </remarks>
    /// <param name="command">The command that was being processed when the exception occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="state">The state object used to indicate whether the exception has been handled.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous exception handling operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        TException exception,
        CommandExceptionHandlerState state,
        CancellationToken cancellationToken
    );
}
