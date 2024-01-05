using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Outernet
{
    public class Crypto
    {
        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;
        private readonly byte[] _key;

        public Crypto(string secret)
        {
            try
            {
                _key = Utils.Sha256(Encoding.ASCII.GetBytes(secret));
            }
            catch (Exception ex)
            {
                _logger?.Error($"Crypto.Crypto, error: {ex}");
            }
        }

        // return null if failed
        public OtBuffer Encrypt(OtBuffer input)
        {
            try
            {
                var output = new OtBuffer();
                var nonce = GenerateNonce();
                output.InsertBack(nonce, 8);
                var inputBytes = new byte[input.GetLen()];
                Array.Copy(input.GetBuf(), 0, inputBytes, 0, input.GetLen());
                var outputBytes = new byte[input.GetLen()];

                // chacha20
                ChaCha20.StreamRefXorIc(outputBytes, inputBytes, (uint)input.GetLen(), nonce, 0, _key);

                output.InsertBack(outputBytes, input.GetLen());
                return output;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Crypto.Encrypt, error: {ex}");
                return null;
            }
        }

        // return null if failed
        public OtBuffer Decrypt(OtBuffer input)
        {
            try
            {
                if (input.GetLen() < 8) return null;
                var output = new OtBuffer();
                var nonce = new byte[8];
                Array.Copy(input.GetBuf(), 0, nonce, 0, 8);
                var inputBytes = new byte[input.GetLen() - 8];
                Array.Copy(input.GetBuf(), 8, inputBytes, 0, input.GetLen() - 8);
                var outputBytes = new byte[input.GetLen() - 8];

                // chacha20
                ChaCha20.StreamRefXorIc(outputBytes, inputBytes, (uint)input.GetLen() - 8, nonce, 0, _key);

                output.InsertBack(outputBytes, input.GetLen() - 8);
                return output;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Crypto.Decrypt, error: {ex}");
                return null;
            }
        }

        private static byte[] GenerateNonce()
        {
            byte[] randomBytes = new byte[8];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}
