﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace RamType0.JsonRpc
{
    public static class BinaryHash
    {
        /// <summary>
        /// 中程度の品質のバイナリ列のハッシュコードを高速に生成します。
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public static int GetSequenceHashCode(this ReadOnlySpan<byte> span)
        {
            var length = span.Length;
            switch (length)
            {
                case 0:
                    return 0;

                case 1:
                    return span[0].GetHashCode();
                case 2:
                    return GetElementUnsafeAs<ushort>(span).GetHashCode();
                case 3:
                    return (GetElementUnsafeAs<ushort>(span) | span[2] << 16).GetHashCode();
                case 4:
                    return GetElementUnsafeAs<int>(span).GetHashCode();
                case 5:
                case 6:
                case 7:
                    return (GetElementUnsafeAs<int>(span) ^ GetElementUnsafeAs<int>(span, length - 4)).GetHashCode();
                case 8:
                    return GetElementUnsafeAs<ulong>(span).GetHashCode();
                default:
                    var hash = (uint)((GetElementUnsafeAs<ulong>(span) ^ GetElementUnsafeAs<ulong>(span, length - 8)).GetHashCode());
                    var shifts = length & 31;
                    return (int)((hash << (32 - shifts)) | (hash >> shifts));//循環右シフト。一旦uintにしないと算術シフトのせいで1まみれになるので注意。
            }
        }
        /// <summary>
        /// 中程度の品質のバイナリ列のハッシュコードを高速に生成します。
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public static int GetSequenceHashCode(this Span<byte> span) => GetSequenceHashCode((ReadOnlySpan<byte>)span); 
        private static T GetElementUnsafeAs<T>(ReadOnlySpan<byte> span, int index = 0)
            where T : unmanaged
        {
            return Unsafe.As<byte, T>(ref Unsafe.AsRef(span[index]));
        }
    }
}