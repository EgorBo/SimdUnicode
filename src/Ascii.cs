using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// Ideally, we would want to implement something that looks like
// https://learn.microsoft.com/en-us/dotnet/api/system.text.asciiencoding?view=net-7.0
//
// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Text/Ascii.cs
//
// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Text/Ascii.Transcoding.cs
namespace SimdUnicode
{
    public unsafe static class Ascii
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAscii(this char c) => c < 128;

        public static bool IsAscii(this string s)
        {
            foreach (var c in s)
            {
                if (!c.IsAscii()) return false;
            }
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAscii(this ReadOnlySpan<char> s)
        {
            foreach (var c in s)
            {
                if (!c.IsAscii()) return false;
            }
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool SIMDIsAscii(this ReadOnlySpan<char> s)
        {
            if (s.IsEmpty) return true;

            if (ArmBase.Arm64.IsSupported)
            {

                // We are going to OR together all the results and then use
                // the maximum value to determine if any of the characters
                // exceeds the ASCII range. See 
                // https://github.com/simdutf/simdutf/blob/master/src/arm64/implementation.cpp

                // There is not a lot of documentation, but we can read the code at
                // https://github.com/dotnet/runtime/tree/main/src/libraries/System.Private.CoreLib/src/System/Runtime/Intrinsics/Arm
                // and see examples at
                // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Encodings.Web/src/System/Text/Encodings/Web/OptimizedInboxTextEncoder.AdvSimd64.cs

                // Go through https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.arm.advsimd.arm64.maxacross?view=net-8.0
                fixed (char* pStart = &MemoryMarshal.GetReference(s))
                {
                    ushort max_so_far = 0;
                    int i = 0;
                    if (s.Length > 8)
                    {
                        // instead of a load, we could have set it to zero, like so...
                        // total = Vector128<ushort>.Zero;
                        // or to a custome value like this:
                        // total = DuplicateToVector128((char)0);
                        Vector128<ushort> total = AdvSimd.LoadVector128((ushort*)pStart);
                        i += 8;
                        // unrolling could be useful here:
                        for (; i + 7 < s.Length; i += 8)
                        {
                            Vector128<ushort> raw = AdvSimd.LoadVector128((ushort*)pStart + i);
                            total = AdvSimd.Or(total, raw);
                        }

                        max_so_far =
                        AdvSimd.Arm64.MaxAcross(total).ToScalar();
                    }
                    for (; i < s.Length; i++)
                    {
                        if (pStart[i] > max_so_far) { max_so_far = pStart[i]; }
                    }
                    return max_so_far < 128;
                }
            }
            else if (Sse41.IsSupported)
            {
                // Go through https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.x86.sse2.comparelessthan?view=net-8.0
                fixed (char* pStart = &MemoryMarshal.GetReference(s))
                {
                    int i = 0;
                    if (s.Length > 8)
                    {
                        Vector128<ushort> total = Sse41.LoadDquVector128((ushort*)pStart);
                        i += 8;
                        // unrolling could be useful here:
                        for (; i + 7 < s.Length; i += 8)
                        {
                            Vector128<ushort> raw = Sse41.LoadDquVector128((ushort*)pStart + i);
                            total = Sse2.Or(total, raw);
                        }
                        Vector128<ushort> b127 = Vector128.Create((ushort)127);
                        Vector128<ushort> b = Sse41.Max(b127, total);
                        Vector128<ushort> b16 = Sse41.CompareEqual(b, b127);
                        int movemask = Sse2.MoveMask(b16.AsByte());
                        if (movemask != 0xfffff)
                        {
                            return false;
                        }
                    }
                    for (; i < s.Length; i++)
                    {
                        if (pStart[i] >= 128) return false;
                    }
                    return true;
                }
            }
            // Fallback code

            foreach (var c in s)
            {
                if (!c.IsAscii()) return false;
            }
            return true;
        }
    }
}
// Further reading:
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Encodings.Web/src/System/Text/Unicode/UnicodeHelpers.cs
