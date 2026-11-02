// Copyright 2026 Eleven19
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using Eleven19.IonDotnet.Builders;
using Eleven19.IonDotnet.Internals.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eleven19.IonDotnet.Tests.Internals
{
    [TestClass]
    public class TextEscapeTest
    {
        private const string GrinningFace = "\U0001F600";
        private const string MaxScalar = "\U0010FFFF";

        [TestMethod]
        [DataRow(@"""\U0001F600""", GrinningFace, DisplayName = "double-quoted astral")]
        [DataRow(@"'''\U0001F600'''", GrinningFace, DisplayName = "triple-quoted astral")]
        [DataRow(@"""\U0010FFFF""", MaxScalar, DisplayName = "highest scalar")]
        [DataRow(@"""\U00010000""", "\U00010000", DisplayName = "lowest astral")]
        [DataRow(@"""\U00000041""", "A", DisplayName = "big U below the astral range")]
        [DataRow(@"""A""", "A", DisplayName = "little u")]
        [DataRow(@"""\x41""", "A", DisplayName = "hex")]
        [DataRow(@"""a\U0001F600b""", "a" + GrinningFace + "b", DisplayName = "astral between characters")]
        [DataRow(@"""😀""", GrinningFace, DisplayName = "double-quoted surrogate pair")]
        [DataRow(@"'''😀'''", GrinningFace, DisplayName = "triple-quoted surrogate pair")]
        public void StringEscapes(string text, string expected)
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(Encoding.UTF8.GetBytes(text))))
            {
                Assert.AreEqual(IonType.String, reader.MoveNext());
                Assert.AreEqual(expected, reader.StringValue());
            }
        }

        [TestMethod]
        [DataRow(@"'\U0001F600'", GrinningFace, DisplayName = "astral")]
        [DataRow(@"'😀'", GrinningFace, DisplayName = "surrogate pair")]
        [DataRow(@"'a\U0001F600'", "a" + GrinningFace, DisplayName = "astral after a character")]
        public void SymbolEscapes(string text, string expected)
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(Encoding.UTF8.GetBytes(text))))
            {
                Assert.AreEqual(IonType.Symbol, reader.MoveNext());
                Assert.AreEqual(expected, reader.SymbolValue().Text);
            }
        }

        [TestMethod]
        [DataRow(@"""\U00110000""", DisplayName = "double-quoted above the highest scalar")]
        [DataRow(@"'\U00110000'", DisplayName = "single-quoted above the highest scalar")]
        [DataRow(@"'''\U00110000'''", DisplayName = "triple-quoted above the highest scalar")]
        [DataRow(@"""\UFFFFFFFF""", DisplayName = "all bits set")]
        [DataRow(@"""\u12G4""", DisplayName = "double-quoted little u bad digit")]
        [DataRow(@"'\u12G4'", DisplayName = "single-quoted little u bad digit")]
        [DataRow(@"'''\u12G4'''", DisplayName = "triple-quoted little u bad digit")]
        [DataRow(@"""\x4G""", DisplayName = "hex bad digit")]
        [DataRow(@"""\U0001F60G""", DisplayName = "big U bad digit")]
        [DataRow(@"""\uD83D""", DisplayName = "lone high surrogate")]
        [DataRow(@"""\uDE00""", DisplayName = "lone low surrogate")]
        [DataRow(@"""\uD83D\U0001DE00""", DisplayName = "high surrogate before an astral escape")]
        public void InvalidEscapesThrow(string text)
        {
            try
            {
                Read(text);
            }
            catch (IonException)
            {
                // The scanner throws IonException or a subclass, depending on the problem.
                return;
            }

            Assert.Fail($"Expected an IonException for {text}.");
        }

        [TestMethod]
        public void ClobsRefuseBigU()
        {
            Assert.ThrowsException<InvalidTokenException>(() => Read(@"{{""\U0001F600""}}"));
        }

        private static void Read(string text)
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(Encoding.UTF8.GetBytes(text))))
            {
                var type = reader.MoveNext();
                if (type == IonType.String)
                {
                    reader.StringValue();
                }
                else if (type == IonType.Symbol)
                {
                    reader.SymbolValue();
                }
                else if (type == IonType.Clob)
                {
                    reader.NewByteArray();
                }
            }
        }
    }
}
