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
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Mediator.Middleware;
using NCode.Mediator.Wrappers;

namespace NCode.Mediator;

/// <summary>
/// Provides extension methods for registering mediator services with the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// This class contains the <see cref="AddMediator"/> extension method which registers all
/// required services for the mediator library to function, including the mediator itself, handler
/// wrappers, middleware, and exception handling infrastructure.
/// </para>
/// </remarks>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers all mediator services with the dependency injection container.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method registers the following services:
        /// <list type="bullet">
        ///   <item><description><see cref="IMediator"/> - The main mediator for dispatching commands</description></item>
        ///   <item><description><see cref="ICommandHandlerWrapper{TCommand}"/> - Wrapper for command-only pipeline execution</description></item>
        ///   <item><description><see cref="ICommandResponseHandlerWrapper{TCommand,TResponse}"/> - Wrapper for command-response pipeline execution</description></item>
        ///   <item><description><see cref="ICommandMiddleware{TCommand}"/> - Standard middleware for command-only pipelines</description></item>
        ///   <item><description><see cref="ICommandResponseMiddleware{TCommand,TResponse}"/> - Standard middleware for command-response pipelines</description></item>
        ///   <item><description>Exception listener and handler wrappers for error handling infrastructure</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// All services are registered with scoped lifetime using <see cref="ServiceCollectionDescriptorExtensions.TryAddScoped{TService,TImplementation}(IServiceCollection)"/>,
        /// which means they will not overwrite existing registrations. This allows applications to
        /// provide custom implementations if needed.
        /// </para>
        /// <para>
        /// After calling this method, you should register your command handlers, middleware,
        /// pre/post processors, and exception handlers with the service collection.
        /// </para>
        /// </remarks>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddMediatorLibrary();
        ///
        /// // Register your handlers
        /// services.AddScoped&lt;ICommandHandler&lt;SendEmailCommand&gt;, SendEmailHandler&gt;();
        /// services.AddScoped&lt;ICommandResponseHandler&lt;GetUserQuery, User&gt;, GetUserHandler&gt;();
        /// </code>
        /// </example>
        [PublicAPI]
        public IServiceCollection AddMediator()
        {
            serviceCollection.TryAddScoped(
                typeof(ICommandHandlerWrapper<>),
                typeof(CommandHandlerWrapper<>)
            );

            serviceCollection.TryAddScoped(
                typeof(ICommandResponseHandlerWrapper<,>),
                typeof(CommandResponseHandlerWrapper<,>)
            );

            serviceCollection.TryAddScoped(
                typeof(CommandExceptionListenerWrapper<,>)
            );

            serviceCollection.TryAddScoped(
                typeof(CommandExceptionHandlerWrapper<,>)
            );

            serviceCollection.TryAddScoped(
                typeof(CommandResponseExceptionHandlerWrapper<,,>)
            );

            serviceCollection.TryAddScoped(
                typeof(ICommandMiddleware<>),
                typeof(StandardCommandMiddleware<>)
            );

            serviceCollection.TryAddScoped(
                typeof(ICommandResponseMiddleware<,>),
                typeof(StandardCommandResponseMiddleware<,>)
            );

            serviceCollection.TryAddScoped<
                IMediator,
                DefaultMediator
            >();

            return serviceCollection;
        }
    }
}
