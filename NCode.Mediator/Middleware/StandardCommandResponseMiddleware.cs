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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using NCode.Mediator.Wrappers;

namespace NCode.Mediator.Middleware;

/// <summary>
/// Provides the standard middleware implementation for command-response pipelines that orchestrates
/// pre-processors, post-processors, exception listeners, and exception handlers.
/// </summary>
/// <remarks>
/// <para>
/// This middleware is automatically registered by <see cref="DefaultRegistration"/>
/// and provides the core pipeline behavior for commands that return a response.
/// </para>
/// <para>
/// The execution flow is:
/// <list type="number">
///   <item><description>Execute all <see cref="ICommandPreProcessor{TCommand}"/> implementations in priority order</description></item>
///   <item><description>Invoke the next middleware/handler in the pipeline and capture the response</description></item>
///   <item><description>Execute all <see cref="ICommandResponsePostProcessor{TCommand,TResponse}"/> implementations in priority order</description></item>
///   <item><description>Return the response</description></item>
/// </list>
/// </para>
/// <para>
/// If an exception occurs at any point:
/// <list type="number">
///   <item><description>All <see cref="ICommandExceptionListener{TCommand,TException}"/> implementations are notified</description></item>
///   <item><description>All <see cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/> implementations are invoked</description></item>
///   <item><description>If a handler provides a fallback response, that response is returned</description></item>
///   <item><description>If no handler handles the exception, it is re-thrown</description></item>
/// </list>
/// </para>
/// <para>
/// This class uses reflection to resolve exception listeners and handlers based on the runtime
/// exception type, which requires dynamic code generation and is not AOT-compatible.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command being processed.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <param name="serviceProvider">The service provider for resolving exception handling wrappers.</param>
/// <param name="preProcessors">The collection of pre-processors to execute before the handler.</param>
/// <param name="postProcessors">The collection of post-processors to execute after the handler.</param>
[RequiresDynamicCode("This class uses reflection for exception handling.")]
internal class StandardCommandResponseMiddleware<TCommand, TResponse>(
    IServiceProvider serviceProvider,
    IEnumerable<ICommandPreProcessor<TCommand>> preProcessors,
    IEnumerable<ICommandResponsePostProcessor<TCommand, TResponse>> postProcessors
) : ICommandResponseMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private IEnumerable<ICommandPreProcessor<TCommand>> PreProcessors { get; } = preProcessors;
    private IEnumerable<ICommandResponsePostProcessor<TCommand, TResponse>> PostProcessors { get; } = postProcessors;

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
    public async ValueTask<TResponse> HandleAsync(
        TCommand command,
        CommandResponseMiddlewareDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await PreProcessAsync(
                command,
                cancellationToken
            );

            var response = await next();

            await PostProcessAsync(
                command,
                response,
                cancellationToken
            );

            return response;
        }
        catch (Exception exception)
        {
            await NotifyExceptionAsync(
                command,
                exception,
                cancellationToken
            );

            var state = await HandleExceptionAsync(
                command,
                exception,
                cancellationToken
            );

            if (state.IsHandled)
                return state.Response;

            throw;
        }
    }

    /// <summary>
    /// Executes all registered pre-processors in priority order.
    /// </summary>
    /// <param name="command">The command being processed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask PreProcessAsync(
        TCommand command,
        CancellationToken cancellationToken
    )
    {
        foreach (var processor in Sort(PreProcessors))
        {
            await processor.PreProcessAsync(
                command,
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Executes all registered post-processors in priority order.
    /// </summary>
    /// <param name="command">The command that was processed.</param>
    /// <param name="response">The response from the handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask PostProcessAsync(
        TCommand command,
        TResponse response,
        CancellationToken cancellationToken
    )
    {
        foreach (var processor in Sort(PostProcessors))
        {
            await processor.PostProcessAsync(
                command,
                response,
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Notifies all registered exception listeners about the exception.
    /// </summary>
    /// <param name="command">The command that was being processed when the exception occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous notification operation.</returns>
    public ValueTask NotifyExceptionAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var wrapperType = typeof(CommandExceptionListenerWrapper<,>).MakeGenericType(
            typeof(TCommand),
            exception.GetType()
        );

        var wrapper = (ICommandExceptionListenerWrapper<TCommand>)
            ServiceProvider.GetRequiredService(wrapperType);

        return wrapper.ListenAsync(
            command,
            exception,
            cancellationToken
        );
    }

    /// <summary>
    /// Invokes all registered exception handlers to potentially handle the exception and provide a fallback response.
    /// </summary>
    /// <param name="command">The command that was being processed when the exception occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The exception handler state indicating whether the exception was handled and any fallback response.</returns>
    public async ValueTask<CommandResponseExceptionHandlerState<TResponse>> HandleExceptionAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var state = new CommandResponseExceptionHandlerState<TResponse>();

        var wrapperType = typeof(CommandResponseExceptionHandlerWrapper<,,>).MakeGenericType(
            typeof(TCommand),
            exception.GetType(),
            typeof(TResponse)
        );

        var wrapper = (ICommandResponseExceptionHandlerWrapper<TCommand, TResponse>)
            ServiceProvider.GetRequiredService(wrapperType);

        await wrapper.HandleAsync(
            command,
            exception,
            state,
            cancellationToken
        );

        return state;
    }
}
