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

using NCode.Mediator.Middleware;

namespace NCode.Mediator.Wrappers;

/// <summary>
/// Defines the contract for a non-generic wrapper that executes the command-response pipeline
/// using polymorphic dispatch.
/// </summary>
/// <remarks>
/// This interface is used by <see cref="DefaultMediator.PolymorphicSendAsync{TResponse}"/> to invoke
/// the pipeline when the command type is only known at runtime. The implementation casts the command
/// to the appropriate type.
/// </remarks>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
internal interface ICommandResponseHandlerWrapper<TResponse>
{
    /// <summary>
    /// Asynchronously executes the command pipeline including middleware and the handler.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the response.</returns>
    ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Defines the contract for a strongly-typed wrapper that executes the command-response pipeline
/// for a specific command type.
/// </summary>
/// <remarks>
/// This interface is used by <see cref="DefaultMediator"/> to invoke the pipeline with compile-time
/// type information, enabling AOT compatibility.
/// </remarks>
/// <typeparam name="TCommand">The type of the command to handle.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
internal interface ICommandResponseHandlerWrapper<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Asynchronously executes the command pipeline including middleware and the handler.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the response.</returns>
    ValueTask<TResponse> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Wraps the command-response pipeline, composing middleware and the handler into a single execution chain.
/// </summary>
/// <remarks>
/// <para>
/// This wrapper builds a middleware pipeline at construction time by composing all registered
/// <see cref="ICommandResponseMiddleware{TCommand,TResponse}"/> implementations around the command handler.
/// The pipeline is built once and reused for all subsequent command invocations.
/// </para>
/// <para>
/// The execution order is:
/// <list type="number">
///   <item><description>Middleware executes in descending priority order (outermost to innermost)</description></item>
///   <item><description>The <see cref="ICommandResponseHandler{TCommand,TResponse}"/> executes and produces a response</description></item>
///   <item><description>Middleware post-processing executes in reverse order (innermost to outermost)</description></item>
/// </list>
/// </para>
/// <para>
/// Unlike <see cref="CommandHandlerWrapper{TCommand}"/>, this wrapper expects exactly one handler
/// to be registered, as the handler must return a response.
/// </para>
/// <para>
/// This class implements both <see cref="ICommandResponseHandlerWrapper{TCommand,TResponse}"/> for
/// strongly-typed invocation and <see cref="ICommandResponseHandlerWrapper{TResponse}"/> for polymorphic invocation.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to handle.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
internal class CommandResponseHandlerWrapper<TCommand, TResponse> :
    ICommandResponseHandlerWrapper<TResponse>,
    ICommandResponseHandlerWrapper<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    /// <summary>
    /// Represents a delegate for the middleware chain that processes a command and returns a response.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the response.</returns>
    public delegate ValueTask<TResponse> MiddlewareChainDelegate(
        TCommand command,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponseHandlerWrapper{TCommand,TResponse}"/> class,
    /// building the middleware pipeline from the provided handler and middleware.
    /// </summary>
    /// <param name="handler">The command handler that produces the response.</param>
    /// <param name="middlewares">The collection of middleware to compose around the handler.</param>
    public CommandResponseHandlerWrapper(
        ICommandResponseHandler<TCommand, TResponse> handler,
        IEnumerable<ICommandResponseMiddleware<TCommand, TResponse>> middlewares
    )
    {
        MiddlewareChain = Sort(middlewares).Select(WrapMiddleware).Aggregate(
            (MiddlewareChainDelegate)handler.HandleAsync,
            (next, factory) => factory(next)
        );
    }

    /// <summary>
    /// Sorts a collection of items by their <see cref="ISupportMediatorPriority.MediatorPriority"/>
    /// in descending order (higher priorities first).
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to sort.</param>
    /// <returns>The sorted collection with higher priority items first.</returns>
    public static IEnumerable<T> Sort<T>(IEnumerable<T> collection) =>
        collection.OrderByDescending(item => item is ISupportMediatorPriority support ? support.MediatorPriority : 0);

    /// <summary>
    /// Wraps a middleware component into a factory function that composes with the next delegate.
    /// </summary>
    /// <param name="middleware">The middleware to wrap.</param>
    /// <returns>A factory function that creates a delegate wrapping the middleware around the next delegate.</returns>
    public static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> WrapMiddleware(
        ICommandResponseMiddleware<TCommand, TResponse> middleware
    ) =>
        next => (command, cancellationToken) =>
        {
            return middleware.HandleAsync(command, SimpleNextAsync, cancellationToken);
            ValueTask<TResponse> SimpleNextAsync() => next(command, cancellationToken);
        };

    /// <inheritdoc />
    public ValueTask<TResponse> HandleAsync(ICommand<TResponse> command, CancellationToken cancellationToken) =>
        MiddlewareChain((TCommand)command, cancellationToken);

    /// <inheritdoc />
    public ValueTask<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        MiddlewareChain(command, cancellationToken);
}
