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
/// Defines a post-processor that executes after the command handler in both command-only and
/// command-response pipelines.
/// </summary>
/// <remarks>
/// <para>
/// Post-processors provide a simplified way to execute logic after the command handler completes
/// successfully without the complexity of full middleware. Unlike middleware, post-processors
/// cannot modify the response—they simply execute after the handler.
/// </para>
/// <para>
/// Post-processors are useful for:
/// <list type="bullet">
///   <item><description>Logging command completion</description></item>
///   <item><description>Publishing domain events or notifications</description></item>
///   <item><description>Updating caches or external systems</description></item>
///   <item><description>Cleaning up resources or context</description></item>
///   <item><description>Recording metrics or audit trails</description></item>
/// </list>
/// </para>
/// <para>
/// Multiple post-processors can be registered for the same command type. The execution order
/// can be controlled by implementing <see cref="ISupportMediatorPriority"/>. Post-processors
/// execute after the handler but before middleware completes its post-processing phase.
/// </para>
/// <para>
/// Post-processors only execute if the handler completes successfully. If the handler throws
/// an exception, post-processors are skipped. To handle exceptions, use
/// <see cref="ICommandExceptionHandler{TCommand, TException}"/> or <see cref="ICommandExceptionListener{TCommand, TException}"/>.
/// </para>
/// <para>
/// To modify the response in command-response pipelines, use
/// <see cref="ICommandResponseMiddleware{TCommand,TResponse}"/> or
/// <see cref="ICommandResponsePostProcessor{TCommand,TResponse}"/> instead.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to post-process.</typeparam>
/// <example>
/// <code>
/// public class AuditPostProcessor&lt;TCommand&gt; : ICommandPostProcessor&lt;TCommand&gt;
///     where TCommand : notnull
/// {
///     private readonly IAuditLog _auditLog;
///
///     public async ValueTask PostProcessAsync(TCommand command, CancellationToken cancellationToken)
///     {
///         await _auditLog.RecordAsync($"Processed {typeof(TCommand).Name}", cancellationToken);
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommandPreProcessor{TCommand}"/>
/// <seealso cref="ICommandResponsePostProcessor{TCommand,TResponse}"/>
/// <seealso cref="ICommandMiddleware{TCommand}"/>
/// <seealso cref="ISupportMediatorPriority"/>
[PublicAPI]
public interface ICommandPostProcessor<in TCommand>
    where TCommand : notnull
{
    /// <summary>
    /// Asynchronously post-processes the command after the handler has executed successfully.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is invoked after the command handler completes successfully. Use it to
    /// perform logging, event publishing, cache updates, or other follow-up work.
    /// </para>
    /// <para>
    /// This method is not called if the handler throws an exception. To handle exceptions,
    /// implement <see cref="ICommandExceptionHandler{TCommand, TException}"/> or <see cref="ICommandExceptionListener{TCommand, TException}"/>.
    /// </para>
    /// <para>
    /// If the operation is canceled via the <paramref name="cancellationToken"/>, the implementation
    /// should throw an <see cref="OperationCanceledException"/> or allow the cancellation to propagate.
    /// </para>
    /// </remarks>
    /// <param name="command">The command that was processed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous post-processing operation.</returns>
    ValueTask PostProcessAsync(
        TCommand command,
        CancellationToken cancellationToken
    );
}
