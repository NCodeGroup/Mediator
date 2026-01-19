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
/// Defines the contract for a wrapper that invokes exception listeners for command pipelines.
/// </summary>
/// <remarks>
/// This interface enables polymorphic invocation of exception listeners when the exception type
/// is only known at runtime. The wrapper casts the exception to the appropriate type and invokes
/// the registered <see cref="ICommandExceptionListener{TCommand,TException}"/> implementations.
/// </remarks>
/// <typeparam name="TCommand">The type of the command that was being processed.</typeparam>
internal interface ICommandExceptionListenerWrapper<in TCommand>
    where TCommand : notnull
{
    /// <summary>
    /// Asynchronously notifies all registered exception listeners about the specified exception.
    /// </summary>
    /// <param name="command">The command that was being processed when the exception occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous notification operation.</returns>
    ValueTask ListenAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Wraps and invokes all registered <see cref="ICommandExceptionListener{TCommand,TException}"/>
/// implementations for a specific command and exception type combination.
/// </summary>
/// <remarks>
/// <para>
/// This wrapper is resolved by the standard middleware using reflection based on the runtime
/// exception type. It aggregates all registered listeners for the specific exception type
/// and invokes them in priority order.
/// </para>
/// <para>
/// Listeners are executed in descending order by <see cref="ISupportMediatorPriority.MediatorPriority"/>.
/// All listeners are invoked regardless of any state—listeners cannot suppress exceptions or
/// affect the pipeline flow.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command that was being processed.</typeparam>
/// <typeparam name="TException">The type of the exception to observe.</typeparam>
/// <param name="listeners">The collection of exception listeners to notify.</param>
internal class CommandExceptionListenerWrapper<TCommand, TException>(
    IEnumerable<ICommandExceptionListener<TCommand, TException>> listeners
) : ICommandExceptionListenerWrapper<TCommand>
    where TCommand : notnull
    where TException : Exception
{
    private IEnumerable<ICommandExceptionListener<TCommand, TException>> Listeners { get; } = listeners;

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
    public async ValueTask ListenAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        foreach (var listener in Sort(Listeners))
        {
            await listener.ListenAsync(
                command,
                (TException)exception,
                cancellationToken
            );
        }
    }
}
