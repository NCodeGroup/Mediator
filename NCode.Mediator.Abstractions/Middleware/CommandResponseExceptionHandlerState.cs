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

namespace NCode.Mediator.Middleware;

/// <summary>
/// Represents the state object passed to <see cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/>
/// implementations to indicate whether an exception has been handled and to provide a fallback response.
/// </summary>
/// <remarks>
/// <para>
/// This state object is shared across all exception handlers invoked for a particular exception.
/// When a handler determines that it can recover from the exception, it should call <see cref="SetHandled"/>
/// with an appropriate response value.
/// </para>
/// <para>
/// Once <see cref="SetHandled"/> is called, the <see cref="IsHandled"/> property returns <see langword="true"/>
/// and the <see cref="Response"/> property contains the value to return instead of re-throwing the exception.
/// </para>
/// <para>
/// If no handler calls <see cref="SetHandled"/>, the original exception is re-thrown after all
/// handlers have been invoked.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">The type of the fallback response that can be provided when handling the exception.</typeparam>
/// <seealso cref="ICommandResponseExceptionHandler{TCommand,TException,TResponse}"/>
[PublicAPI]
public class CommandResponseExceptionHandlerState<TResponse>
{
    /// <summary>
    /// Gets a value indicating whether the exception has been handled by calling <see cref="SetHandled"/>.
    /// </summary>
    /// <remarks>
    /// When this property returns <see langword="true"/>, the <see cref="Response"/> property is guaranteed
    /// to be non-null and contains the fallback response to return.
    /// </remarks>
    /// <value>
    /// <see langword="true"/> if the exception has been handled and a response is available;
    /// otherwise, <see langword="false"/>.
    /// </value>
    [MemberNotNullWhen(true, nameof(Response))]
    public bool IsHandled => Response is not null;

    /// <summary>
    /// Gets the fallback response to return when the exception has been handled.
    /// </summary>
    /// <remarks>
    /// This property is <see langword="null"/> until <see cref="SetHandled"/> is called.
    /// After <see cref="SetHandled"/> is called, this property contains the response value
    /// that will be returned to the caller instead of re-throwing the exception.
    /// </remarks>
    /// <value>
    /// The fallback response if <see cref="IsHandled"/> is <see langword="true"/>;
    /// otherwise, <see langword="null"/>.
    /// </value>
    public TResponse? Response { get; private set; }

    /// <summary>
    /// Marks the exception as handled and specifies the fallback response to return.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method to indicate that the exception has been successfully handled and should
    /// not be re-thrown. The specified <paramref name="response"/> will be returned to the caller
    /// as if the command handler had completed successfully.
    /// </para>
    /// <para>
    /// Only the first call to this method has an effect; subsequent calls will overwrite the
    /// previous response value.
    /// </para>
    /// </remarks>
    /// <param name="response">The fallback response to return to the caller.</param>
    public void SetHandled(TResponse response) => Response = response;
}
