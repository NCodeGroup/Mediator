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
/// Defines the contract for a strongly-typed wrapper that executes the command-only pipeline
/// for a specific command type.
/// </summary>
/// <remarks>
/// This interface is used by <see cref="DefaultMediator"/> to invoke the pipeline with compile-time
/// type information, enabling AOT compatibility.
/// </remarks>
/// <typeparam name="TCommand">The type of the command to handle.</typeparam>
internal interface ICommandHandlerWrapper<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Asynchronously executes the command pipeline including middleware and handlers.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Defines the contract for a non-generic wrapper that executes the command-only pipeline
/// using polymorphic dispatch.
/// </summary>
/// <remarks>
/// This interface is used by <see cref="DefaultMediator.PolymorphicSendAsync"/> to invoke the pipeline
/// when the command type is only known at runtime. The implementation casts the command to the
/// appropriate type.
/// </remarks>
internal interface ICommandHandlerWrapper
{
    /// <summary>
    /// Asynchronously executes the command pipeline including middleware and handlers.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask HandleAsync(
        ICommand command,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Wraps the command-only pipeline, composing middleware and handlers into a single execution chain.
/// </summary>
/// <remarks>
/// <para>
/// This wrapper builds a middleware pipeline at construction time by composing all registered
/// <see cref="ICommandMiddleware{TCommand}"/> implementations around the command handlers.
/// The pipeline is built once and reused for all subsequent command invocations.
/// </para>
/// <para>
/// The execution order is:
/// <list type="number">
///   <item><description>Middleware executes in descending priority order (outermost to innermost)</description></item>
///   <item><description>All <see cref="ICommandHandler{TCommand}"/> implementations execute in descending priority order</description></item>
///   <item><description>Middleware post-processing executes in reverse order (innermost to outermost)</description></item>
/// </list>
/// </para>
/// <para>
/// This class implements both <see cref="ICommandHandlerWrapper{TCommand}"/> for strongly-typed
/// invocation and <see cref="ICommandHandlerWrapper"/> for polymorphic invocation.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command to handle.</typeparam>
internal class CommandHandlerWrapper<TCommand> : ICommandHandlerWrapper<TCommand>, ICommandHandlerWrapper
    where TCommand : ICommand
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    /// <summary>
    /// Represents a delegate for the middleware chain that processes a command.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public delegate ValueTask MiddlewareChainDelegate(
        TCommand command,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandlerWrapper{TCommand}"/> class,
    /// building the middleware pipeline from the provided handlers and middleware.
    /// </summary>
    /// <param name="handlers">The collection of command handlers to invoke at the end of the pipeline.</param>
    /// <param name="middlewares">The collection of middleware to compose around the handlers.</param>
    public CommandHandlerWrapper(
        IEnumerable<ICommandHandler<TCommand>> handlers,
        IEnumerable<ICommandMiddleware<TCommand>> middlewares
    )
    {
        MiddlewareChain = Sort(middlewares).Select(WrapMiddleware).Aggregate(
            WrapRoot(handlers),
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
    /// Creates the innermost delegate that invokes all command handlers in priority order.
    /// </summary>
    /// <param name="handlers">The collection of handlers to invoke.</param>
    /// <returns>A delegate that invokes all handlers sequentially.</returns>
    public static MiddlewareChainDelegate WrapRoot(
        IEnumerable<ICommandHandler<TCommand>> handlers
    ) =>
        async (command, cancellationToken) =>
        {
            foreach (var handler in Sort(handlers))
            {
                await handler.HandleAsync(command, cancellationToken);
            }
        };

    /// <summary>
    /// Wraps a middleware component into a factory function that composes with the next delegate.
    /// </summary>
    /// <param name="middleware">The middleware to wrap.</param>
    /// <returns>A factory function that creates a delegate wrapping the middleware around the next delegate.</returns>
    public static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> WrapMiddleware(
        ICommandMiddleware<TCommand> middleware
    ) =>
        next => (command, cancellationToken) =>
        {
            return middleware.HandleAsync(command, SimpleNextAsync, cancellationToken);
            ValueTask SimpleNextAsync() => next(command, cancellationToken);
        };

    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        MiddlewareChain(command, cancellationToken);

    /// <inheritdoc />
    public ValueTask HandleAsync(ICommand command, CancellationToken cancellationToken) =>
        MiddlewareChain((TCommand)command, cancellationToken);
}
