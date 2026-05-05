//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenTap.Metrics;

static class ReflectionHelper
{
    /// <summary>
    /// Returns true if a type is numeric.
    /// </summary>
    public static bool IsNumeric(this Type t)
    {
        if (t.IsEnum)
            return false;
        switch (Type.GetTypeCode(t))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }

    public static TypeData AsTypeData(this ITypeData type)
    {
        do
        {
            if (type is TypeData td)
                return td;
            type = type?.BaseType;
        } while (type != null);

        return null;
    }

    /// <summary> Returns true if a type is numeric. </summary>
    public static bool IsNumeric(this ITypeData t)
    {
        return t.AsTypeData()?.Type.IsNumeric() == true;
    }

    internal static Task<T> StartAwaitableTapThread<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>();
        TapThread.Start(() =>
        {
            try
            {
                result.SetResult(action());
            }
            catch (Exception inner)
            {
                result.SetException(inner);
            }
        });
        return result.Task;
    }

    struct OnceLogToken
    {
        public object Token;
        public TraceSource Log;
    }

    static HashSet<OnceLogToken> logOnceTokens = new HashSet<OnceLogToken>();

    /// <summary>
    /// Avoids spamming the log with errors that 
    /// should only be shown once by memorizing token and TraceSource. 
    /// </summary>
    /// <returns>True if an error was logged.</returns>
    public static bool ErrorOnce(this TraceSource log, object token, string message, params object[] args)
    {
        lock (logOnceTokens)
        {
            var logtoken = new OnceLogToken { Token = token, Log = log };
            if (!logOnceTokens.Contains(logtoken))
            {
                log.Error(message, args);
                logOnceTokens.Add(logtoken);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Creates a HashSet from an IEnumerable.
    /// </summary>
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
    {
        return new HashSet<T>(source);
    }
}
