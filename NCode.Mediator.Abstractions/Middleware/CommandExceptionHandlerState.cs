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
/// Represents the state object passed to <see cref="ICommandExceptionHandler{TCommand,TException}"/>
/// implementations to indicate whether an exception has been handled and should be suppressed.
/// </summary>
/// <remarks>
/// <para>
/// This state object is shared across all exception handlers invoked for a particular exception.
/// When a handler determines that it can recover from the exception, it should call <see cref="SetHandled"/>
/// to prevent the exception from propagating.
/// </para>
/// <para>
/// Once <see cref="SetHandled"/> is called, the <see cref="IsHandled"/> property returns <see langword="true"/>
/// and the exception will not be re-thrown after all handlers have been invoked.
/// </para>
/// <para>
/// If no handler calls <see cref="SetHandled"/>, the original exception is re-thrown after all
/// handlers have been invoked.
/// </para>
/// <para>
/// For command-response pipelines where a fallback response is needed, use
/// <see cref="CommandResponseExceptionHandlerState{TResponse}"/> instead.
/// </para>
/// </remarks>
/// <seealso cref="ICommandExceptionHandler{TCommand,TException}"/>
/// <seealso cref="CommandResponseExceptionHandlerState{TResponse}"/>
[PublicAPI]
public class CommandExceptionHandlerState
{
    /// <summary>
    /// Gets a value indicating whether the exception has been handled by calling <see cref="SetHandled"/>.
    /// </summary>
    /// <remarks>
    /// When this property returns <see langword="true"/>, the exception will be suppressed and not
    /// re-thrown after all handlers have completed.
    /// </remarks>
    /// <value>
    /// <see langword="true"/> if the exception has been handled and should be suppressed;
    /// otherwise, <see langword="false"/>.
    /// </value>
    public bool IsHandled { get; private set; }

    /// <summary>
    /// Marks the exception as handled so that it will not be re-thrown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method to indicate that the exception has been successfully handled and should
    /// not be re-thrown. The command pipeline will complete normally as if no exception occurred.
    /// </para>
    /// <para>
    /// This method is idempotent; calling it multiple times has no additional effect.
    /// </para>
    /// </remarks>
    public void SetHandled() => IsHandled = true;
}
