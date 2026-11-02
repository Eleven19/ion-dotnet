// Copyright 2026 Eleven19
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Eleven19.IonDotnet.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eleven19.IonDotnet.Tests.Internals
{
    [TestClass]
    public class BinaryUtf8Test
    {
        public static IEnumerable<object[]> InvalidStrings()
        {
            var invalidSequences = new[]
            {
                new byte[] { 0xFF },
                new byte[] { 0x80 },
                new byte[] { 0xC0, 0xAF },
                new byte[] { 0xE0, 0x80, 0xAF },
                new byte[] { 0xED, 0xA0, 0x80 },
                new byte[] { 0xF4, 0x90, 0x80, 0x80 },
                new byte[] { 0xC2 },
                new byte[] { 0xE2, 0x82 },
                new byte[] { 0xF0, 0x9F, 0x98 },
                new byte[] { 0xE2, 0x28, 0xA1 },
            };

            foreach (var prefixLength in new[] { 0, 127, 128, 1024 })
            {
                foreach (var invalid in invalidSequences)
                {
                    var bytes = Enumerable.Repeat((byte)'a', prefixLength).Concat(invalid).ToArray();
                    yield return new object[] { EncodeString(bytes) };
                }
            }
        }

        [TestMethod]
        [DynamicData(nameof(InvalidStrings), DynamicDataSourceType.Method)]
        public void InvalidStringThrowsIonException(byte[] data)
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(data)))
            {
                Assert.AreEqual(IonType.String, reader.MoveNext());
                var error = Assert.ThrowsException<IonException>(() => reader.StringValue());
                Assert.IsInstanceOfType(error.InnerException, typeof(DecoderFallbackException));
            }
        }

        [TestMethod]
        [DynamicData(nameof(InvalidStrings), DynamicDataSourceType.Method)]
        public void LoaderRejectsInvalidString(byte[] data)
        {
            Assert.ThrowsException<IonException>(() => IonLoader.Default.Load(data));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(128)]
        public void ValidUnicodeAndReplacementCharacterArePreserved(int prefixLength)
        {
            var text = new string('a', prefixLength) + "\u0000\u00E9\u4E2D\U0001F600\uFFFD";
            using (var reader = IonReaderBuilder.Build(new MemoryStream(EncodeString(Encoding.UTF8.GetBytes(text)))))
            {
                Assert.AreEqual(IonType.String, reader.MoveNext());
                Assert.AreEqual(text, reader.StringValue());
                Assert.AreEqual(IonType.None, reader.MoveNext());
            }
        }

        private static byte[] EncodeString(byte[] payload)
        {
            var data = new List<byte> { 0xE0, 0x01, 0x00, 0xEA };
            if (payload.Length < 14)
            {
                data.Add((byte)(0x80 | payload.Length));
            }
            else
            {
                data.Add(0x8E);
                var length = new List<byte> { (byte)((payload.Length & 0x7F) | 0x80) };
                for (var remaining = payload.Length >> 7; remaining > 0; remaining >>= 7)
                {
                    length.Insert(0, (byte)(remaining & 0x7F));
                }

                data.AddRange(length);
            }

            data.AddRange(payload);
            return data.ToArray();
        }
    }
}
