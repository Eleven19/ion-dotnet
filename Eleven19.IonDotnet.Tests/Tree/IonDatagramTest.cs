using Eleven19.IonDotnet.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eleven19.IonDotnet.Tests.Tree
{
    [TestClass]
    public class IonDatagramTest
    {
        [TestMethod]
        [DataRow("")]
        [DataRow("1 true null.string")]
        [DataRow("tag::{a:[1,2],b:(x y)} -0.00 1.00")]
        public void SeparatelyLoadedDatagramsAreEquivalent(string text)
        {
            var first = IonLoader.Default.Load(text);
            var second = IonLoader.Default.Load(text);
            Assert.AreNotSame(first, second);
            Assert.IsTrue(first.IsEquivalentTo(second));
            Assert.IsTrue(second.IsEquivalentTo(first));
            Assert.IsTrue(first.IsEquivalentTo(first));
        }

        [TestMethod]
        [DataRow("1 2", "2 1")]
        [DataRow("1", "1 2")]
        [DataRow("[1]", "[2]")]
        [DataRow("tag::1", "1")]
        [DataRow("0.0", "0.00")]
        public void DifferentDatagramsAreNotEquivalent(string left, string right)
        {
            var first = IonLoader.Default.Load(left);
            var second = IonLoader.Default.Load(right);
            Assert.IsFalse(first.IsEquivalentTo(second));
            Assert.IsFalse(second.IsEquivalentTo(first));
        }

        [TestMethod]
        public void DatagramIsNotEquivalentToNullOrList()
        {
            var datagram = IonLoader.Default.Load("1");
            var list = IonLoader.Default.Load("[1]").GetElementAt(0);
            Assert.IsFalse(datagram.IsEquivalentTo(null));
            Assert.IsFalse(datagram.IsEquivalentTo(list));
        }
    }
}
