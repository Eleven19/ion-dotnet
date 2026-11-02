using System;
using System.IO;
using System.Text;
using Eleven19.IonDotnet.Internals.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eleven19.IonDotnet.Tests.Internals
{
    [TestClass]
    public class TextScannerTest
    {
        private TextScanner CreateScanner(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var stream = new MemoryStream(bytes);
            var textStream = new UnicodeStream(stream);
            return new TextScanner(textStream);
        }

        [TestMethod]
        public void TestMalformedBlobHandling()
        {
            // Test simple malformed blob
            var scanner = CreateScanner("{{");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });

            // Test the specific malformed input that caused the infinite loop
            var hexString = "282f5959595959595959593a3a282b2727357b7b7b7b7b7b7b7b27272728fb2b272829";
            var bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            // 0xFB is not valid UTF-8, so the reader now stops at that byte.
            var stream = new MemoryStream(bytes);
            var textStream = new UnicodeStream(stream);
            scanner = new TextScanner(textStream);

            Assert.ThrowsException<IonException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });

            // The same input with that byte replaced still reaches the end of the stream without looping.
            bytes[Array.IndexOf(bytes, (byte)0xFB)] = (byte)'x';
            scanner = new TextScanner(new UnicodeStream(new MemoryStream(bytes)));

            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });
        }

        [TestMethod]
        public void TestSingleQuotedStringEofHandling()
        {
            // Test EOF in single-quoted string
            var scanner = CreateScanner("'unterminated");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });

            // Test EOF after escape in single-quoted string
            scanner = CreateScanner("'escaped\\");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });
        }

        [TestMethod]
        public void TestTripleQuotedStringEofHandling()
        {
            // Test EOF in triple-quoted string
            var scanner = CreateScanner("'''unterminated");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });

            // Test EOF after partial triple quote
            scanner = CreateScanner("'''content''");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });

            // Test EOF after escape in triple-quoted string
            scanner = CreateScanner("'''escaped\\");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });
        }

        [TestMethod]
        public void TestDoubleQuotedStringEofHandling()
        {
            // Test EOF in double-quoted string
            var scanner = CreateScanner("\"unterminated");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });

            // Test EOF after escape in double-quoted string
            scanner = CreateScanner("\"escaped\\");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });
        }

        [TestMethod]
        public void TestMalformedClobHandling()
        {
            // Test malformed clob with missing closing braces
            var scanner = CreateScanner("{{\"clob content\"");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });

            // Test malformed clob with triple quotes
            scanner = CreateScanner("{{{'''clob content");
            Assert.ThrowsException<UnexpectedEofException>(() =>
            {
                scanner.NextToken();
                while (scanner.Token != TextConstants.TokenEof)
                {
                    scanner.NextToken();
                }
            });
        }
    }
}
