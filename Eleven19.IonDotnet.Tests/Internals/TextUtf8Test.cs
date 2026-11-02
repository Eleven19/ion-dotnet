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
    public class TextUtf8Test
    {
        public static IEnumerable<object[]> InvalidSequences()
        {
            yield return new object[] { new byte[] { 0xFF } };
            yield return new object[] { new byte[] { 0x80 } };
            yield return new object[] { new byte[] { 0xC0, 0xAF } };
            yield return new object[] { new byte[] { 0xE0, 0x80, 0xAF } };
            yield return new object[] { new byte[] { 0xED, 0xA0, 0x80 } };
            yield return new object[] { new byte[] { 0xF4, 0x90, 0x80, 0x80 } };
            yield return new object[] { new byte[] { 0xC2 } };
            yield return new object[] { new byte[] { 0xF0, 0x9F, 0x98 } };
        }

        [TestMethod]
        [DynamicData(nameof(InvalidSequences), DynamicDataSourceType.Method)]
        public void InvalidBytesInAStringThrow(byte[] invalid)
        {
            var data = new[] { (byte)'"' }.Concat(invalid).Concat(new[] { (byte)'"' }).ToArray();
            Assert.ThrowsException<IonException>(() => ReadStringFrom(data));
        }

        [TestMethod]
        [DynamicData(nameof(InvalidSequences), DynamicDataSourceType.Method)]
        public void InvalidBytesPastTheFirstBufferThrow(byte[] invalid)
        {
            var padding = Enumerable.Repeat((byte)'a', 8192).ToArray();
            var data = new[] { (byte)'"' }.Concat(padding).Concat(invalid).Concat(new[] { (byte)'"' }).ToArray();
            Assert.ThrowsException<IonException>(() => ReadStringFrom(data));
        }

        [TestMethod]
        [DataRow("café")]
        [DataRow("中文")]
        [DataRow("\U0001F600")]
        [DataRow("�")]
        public void ValidUnicodeInAStreamReads(string text)
        {
            Assert.AreEqual(text, ReadStringFrom(Encoding.UTF8.GetBytes($"\"{text}\"")));
        }

        [TestMethod]
        public void ValidUnicodeFollowedByMoreValuesReads()
        {
            var data = Encoding.UTF8.GetBytes("{ doc: \"café\", 'naïve': 1 } next");
            using (var reader = IonReaderBuilder.Build(new MemoryStream(data)))
            {
                Assert.AreEqual(IonType.Struct, reader.MoveNext());
                reader.StepIn();
                Assert.AreEqual(IonType.String, reader.MoveNext());
                Assert.AreEqual("café", reader.StringValue());
                Assert.AreEqual(IonType.Int, reader.MoveNext());
                Assert.AreEqual("naïve", reader.CurrentFieldName);
                reader.StepOut();
                Assert.AreEqual(IonType.Symbol, reader.MoveNext());
                Assert.AreEqual("next", reader.SymbolValue().Text);
                Assert.AreEqual(IonType.None, reader.MoveNext());
            }
        }

        [TestMethod]
        public void AByteOrderMarkIsStillAccepted()
        {
            var data = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("\"a\"")).ToArray();
            Assert.AreEqual("a", ReadStringFrom(data));
        }

        [TestMethod]
        public void AnExplicitEncodingIsStillHonored()
        {
            var options = new ReaderOptions { Encoding = Encoding.Unicode };
            var data = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("\"café\"")).ToArray();
            using (var reader = IonReaderBuilder.Build(new MemoryStream(data), options))
            {
                Assert.AreEqual(IonType.String, reader.MoveNext());
                Assert.AreEqual("café", reader.StringValue());
            }
        }

        private static string ReadStringFrom(byte[] data)
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(data)))
            {
                Assert.AreEqual(IonType.String, reader.MoveNext());
                return reader.StringValue();
            }
        }
    }
}
