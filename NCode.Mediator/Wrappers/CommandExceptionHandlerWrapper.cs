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
/// Defines the contract for a wrapper that invokes exception handlers for command-only pipelines.
/// </summary>
/// <remarks>
/// This interface enables polymorphic invocation of exception handlers when the exception type
/// is only known at runtime. The wrapper casts the exception to the appropriate type and invokes
/// the registered <see cref="ICommandExceptionHandler{TCommand,TException}"/> implementations.
/// </remarks>
/// <typeparam name="TCommand">The type of the command that was being processed.</typeparam>
internal interface ICommandExceptionHandlerWrapper<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Asynchronously invokes all registered exception handlers for the specified exception.
    /// </summary>
    /// <param name="command">The command that was being processed when the exception occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="state">The state object used to indicate whether the exception has been handled.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        Exception exception,
        CommandExceptionHandlerState state,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Wraps and invokes all registered <see cref="ICommandExceptionHandler{TCommand,TException}"/>
/// implementations for a specific command and exception type combination.
/// </summary>
/// <remarks>
/// <para>
/// This wrapper is resolved by the <see cref="Middleware.StandardCommandMiddleware{TCommand}"/>
/// using reflection based on the runtime exception type. It aggregates all registered handlers
/// for the specific exception type and invokes them in priority order.
/// </para>
/// <para>
/// Handlers are executed in descending order by <see cref="ISupportMediatorPriority.MediatorPriority"/>.
/// Execution stops early if a handler marks the exception as handled via
/// <see cref="CommandExceptionHandlerState.SetHandled"/>.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command that was being processed.</typeparam>
/// <typeparam name="TException">The type of the exception to handle.</typeparam>
/// <param name="handlers">The collection of exception handlers to invoke.</param>
internal class CommandExceptionHandlerWrapper<TCommand, TException>(
    IEnumerable<ICommandExceptionHandler<TCommand, TException>> handlers
) : ICommandExceptionHandlerWrapper<TCommand>
    where TCommand : ICommand
    where TException : Exception
{
    private IEnumerable<ICommandExceptionHandler<TCommand, TException>> Handlers { get; } = handlers;

    /// <summary>
    /// Sorts a collection of items by their <see cref="ISupportMediatorPriority.MediatorPriority"/>
    /// in descending order (higher priorities first).
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to sort.</param>
    /// <returns>The sorted collection with higher priority items first.</returns>
    public static IEnumerable<T> Sort<T>(IEnumerable<T> collection) =>
        collection.OrderByDescending(item => item is ISupportMediatorPriority support ? support.MediatorPriority : 0);

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        TCommand command,
        Exception exception,
        CommandExceptionHandlerState state,
        CancellationToken cancellationToken
    )
    {
        foreach (var handler in Sort(Handlers))
        {
            await handler.HandleAsync(
                command,
                (TException)exception,
                state,
                cancellationToken
            );

            if (state.IsHandled) return;
        }
    }
}
