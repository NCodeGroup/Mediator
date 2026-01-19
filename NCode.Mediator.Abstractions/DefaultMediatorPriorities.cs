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
/// Provides default priority values for mediator components that implement <see cref="ISupportMediatorPriority"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class defines well-known priority constants that can be used when implementing
/// <see cref="ISupportMediatorPriority"/> on handlers, middleware, processors, and other
/// mediator components to control their execution order.
/// </para>
/// <para>
/// Priority values determine the execution order of components when multiple components of the
/// same type are registered. Components are sorted in descending order by priority, meaning
/// higher values execute first.
/// </para>
/// <para>
/// The priority scale is:
/// <list type="table">
///   <listheader>
///     <term>Priority</term>
///     <description>Value and Usage</description>
///   </listheader>
///   <item>
///     <term><see cref="High"/></term>
///     <description>100 - Components that should execute early (e.g., logging, validation)</description>
///   </item>
///   <item>
///     <term>Default</term>
///     <description>0 - Standard components (implicit when <see cref="ISupportMediatorPriority"/> is not implemented)</description>
///   </item>
///   <item>
///     <term><see cref="Low"/></term>
///     <description>-100 - Components that should execute late (e.g., cleanup, finalization)</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// These values are mutable to allow applications to customize the default priority boundaries
/// if needed, though this is generally not recommended.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class ValidationMiddleware : ICommandMiddleware&lt;MyCommand&gt;, ISupportMediatorPriority
/// {
///     // Execute early to validate before other middleware
///     public int MediatorPriority => DefaultMediatorPriorities.High;
///
///     // ...
/// }
/// </code>
/// </example>
/// <seealso cref="ISupportMediatorPriority"/>
[PublicAPI]
public static class DefaultMediatorPriorities
{
    /// <summary>
    /// Gets or sets the default low priority value for mediator components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Components with this priority execute after components with default (0) or high priority.
    /// Use this for components that should run late in the pipeline, such as cleanup handlers
    /// or finalization logic.
    /// </para>
    /// <para>
    /// The default value is <c>-100</c>, leaving room for custom priorities between low and default.
    /// </para>
    /// </remarks>
    /// <value>The low priority value. Default is <c>-100</c>.</value>
    public static int Low { get; set; } = -100;

    /// <summary>
    /// Gets or sets the default high priority value for mediator components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Components with this priority execute before components with default (0) or low priority.
    /// Use this for components that should run early in the pipeline, such as logging middleware,
    /// validation handlers, or authentication checks.
    /// </para>
    /// <para>
    /// The default value is <c>100</c>, leaving room for custom priorities between default and high.
    /// </para>
    /// </remarks>
    /// <value>The high priority value. Default is <c>100</c>.</value>
    public static int High { get; set; } = 100;
}
