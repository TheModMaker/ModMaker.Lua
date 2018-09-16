// Copyright 2012 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Extensions
{
    /// <summary>
    /// An extension class that defines Action&lt;...&gt; and Func&lt;...&gt; extensions
    /// to Lua so you don'type have to cast to Delegate to pass.
    /// </summary>
    public static class LuaExt
    {
        #region public static void Register(Action<...> func)
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register(this Lua lua, Action func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T>(this Lua lua, Action<T> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2>(this Lua lua, Action<T, T2> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3>(this Lua lua, Action<T, T2, T3> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4>(this Lua lua, Action<T, T2, T3, T4> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5>(this Lua lua, Action<T, T2, T3, T4, T5> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6>(this Lua lua, Action<T, T2, T3, T4, T5, T6> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> func)
        {
            lua.Register((Delegate)func);
        }
        #endregion

        #region public static void Register(Func<...> func)
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<TResult>(this Lua lua, Func<TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, TResult>(this Lua lua, Func<T, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, TResult>(this Lua lua, Func<T, T2, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, TResult>(this Lua lua, Func<T, T2, T3, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, TResult>(this Lua lua, Func<T, T2, T3, T4, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> func)
        {
            lua.Register((Delegate)func);
        }
        #endregion

        #region public static void Register(Action<...> func, string name)
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register(this Lua lua, Action func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T>(this Lua lua, Action<T> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2>(this Lua lua, Action<T, T2> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3>(this Lua lua, Action<T, T2, T3> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4>(this Lua lua, Action<T, T2, T3, T4> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5>(this Lua lua, Action<T, T2, T3, T4, T5> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6>(this Lua lua, Action<T, T2, T3, T4, T5, T6> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(this Lua lua, Action<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        #endregion

        #region public static void Register(Func<...> func, string name)
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<TResult>(this Lua lua, Func<TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, TResult>(this Lua lua, Func<T, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, TResult>(this Lua lua, Func<T, T2, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, TResult>(this Lua lua, Func<T, T2, T3, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, TResult>(this Lua lua, Func<T, T2, T3, T4, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="lua">The Lua object to register to.</param>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <param name="name">The name of the delegate.  If null, will use the function name.</param>
        /// <exception cref="System.ArgumentException">When there is another method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func has more than one item in the InvokationList.</exception>
        public static void Register<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(this Lua lua, Func<T, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> func, string name)
        {
            lua.Register((Delegate)func, name);
        }
        #endregion
    }
}
