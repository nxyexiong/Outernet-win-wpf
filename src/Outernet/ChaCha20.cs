using System;

namespace Outernet
{
    // https://github.com/jedisct1/libsodium/blob/master/src/libsodium/crypto_stream/chacha20/ref/chacha20_ref.c
    public class ChaCha20
    {
        private readonly uint[] _input;

        public ChaCha20()
        {
            _input = new uint[16];
        }

        public static void StreamRefXorIc(byte[] c, byte[] m, uint mlen, byte[] n, ulong ic, byte[] k)
        {
            if (mlen == 0) return;
            var chacha20 = new ChaCha20();
            var icBytes = new byte[8];
            var icHigh = (uint)(ic >> 32);
            var icLow = (uint)ic;
            Store32Le(icBytes, 0, icLow);
            Store32Le(icBytes, 4, icHigh);
            chacha20.KeySetup(k);
            chacha20.IvSetup(n, icBytes);
            chacha20.EncryptBytes(m, c, mlen);
        }

        private void KeySetup(byte[] k)
        {
            _input[0] = 0x61707865u;
            _input[1] = 0x3320646eu;
            _input[2] = 0x79622d32u;
            _input[3] = 0x6b206574u;
            _input[4] = Load32Le(k, 0);
            _input[5] = Load32Le(k, 4);
            _input[6] = Load32Le(k, 8);
            _input[7] = Load32Le(k, 12);
            _input[8] = Load32Le(k, 16);
            _input[9] = Load32Le(k, 20);
            _input[10] = Load32Le(k, 24);
            _input[11] = Load32Le(k, 28);
        }

        private void IvSetup(byte[] iv, byte[] counter)
        {
            _input[12] = counter == null ? 0 : Load32Le(counter, 0);
            _input[13] = counter == null ? 0 : Load32Le(counter, 4);
            _input[14] = Load32Le(iv, 0);
            _input[15] = Load32Le(iv, 4);
        }

