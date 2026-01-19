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
/// Represents a delegate that invokes the next middleware component in the command pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This delegate is passed to each middleware component to allow it to invoke the next component
/// in the pipeline. Middleware can choose to:
/// <list type="bullet">
///   <item><description>Call the delegate to continue processing through the pipeline</description></item>
///   <item><description>Not call the delegate to short-circuit the pipeline</description></item>
///   <item><description>Perform work before and/or after calling the delegate</description></item>
/// </list>
/// </para>
/// <para>
/// The delegate is declared without arguments to enable contravariance on
/// <see cref="ICommandMiddleware{TCommand}"/>, allowing middleware registered for a base
/// command type to be used with derived command types in dependency injection scenarios.
/// </para>
/// </remarks>
/// <returns>A <see cref="ValueTask"/> that represents the asynchronous execution of the remaining pipeline.</returns>
[PublicAPI]
public delegate ValueTask CommandMiddlewareDelegate();

/// <summary>
/// Defines a middleware component that intercepts command processing in the pipeline for commands
/// that do not return a response.
/// </summary>
/// <remarks>
/// <para>
/// Middleware components form a pipeline that wraps the execution of command handlers. Each middleware
/// can perform operations before and after the next component in the pipeline, enabling cross-cutting
/// concerns such as logging, validation, caching, and error handling.
/// </para>
/// <para>
/// Middleware is executed in order of priority (via <see cref="ISupportMediatorPriority"/>) with
/// the command handler at the end of the pipeline. The execution flows through middleware in
/// priority order, then back through each middleware in reverse order after the handler completes.
/// </para>
/// <para>
/// Common use cases include:
/// <list type="bullet">
///   <item><description>Logging request/response information</description></item>
///   <item><description>Validating commands before processing</description></item>
///   <item><description>Implementing retry logic or circuit breakers</description></item>
///   <item><description>Managing transactions or unit of work patterns</description></item>
///   <item><description>Performance monitoring and metrics collection</description></item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to intercept. Must implement <see cref="ICommand"/>.</typeparam>
/// <example>
/// <code>
/// public class LoggingMiddleware&lt;TCommand&gt; : ICommandMiddleware&lt;TCommand&gt;
///     where TCommand : ICommand
/// {
///     public async ValueTask HandleAsync(TCommand command, CommandMiddlewareDelegate next, CancellationToken cancellationToken)
///     {
///         Console.WriteLine($"Processing {typeof(TCommand).Name}");
///         await next();
///         Console.WriteLine($"Completed {typeof(TCommand).Name}");
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommand"/>
/// <seealso cref="ICommandHandler{TCommand}"/>
/// <seealso cref="ISupportMediatorPriority"/>
/// <seealso cref="ICommandResponseMiddleware{TCommand,TResponse}"/>
[PublicAPI]
public interface ICommandMiddleware<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles the command by optionally performing work before and/or after invoking the next middleware in the pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations should call the <paramref name="next"/> delegate to continue processing through
    /// the middleware pipeline. Not calling <paramref name="next"/> will short-circuit the pipeline,
    /// preventing subsequent middleware and the handler from executing.
    /// </para>
    /// <para>
    /// Work can be performed before calling <paramref name="next"/> (pre-processing) and after
    /// <paramref name="next"/> returns (post-processing). Exceptions thrown by subsequent middleware
    /// or the handler will propagate back through the pipeline unless caught.
    /// </para>
    /// </remarks>
    /// <param name="command">The command being processed.</param>
    /// <param name="next">The delegate to invoke the next middleware component in the pipeline.
    /// Must be called to continue processing unless intentionally short-circuiting.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous middleware operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        CommandMiddlewareDelegate next,
        CancellationToken cancellationToken
    );
}
