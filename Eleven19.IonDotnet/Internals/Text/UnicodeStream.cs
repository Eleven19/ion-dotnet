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

            this.streamReader = new StreamReader(inputStream, Decoding(encoding));
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

            this.streamReader = new StreamReader(inputStream, Decoding(encoding));
            if (inputStream.CanSeek)
            {
                this.InputStream.Seek(-readBytes.Length, SeekOrigin.Current);
                return;
            }

            for (var i = readBytes.Length - 1; i >= 0; i--)
            {
                this.unreadStack.Push(readBytes[i]);
            }
        }

        private Stream InputStream => this.streamReader.BaseStream;

        public override int Read()
        {
            if (this.unreadStack.Count > 0)
            {
                return this.unreadStack.Pop();
            }

            try
            {
                return this.streamReader.Read();
            }
            catch (DecoderFallbackException exception)
            {
                throw new IonException("Input stream is not a valid UTF-8 stream.", exception);
            }
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
    }
}
