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
/// Defines a pre-processor that executes before the command handler in both command-only and
/// command-response pipelines.
/// </summary>
/// <remarks>
/// <para>
/// Pre-processors provide a simplified way to execute logic before the command handler without
/// the complexity of full middleware. Unlike middleware, pre-processors cannot short-circuit
/// the pipeline or modify the response—they simply execute before the handler.
/// </para>
/// <para>
/// Pre-processors are useful for:
/// <list type="bullet">
///   <item><description>Validating command data before processing</description></item>
///   <item><description>Logging incoming commands</description></item>
///   <item><description>Enriching or normalizing command data</description></item>
///   <item><description>Authorization checks</description></item>
///   <item><description>Setting up context or ambient state</description></item>
/// </list>
/// </para>
/// <para>
/// Multiple pre-processors can be registered for the same command type. The execution order
/// can be controlled by implementing <see cref="ISupportMediatorPriority"/>. Pre-processors
/// execute before middleware in the pipeline.
/// </para>
/// <para>
/// To short-circuit the pipeline or perform post-processing, use <see cref="ICommandMiddleware{TCommand}"/>
/// or <see cref="ICommandResponseMiddleware{TCommand,TResponse}"/> instead.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to pre-process.</typeparam>
/// <example>
/// <code>
/// public class ValidationPreProcessor&lt;TCommand&gt; : ICommandPreProcessor&lt;TCommand&gt;
///     where TCommand : notnull
/// {
///     private readonly IValidator&lt;TCommand&gt; _validator;
///
///     public async ValueTask PreProcessAsync(TCommand command, CancellationToken cancellationToken)
///     {
///         var result = await _validator.ValidateAsync(command, cancellationToken);
///         if (!result.IsValid)
///             throw new ValidationException(result.Errors);
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommandPostProcessor{TCommand}"/>
/// <seealso cref="ICommandMiddleware{TCommand}"/>
/// <seealso cref="ISupportMediatorPriority"/>
[PublicAPI]
public interface ICommandPreProcessor<in TCommand>
    where TCommand : notnull
{
    /// <summary>
    /// Asynchronously pre-processes the command before the handler executes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is invoked before the command handler and any middleware execute. Use it to
    /// perform validation, logging, or other preparatory work.
    /// </para>
    /// <para>
    /// To prevent the handler from executing, throw an exception. Pre-processors cannot
    /// short-circuit the pipeline silently—use middleware for that capability.
    /// </para>
    /// <para>
    /// If the operation is canceled via the <paramref name="cancellationToken"/>, the implementation
    /// should throw an <see cref="OperationCanceledException"/> or allow the cancellation to propagate.
    /// </para>
    /// </remarks>
    /// <param name="command">The command to pre-process.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous pre-processing operation.</returns>
    ValueTask PreProcessAsync(
        TCommand command,
        CancellationToken cancellationToken
    );
}
