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
/// Represents a delegate that invokes the next middleware component in the command-response pipeline
/// and returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// <para>
/// This delegate is passed to each middleware component to allow it to invoke the next component
/// in the pipeline and receive the response. Middleware can choose to:
/// <list type="bullet">
///   <item><description>Call the delegate to continue processing and receive the response</description></item>
///   <item><description>Not call the delegate and return a response directly to short-circuit the pipeline</description></item>
///   <item><description>Modify the response returned by the delegate before returning it</description></item>
///   <item><description>Perform work before and/or after calling the delegate</description></item>
/// </list>
/// </para>
/// <para>
/// The delegate is declared without arguments to enable contravariance on
/// <see cref="ICommandResponseMiddleware{TCommand,TResponse}"/>, allowing middleware registered for a base
/// command type to be used with derived command types in dependency injection scenarios.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">The type of the response returned by the pipeline.</typeparam>
/// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous execution of the remaining
/// pipeline, containing the response.</returns>
[PublicAPI]
public delegate ValueTask<TResponse> CommandResponseMiddlewareDelegate<TResponse>();

/// <summary>
/// Defines a middleware component that intercepts command processing in the pipeline for commands
/// that return a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// <para>
/// Middleware components form a pipeline that wraps the execution of command handlers. Each middleware
/// can perform operations before and after the next component in the pipeline, and can inspect or
/// modify the response. This enables cross-cutting concerns such as logging, validation, caching,
/// and error handling.
/// </para>
/// <para>
/// Middleware is executed in order of priority (via <see cref="ISupportMediatorPriority"/>) with
/// the command handler at the end of the pipeline. The execution flows through middleware in
/// priority order, then back through each middleware in reverse order after the handler completes,
/// allowing each middleware to process the response.
/// </para>
/// <para>
/// Common use cases include:
/// <list type="bullet">
///   <item><description>Logging request/response information</description></item>
///   <item><description>Validating commands before processing</description></item>
///   <item><description>Caching responses for repeated commands</description></item>
///   <item><description>Transforming or enriching responses</description></item>
///   <item><description>Implementing retry logic or circuit breakers</description></item>
///   <item><description>Managing transactions or unit of work patterns</description></item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to intercept. Must implement <see cref="ICommand{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <example>
/// <code>
/// public class CachingMiddleware&lt;TCommand, TResponse&gt; : ICommandResponseMiddleware&lt;TCommand, TResponse&gt;
///     where TCommand : ICommand&lt;TResponse&gt;
/// {
///     private readonly ICache _cache;
///
///     public async ValueTask&lt;TResponse&gt; HandleAsync(TCommand command, CommandResponseMiddlewareDelegate&lt;TResponse&gt; next, CancellationToken cancellationToken)
///     {
///         var cacheKey = GetCacheKey(command);
///         if (_cache.TryGet(cacheKey, out TResponse cached))
///             return cached;
///
///         var response = await next();
///         _cache.Set(cacheKey, response);
///         return response;
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICommand{TResponse}"/>
/// <seealso cref="ICommandResponseHandler{TCommand,TResponse}"/>
/// <seealso cref="ISupportMediatorPriority"/>
/// <seealso cref="ICommandMiddleware{TCommand}"/>
[PublicAPI]
public interface ICommandResponseMiddleware<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command by optionally performing work before and/or after invoking the next middleware
    /// in the pipeline, and returns a response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations should call the <paramref name="next"/> delegate to continue processing through
    /// the middleware pipeline and receive the response. Not calling <paramref name="next"/> will
    /// short-circuit the pipeline, preventing subsequent middleware and the handler from executing;
    /// in this case, the middleware must return an appropriate response directly.
    /// </para>
    /// <para>
    /// Work can be performed before calling <paramref name="next"/> (pre-processing) and after
    /// <paramref name="next"/> returns (post-processing). The response from <paramref name="next"/>
    /// can be inspected, modified, or replaced before being returned. Exceptions thrown by subsequent
    /// middleware or the handler will propagate back through the pipeline unless caught.
    /// </para>
    /// </remarks>
    /// <param name="command">The command being processed.</param>
    /// <param name="next">The delegate to invoke the next middleware component in the pipeline.
    /// Must be called to continue processing unless intentionally short-circuiting with a direct response.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous middleware operation,
    /// containing the response to return to the caller.</returns>
    ValueTask<TResponse> HandleAsync(
        TCommand command,
        CommandResponseMiddlewareDelegate<TResponse> next,
        CancellationToken cancellationToken
    );
}
