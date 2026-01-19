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
/// Defines an exception listener that observes exceptions of type <typeparamref name="TException"/>
/// without the ability to handle or suppress them.
/// </summary>
/// <remarks>
/// <para>
/// Exception listeners provide a lightweight way to observe exceptions that occur during command
/// processing without affecting the exception's propagation. Unlike <see cref="ICommandExceptionHandler{TCommand,TException}"/>,
/// listeners cannot suppress exceptions—they are purely for observation purposes.
/// </para>
/// <para>
/// Listeners are useful for:
/// <list type="bullet">
///   <item><description>Logging exceptions to external systems</description></item>
///   <item><description>Recording error metrics and telemetry</description></item>
///   <item><description>Sending alerts or notifications</description></item>
///   <item><description>Auditing failed operations</description></item>
/// </list>
/// </para>
/// <para>
/// Multiple listeners can be registered for the same exception type. The execution order
/// can be controlled by implementing <see cref="ISupportMediatorPriority"/>. All registered
/// listeners are invoked regardless of whether exception handlers have marked the exception
/// as handled.
/// </para>
/// <para>
/// To handle or suppress exceptions, use <see cref="ICommandExceptionHandler{TCommand,TException}"/>
/// for command-only pipelines or <see cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/>
/// for command-response pipelines.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command that was being processed.</typeparam>
/// <typeparam name="TException">The type of the exception to listen for. Must derive from <see cref="Exception"/>.</typeparam>
/// <example>
/// <code>
/// public class ExceptionLoggingListener&lt;TCommand, TException&gt; : ICommandExceptionListener&lt;TCommand, TException&gt;
///     where TCommand : notnull
///     where TException : Exception
/// {
///     private readonly ILogger _logger;
///
///     public ValueTask ListenAsync(TCommand command, TException exception, CancellationToken cancellationToken)
///     {
///         _logger.LogError(exception, "Error processing {CommandType}", typeof(TCommand).Name);
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommandExceptionHandler{TCommand,TException}"/>
/// <seealso cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/>
/// <seealso cref="ISupportMediatorPriority"/>
[PublicAPI]
public interface ICommandExceptionListener<in TCommand, in TException>
    where TCommand : notnull
    where TException : Exception
{
    /// <summary>
    /// Asynchronously observes an exception that occurred during command processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is invoked when an exception of type <typeparamref name="TException"/> is thrown
    /// during the processing of a command. The listener can inspect the command and exception for
    /// logging, telemetry, or notification purposes.
    /// </para>
    /// <para>
    /// Unlike exception handlers, this method cannot suppress or handle the exception. The exception
    /// will propagate normally after all listeners have been invoked (unless an exception handler
    /// marks it as handled).
    /// </para>
    /// <para>
    /// Implementations should be careful not to throw exceptions, as this could interfere with
    /// exception handling and other listeners. Consider wrapping operations in try-catch blocks
    /// and logging any secondary errors.
    /// </para>
    /// </remarks>
    /// <param name="command">The command that was being processed when the exception occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous observation operation.</returns>
    ValueTask ListenAsync(
        TCommand command,
        TException exception,
        CancellationToken cancellationToken
    );
}
