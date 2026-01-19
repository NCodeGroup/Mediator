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
/// Defines a post-processor that executes after the command handler in command-response pipelines,
/// with access to both the command and the response.
/// </summary>
/// <remarks>
/// <para>
/// Post-processors provide a simplified way to execute logic after the command handler completes
/// successfully. Unlike middleware, post-processors cannot modify the response—they simply observe
/// the command and response after processing.
/// </para>
/// <para>
/// Post-processors are useful for:
/// <list type="bullet">
///   <item><description>Logging command completion with response details</description></item>
///   <item><description>Publishing domain events based on the response</description></item>
///   <item><description>Updating caches with the response data</description></item>
///   <item><description>Recording metrics or audit trails</description></item>
///   <item><description>Triggering side effects based on the response</description></item>
/// </list>
/// </para>
/// <para>
/// Multiple post-processors can be registered for the same command-response type. The execution order
/// can be controlled by implementing <see cref="ISupportMediatorPriority"/>. Post-processors
/// execute after the handler but before middleware completes its post-processing phase.
/// </para>
/// <para>
/// Post-processors only execute if the handler completes successfully. If the handler throws
/// an exception, post-processors are skipped. To handle exceptions, use
/// <see cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/>.
/// </para>
/// <para>
/// To modify the response, use <see cref="ICommandResponseMiddleware{TCommand,TResponse}"/> instead.
/// For command-only pipelines (commands without responses), use <see cref="ICommandPostProcessor{TCommand}"/>.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command. Must implement <see cref="ICommand{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of the response from the handler.</typeparam>
/// <example>
/// <code>
/// public class ResponseLoggingPostProcessor&lt;TCommand, TResponse&gt; : ICommandResponsePostProcessor&lt;TCommand, TResponse&gt;
///     where TCommand : ICommand&lt;TResponse&gt;
/// {
///     private readonly ILogger _logger;
///
///     public ValueTask PostProcessAsync(TCommand command, TResponse response, CancellationToken cancellationToken)
///     {
///         _logger.LogInformation("Command {CommandType} returned {Response}", typeof(TCommand).Name, response);
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommandPostProcessor{TCommand}"/>
/// <seealso cref="ICommandResponseMiddleware{TCommand,TResponse}"/>
/// <seealso cref="ICommandPreProcessor{TCommand}"/>
/// <seealso cref="ISupportMediatorPriority"/>
[PublicAPI]
public interface ICommandResponsePostProcessor<in TCommand, in TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Asynchronously post-processes the command and response after the handler has executed successfully.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is invoked after the command handler completes successfully. Use it to
    /// perform logging, event publishing, cache updates, or other follow-up work that requires
    /// access to both the command and the response.
    /// </para>
    /// <para>
    /// This method cannot modify the response. To modify responses, implement
    /// <see cref="ICommandResponseMiddleware{TCommand,TResponse}"/> instead.
    /// </para>
    /// <para>
    /// This method is not called if the handler throws an exception. To handle exceptions,
    /// implement <see cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/>.
    /// </para>
    /// <para>
    /// If the operation is canceled via the <paramref name="cancellationToken"/>, the implementation
    /// should throw an <see cref="OperationCanceledException"/> or allow the cancellation to propagate.
    /// </para>
    /// </remarks>
    /// <param name="command">The command that was processed.</param>
    /// <param name="response">The response returned by the handler.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous post-processing operation.</returns>
    ValueTask PostProcessAsync(
        TCommand command,
        TResponse response,
        CancellationToken cancellationToken
    );
}
