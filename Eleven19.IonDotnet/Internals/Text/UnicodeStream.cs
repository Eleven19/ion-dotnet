/*
 * Copyright 2020 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

namespace Eleven19.IonDotnet.Internals.Text
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal class UnicodeStream : TextStream
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly StreamReader streamReader;
        private readonly TailTrackingStream tail;
        private readonly Stack<int> unreadStack = new Stack<int>();

        public UnicodeStream(Stream inputStream)
            : this(inputStream, Encoding.UTF8)
        {
        }

        public UnicodeStream(Stream inputStream, Encoding encoding)
        {
            if (!inputStream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable", nameof(inputStream));
            }

            this.tail = new TailTrackingStream(inputStream);
            this.streamReader = new StreamReader(this.tail, Decoding(encoding));
        }

        public UnicodeStream(Stream inputStream, Span<byte> readBytes)
            : this(inputStream, Encoding.UTF8, readBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnicodeStream"/> class.
        /// Create utf8 byte stream after already reading some bytes.
        /// </summary>
        /// <param name="inputStream">Input stream.</param>
        /// <param name="encoding">The type of encoding.</param>
        /// <param name="readBytes">Bytes read.</param>
        public UnicodeStream(Stream inputStream, Encoding encoding, Span<byte> readBytes)
        {
            if (!inputStream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable", nameof(inputStream));
            }

            this.tail = new TailTrackingStream(inputStream);
            this.streamReader = new StreamReader(this.tail, Decoding(encoding));
            if (inputStream.CanSeek)
            {
                inputStream.Seek(-readBytes.Length, SeekOrigin.Current);
                return;
            }

            for (var i = readBytes.Length - 1; i >= 0; i--)
            {
                this.unreadStack.Push(readBytes[i]);
            }
        }

        public override int Read()
        {
            if (this.unreadStack.Count > 0)
            {
                return this.unreadStack.Pop();
            }

            int value;
            try
            {
                value = this.streamReader.Read();
            }
            catch (DecoderFallbackException exception)
            {
                throw new IonException("Input stream is not a valid UTF-8 stream.", exception);
            }

            // Some runtimes do not flush StreamReader's decoder at the end of the stream, so a cut
            // sequence in the last bytes reads as if it were not there.
            if (value == -1
                && this.streamReader.CurrentEncoding.CodePage == Encoding.UTF8.CodePage
                && this.tail.EndsInsideUtf8Sequence())
            {
                throw new IonException("Input stream is not a valid UTF-8 stream.");
            }

            return value;
        }

        public override void Unread(int c)
        {
            if (c == -1)
            {
                return;
            }

            this.unreadStack.Push(c);
        }

        /// <summary>
        /// Ion text must be valid, so UTF-8 decoding throws instead of substituting replacement characters.
        /// </summary>
        private static Encoding Decoding(Encoding encoding) =>
            encoding.CodePage == Encoding.UTF8.CodePage ? StrictUtf8 : encoding;

        /// <summary>
        /// Passes reads through and remembers the last bytes read.
        /// </summary>
        private sealed class TailTrackingStream : Stream
        {
            private readonly Stream inner;
            private readonly byte[] last = new byte[3];
            private int lastCount;

            public TailTrackingStream(Stream inner)
            {
                this.inner = inner;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            /// <summary>
            /// Whether the bytes end with a UTF-8 lead byte that lacks the continuation bytes it announces.
            /// </summary>
            public bool EndsInsideUtf8Sequence()
            {
                for (var back = 1; back <= this.lastCount; back++)
                {
                    var value = this.last[this.lastCount - back];
                    if ((value & 0xC0) == 0x80)
                    {
                        continue;
                    }

                    var length = value >= 0xF0 ? 4 : value >= 0xE0 ? 3 : value >= 0xC0 ? 2 : 1;
                    return length > back;
                }

                return false;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var amount = this.inner.Read(buffer, offset, count);
                if (amount >= this.last.Length)
                {
                    Array.Copy(buffer, offset + amount - this.last.Length, this.last, 0, this.last.Length);
                    this.lastCount = this.last.Length;
                }
                else if (amount > 0)
                {
                    var keep = Math.Min(this.lastCount, this.last.Length - amount);
                    Array.Copy(this.last, this.lastCount - keep, this.last, 0, keep);
                    Array.Copy(buffer, offset, this.last, keep, amount);
                    this.lastCount = keep + amount;
                }

                return amount;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
