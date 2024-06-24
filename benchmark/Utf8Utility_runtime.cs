// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace DotnetRuntime;

public static unsafe class Utf8Utility
{
    private delegate byte* GetPointerToFirstInvalidByteDelegate(byte* pInputBuffer, int inputLength, out int utf16CodeUnitCountAdjustment, out int scalarCountAdjustment);

    private static readonly GetPointerToFirstInvalidByteDelegate RuntimeMethod = PrepareRuntimeMethod();

    private static GetPointerToFirstInvalidByteDelegate PrepareRuntimeMethod()
    {
        MethodInfo methodInfo = typeof(object).Assembly.GetType("System.Text.Unicode.Utf8Utility")!
            .GetMethod("GetPointerToFirstInvalidByte", BindingFlags.Static | BindingFlags.Public)!;
        return (GetPointerToFirstInvalidByteDelegate)Delegate.CreateDelegate(typeof(GetPointerToFirstInvalidByteDelegate), methodInfo);
    }

    public static byte* GetPointerToFirstInvalidByte(byte* pInputBuffer, int inputLength, out int utf16CodeUnitCountAdjustment, out int scalarCountAdjustment) =>
        RuntimeMethod(pInputBuffer, inputLength, out utf16CodeUnitCountAdjustment, out scalarCountAdjustment);
}