#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Mediator;

/// <summary>
/// Defines a contract for components that support prioritized ordering during mediator operations.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface on handlers, middleware, processors, or other mediator components
/// to control the order in which they are executed when multiple components of the same type
/// are registered for a command.
/// </para>
/// <para>
/// Components are sorted by <see cref="MediatorPriority"/> in descending order, meaning higher
/// values execute first. Components that do not implement this interface are treated as having
/// a priority of zero.
/// </para>
/// <para>
/// Use <see cref="DefaultMediatorPriorities"/> for common priority values:
/// <list type="bullet">
///   <item><description><see cref="DefaultMediatorPriorities.High"/> (100) - Execute before most other components</description></item>
///   <item><description>0 (default) - Standard execution order</description></item>
///   <item><description><see cref="DefaultMediatorPriorities.Low"/> (-100) - Execute after most other components</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class LoggingMiddleware : ICommandMiddleware&lt;MyCommand&gt;, ISupportMediatorPriority
/// {
///     // Execute early to log all requests
///     public int MediatorPriority => DefaultMediatorPriorities.High;
///
///     public async ValueTask InvokeAsync(MyCommand command, CommandMiddlewareDelegate&lt;MyCommand&gt; next, CancellationToken cancellationToken)
///     {
///         // Logging logic here
///         await next(command, cancellationToken);
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="DefaultMediatorPriorities"/>
/// <seealso cref="ICommandHandler{TCommand}"/>
[PublicAPI]
public interface ISupportMediatorPriority
{
    /// <summary>
    /// Gets the numeric priority that determines the execution order relative to other components of the same type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Higher values indicate higher priority and cause the component to execute earlier in the pipeline.
    /// Lower values cause the component to execute later. The default priority for components that do not
    /// implement this interface is zero.
    /// </para>
    /// <para>
    /// Consider using <see cref="DefaultMediatorPriorities.High"/> or <see cref="DefaultMediatorPriorities.Low"/>
    /// for consistent priority values across the application.
    /// </para>
    /// </remarks>
    /// <value>
    /// The priority value. Higher values execute first; lower values execute last. Default is zero.
    /// </value>
    int MediatorPriority { get; }
}