        private void EncryptBytes(byte[] mm, byte[] cc, uint bytes)
        {
            uint x0, x1, x2, x3, x4, x5, x6, x7, x8, x9, x10, x11, x12, x13, x14, x15;
            uint j0, j1, j2, j3, j4, j5, j6, j7, j8, j9, j10, j11, j12, j13, j14, j15;
            byte[] tmpp = new byte[64];
            uint i;

            unsafe
            {
                fixed (byte* mfix = mm)
                {
                    fixed (byte* cfix = cc)
                    {
                        fixed (byte* tmpfix = tmpp)
                        {
                            byte* m = mfix;
                            byte* c = cfix;
                            byte* tmp = tmpfix;
                            byte* ctarget = null;

                            if (bytes <= 0) return;

                            j0 = _input[0];
                            j1 = _input[1];
                            j2 = _input[2];
                            j3 = _input[3];
                            j4 = _input[4];
                            j5 = _input[5];
                            j6 = _input[6];
                            j7 = _input[7];
                            j8 = _input[8];
                            j9 = _input[9];
                            j10 = _input[10];
                            j11 = _input[11];
                            j12 = _input[12];
                            j13 = _input[13];
                            j14 = _input[14];
                            j15 = _input[15];

                            while (true)
                            {
                                if (bytes < 64)
                                {
                                    for (i = 0; i < 64; i++) tmp[i] = 0;
                                    for (i = 0; i < bytes; i++)
                                        tmp[i] = m[i];
                                    m = tmp;
                                    ctarget = c;
                                    c = tmp;
                                }
                                x0 = j0;
                                x1 = j1;
                                x2 = j2;
                                x3 = j3;
                                x4 = j4;
                                x5 = j5;
                                x6 = j6;
                                x7 = j7;
                                x8 = j8;
                                x9 = j9;
                                x10 = j10;
                                x11 = j11;
                                x12 = j12;
                                x13 = j13;
                                x14 = j14;
                                x15 = j15;
                                for (i = 20; i > 0; i -= 2)
                                {
                                    QuarterRound(ref x0, ref x4, ref x8, ref x12);
                                    QuarterRound(ref x1, ref x5, ref x9, ref x13);
                                    QuarterRound(ref x2, ref x6, ref x10, ref x14);
                                    QuarterRound(ref x3, ref x7, ref x11, ref x15);
                                    QuarterRound(ref x0, ref x5, ref x10, ref x15);
                                    QuarterRound(ref x1, ref x6, ref x11, ref x12);
                                    QuarterRound(ref x2, ref x7, ref x8, ref x13);
                                    QuarterRound(ref x3, ref x4, ref x9, ref x14);
                                }
                                x0 = WrapAroundAdd(x0, j0);
                                x1 = WrapAroundAdd(x1, j1);
                                x2 = WrapAroundAdd(x2, j2);
                                x3 = WrapAroundAdd(x3, j3);
                                x4 = WrapAroundAdd(x4, j4);
                                x5 = WrapAroundAdd(x5, j5);
                                x6 = WrapAroundAdd(x6, j6);
                                x7 = WrapAroundAdd(x7, j7);
                                x8 = WrapAroundAdd(x8, j8);
                                x9 = WrapAroundAdd(x9, j9);
                                x10 = WrapAroundAdd(x10, j10);
                                x11 = WrapAroundAdd(x11, j11);
                                x12 = WrapAroundAdd(x12, j12);
                                x13 = WrapAroundAdd(x13, j13);
                                x14 = WrapAroundAdd(x14, j14);
                                x15 = WrapAroundAdd(x15, j15);
                                x0 ^= Load32LeUnsafe(m, 0);
                                x1 ^= Load32LeUnsafe(m, 4);
                                x2 ^= Load32LeUnsafe(m, 8);
                                x3 ^= Load32LeUnsafe(m, 12);
                                x4 ^= Load32LeUnsafe(m, 16);
                                x5 ^= Load32LeUnsafe(m, 20);
                                x6 ^= Load32LeUnsafe(m, 24);
                                x7 ^= Load32LeUnsafe(m, 28);
                                x8 ^= Load32LeUnsafe(m, 32);
                                x9 ^= Load32LeUnsafe(m, 36);
                                x10 ^= Load32LeUnsafe(m, 40);
                                x11 ^= Load32LeUnsafe(m, 44);
                                x12 ^= Load32LeUnsafe(m, 48);
                                x13 ^= Load32LeUnsafe(m, 52);
                                x14 ^= Load32LeUnsafe(m, 56);
                                x15 ^= Load32LeUnsafe(m, 60);
                                j12 = WrapAroundAdd(j12, 1);
                                if (j12 == 0) j13 = WrapAroundAdd(j13, 1);
                                Store32LeUnsafe(c, 0, x0);
                                Store32LeUnsafe(c, 4, x1);
                                Store32LeUnsafe(c, 8, x2);
                                Store32LeUnsafe(c, 12, x3);
                                Store32LeUnsafe(c, 16, x4);
                                Store32LeUnsafe(c, 20, x5);
                                Store32LeUnsafe(c, 24, x6);
                                Store32LeUnsafe(c, 28, x7);
                                Store32LeUnsafe(c, 32, x8);
                                Store32LeUnsafe(c, 36, x9);
                                Store32LeUnsafe(c, 40, x10);
                                Store32LeUnsafe(c, 44, x11);
                                Store32LeUnsafe(c, 48, x12);
                                Store32LeUnsafe(c, 52, x13);
                                Store32LeUnsafe(c, 56, x14);
                                Store32LeUnsafe(c, 60, x15);
                                if (bytes <= 64)
                                {
                                    if (bytes < 64)
                                    {
                                        for (i = 0; i < bytes; i++)
                                            ctarget[i] = c[i];
                                    }
                                    _input[12] = j12;
                                    _input[13] = j13;
                                    return;
                                }
                                bytes -= 64;
                                c += 64;
                                m += 64;
                            }
                        }
                    }
                }
            }
        }

        private static uint WrapAroundAdd(uint a, uint b)
        {
            ulong rst = a + b;
            return (uint)(rst % 0x100000000ul);
        }

        private static uint Rotate(uint x, int b)
        {
            return (x << b) | (x >> (32 - b));
        }

        private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a = WrapAroundAdd(a, b);
            d = Rotate(d ^ a, 16);
            c = WrapAroundAdd(c, d);
            b = Rotate(b ^ c, 12);
            a = WrapAroundAdd(a, b);
            d = Rotate(d ^ a, 8);
            c = WrapAroundAdd(c, d);
            b = Rotate(b ^ c, 7);
        }

        private static uint Load32Le(byte[] src, int offset)
        {
            unsafe
            {
                fixed (byte* srcPtr = src)
                {
                    return Load32LeUnsafe(srcPtr, offset);
                }
            }
        }

        private unsafe static uint Load32LeUnsafe(byte* src, int offset)
        {
            uint ret = src[offset + 0];
            ret |= (uint)src[offset + 1] << 8;
            ret |= (uint)src[offset + 2] << 16;
            ret |= (uint)src[offset + 3] << 24;
            return ret;
        }

        private static void Store32Le(byte[] dst, int offset, uint x)
        {
            unsafe
            {
                fixed (byte* dstPtr = dst)
                {
                    Store32LeUnsafe(dstPtr, offset, x);
                }
            }
        }

        private unsafe static void Store32LeUnsafe(byte* dst, int offset, uint x)
        {
            dst[offset + 0] = (byte)x;
            x >>= 8;
            dst[offset + 1] = (byte)x;
            x >>= 8;
            dst[offset + 2] = (byte)x;
            x >>= 8;
            dst[offset + 3] = (byte)x;
        }
    }
}
