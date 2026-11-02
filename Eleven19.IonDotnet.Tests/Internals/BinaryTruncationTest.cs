// Copyright 2026 Eleven19
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eleven19.IonDotnet.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eleven19.IonDotnet.Tests.Internals
{
    [TestClass]
    public class BinaryTruncationTest
    {
        private static readonly byte[] VersionMarker = { 0xE0, 0x01, 0x00, 0xEA };

        public static IEnumerable<object[]> DamagedValues()
        {
            yield return Case("string claims 5 bytes and has 2", 0x85, 0x61, 0x62);
            yield return Case("int claims 2 bytes and has 1", 0x22, 0x01);
            yield return Case("float claims 8 bytes and has 3", 0x48, 0x01, 0x02, 0x03);
            yield return Case("decimal claims 3 bytes and has 1", 0x53, 0x80);
            yield return Case("timestamp claims 3 bytes and has 1", 0x63, 0x80);
            yield return Case("symbol claims 2 bytes and has 1", 0x72, 0x01);
            yield return Case("clob claims 4 bytes and has 1", 0x94, 0x01);
            yield return Case("blob claims 4 bytes and has 1", 0xA4, 0x01);
            yield return Case("list claims 3 bytes and has 1", 0xB3, 0x21);
            yield return Case("list claims 3 bytes and has 2", 0xB3, 0x21, 0x01);
            yield return Case("sexp claims 3 bytes and has 1", 0xC3, 0x21);
            yield return Case("struct claims 4 bytes and has 2", 0xD4, 0x84, 0x21);
            yield return Case("struct ends after a field name", 0xD4, 0x84);
            yield return Case("annotation wrapper is cut", 0xE5, 0x82);
            yield return Case("annotated value is cut", 0xE4, 0x81, 0x84, 0x22, 0x01);
            yield return Case("varuint length is cut", 0x8E, 0x01);
            yield return Case("nop pad claims 4 bytes and has 1", 0x04, 0x01);
            yield return Case("third value is cut", 0x21, 0x01, 0x21, 0x02, 0x85, 0x61);
            yield return Case("string is longer than its list", 0xB2, 0x85, 0x61, 0x62, 0x63, 0x64, 0x65);
            yield return Case("list is longer than its list", 0xB2, 0xB4, 0x21, 0x01, 0x21, 0x02);
            yield return Case("field value is longer than its struct", 0xD2, 0x84, 0x83, 0x61, 0x62, 0x63);
        }

        public static string DamagedValueName(System.Reflection.MethodInfo method, object[] data) => (string)data[0];

        [TestMethod]
        [DynamicData(nameof(DamagedValues), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(DamagedValueName))]
        public void WalkingEveryValueThrows(string name, byte[] data)
        {
            Assert.ThrowsException<UnexpectedEofException>(() => Walk(data, readScalars: true), name);
        }

        [TestMethod]
        [DynamicData(nameof(DamagedValues), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(DamagedValueName))]
        public void SkippingScalarsThrows(string name, byte[] data)
        {
            Assert.ThrowsException<UnexpectedEofException>(() => Walk(data, readScalars: false), name);
        }

        [TestMethod]
        [DynamicData(nameof(DamagedValues), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(DamagedValueName))]
        public void LoaderThrows(string name, byte[] data)
        {
            Assert.ThrowsException<UnexpectedEofException>(() => IonLoader.Default.Load(data), name);
        }

        [TestMethod]
        public void SteppingOutOfAContainerWithMissingBytesThrows()
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(Bytes(0xB3, 0x21))))
            {
                Assert.AreEqual(IonType.List, reader.MoveNext());
                reader.StepIn();
                Assert.ThrowsException<UnexpectedEofException>(() => reader.StepOut());
            }
        }

        [TestMethod]
        public void EveryCutOfADocumentInsideAValueThrows()
        {
            var document = Document();

            // A cut exactly between top-level values leaves a shorter valid document.
            var boundaries = TopLevelBoundaries(document);

            for (var length = VersionMarker.Length; length < document.Length; length++)
            {
                var cut = document.Take(length).ToArray();

                if (boundaries.Contains(length))
                {
                    Walk(cut, readScalars: true);
                    Walk(cut, readScalars: false);
                }
                else
                {
                    Assert.ThrowsException<UnexpectedEofException>(() => Walk(cut, readScalars: true), $"{length} bytes, reading");
                    Assert.ThrowsException<UnexpectedEofException>(() => Walk(cut, readScalars: false), $"{length} bytes, skipping");
                }
            }
        }

        [TestMethod]
        public void ACompleteDocumentStillReads()
        {
            Assert.AreEqual(10, Walk(Document(), readScalars: true));
            Assert.AreEqual(10, Walk(Document(), readScalars: false));
        }

        [TestMethod]
        public void AnEmptyContainerAtTheEndOfTheInputReads()
        {
            Assert.AreEqual(2, Walk(Bytes(0x21, 0x01, 0xB0), readScalars: true));
        }

        [TestMethod]
        public void GetBytesStopsAtTheEndOfTheLob()
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(Bytes(0xA2, 0x01, 0x02, 0x21, 0x07))))
            {
                Assert.AreEqual(IonType.Blob, reader.MoveNext());
                var buffer = new byte[16];
                Assert.AreEqual(2, reader.GetBytes(buffer));
                CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer.Take(2).ToArray());
                Assert.AreEqual(IonType.Int, reader.MoveNext());
                Assert.AreEqual(7, reader.IntValue());
            }
        }

        [TestMethod]
        public void BinaryIsDetectedFromAStreamThatReturnsShortReads()
        {
            using (var reader = IonReaderBuilder.Build(new OneByteStream(Bytes(0x21, 0x07))))
            {
                Assert.AreEqual(IonType.Int, reader.MoveNext());
                Assert.AreEqual(7, reader.IntValue());
                Assert.AreEqual(IonType.None, reader.MoveNext());
            }
        }

        [TestMethod]
        public void ALobIsReadInFullFromAStreamThatReturnsShortReads()
        {
            var data = Bytes(0xA4, 0x01, 0x02, 0x03, 0x04);
            using (var reader = IonReaderBuilder.Build(new OneByteStream(data)))
            {
                Assert.AreEqual(IonType.Blob, reader.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, reader.NewByteArray());
            }
        }

        private static object[] Case(string name, params byte[] body) => new object[] { name, Bytes(body) };

        private static byte[] Bytes(params byte[] body) => VersionMarker.Concat(body).ToArray();

        /// <summary>
        /// Holds one of each kind of value: top-level values at known offsets, nesting, annotations and a local symbol table.
        /// </summary>
        private static byte[] Document()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = IonBinaryWriterBuilder.Build(stream))
                {
                    writer.WriteInt(1);
                    writer.WriteString("a longer string value");
                    writer.SetTypeAnnotations(new[] { "first", "second" });
                    writer.StepIn(IonType.Struct);
                    writer.SetFieldName("name");
                    writer.WriteString("value");
                    writer.SetFieldName("items");
                    writer.StepIn(IonType.List);
                    writer.WriteDecimal(new BigDecimal(new System.Numerics.BigInteger(12345), 2));
                    writer.WriteBlob(new byte[] { 1, 2, 3 });
                    writer.WriteTimestamp(Timestamp.Parse("2026-10-01T01:02:03Z"));
                    writer.WriteFloat(1.5);
                    writer.StepOut();
                    writer.StepOut();
                    writer.WriteSymbol("sym");
                    writer.Finish();
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Finds the offsets where a cut leaves only whole top-level values. Counted from the framing rather than the
        /// reader, so a reader bug cannot hide here.
        /// </summary>
        private static HashSet<int> TopLevelBoundaries(byte[] data)
        {
            var boundaries = new HashSet<int>();
            var index = VersionMarker.Length;
            boundaries.Add(index);

            while (index < data.Length)
            {
                var descriptor = data[index++];
                var type = descriptor >> 4;
                var length = descriptor & 0x0F;

                if (length == 14 || (type == 13 && length == 1))
                {
                    length = 0;
                    byte next;
                    do
                    {
                        next = data[index++];
                        length = (length << 7) | (next & 0x7F);
                    }
                    while ((next & 0x80) == 0);
                }
                else if (length == 15)
                {
                    length = 0;
                }

                index += length;
                boundaries.Add(index);
            }

            return boundaries;
        }

        private static int Walk(byte[] data, bool readScalars)
        {
            using (var reader = IonReaderBuilder.Build(new MemoryStream(data)))
            {
                return WalkLevel(reader, readScalars);
            }
        }

        private static int WalkLevel(IIonReader reader, bool readScalars)
        {
            var count = 0;
            IonType type;
            while ((type = reader.MoveNext()) != IonType.None)
            {
                count++;
                if (reader.CurrentIsNull)
                {
                    continue;
                }

                if (type == IonType.List || type == IonType.Sexp || type == IonType.Struct)
                {
                    reader.StepIn();
                    count += WalkLevel(reader, readScalars);
                    reader.StepOut();
                }
                else if (readScalars)
                {
                    ReadScalar(reader, type);
                }
            }

            return count;
        }

        private static void ReadScalar(IIonReader reader, IonType type)
        {
            if (type == IonType.Int)
            {
                reader.BigIntegerValue();
            }
            else if (type == IonType.Float)
            {
                reader.DoubleValue();
            }
            else if (type == IonType.Decimal)
            {
                reader.DecimalValue();
            }
            else if (type == IonType.Timestamp)
            {
                reader.TimestampValue();
            }
            else if (type == IonType.String)
            {
                reader.StringValue();
            }
            else if (type == IonType.Symbol)
            {
                reader.SymbolValue();
            }
            else if (type == IonType.Blob || type == IonType.Clob)
            {
                reader.NewByteArray();
            }
        }

        /// <summary>
        /// Returns at most one byte per read, as a network stream may.
        /// </summary>
        private sealed class OneByteStream : MemoryStream
        {
            public OneByteStream(byte[] data)
                : base(data)
            {
            }

            public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));

            public override int Read(Span<byte> buffer) => base.Read(buffer.Slice(0, Math.Min(buffer.Length, 1)));
        }
    }
}
