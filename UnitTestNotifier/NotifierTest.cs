using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SaleNotifier;

namespace UnitTestNotifier
{
    [TestClass]
    public class NotifierTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            string CattoSell = "376899";

            Program.SellCatListing(CattoSell);


        }
    }
}
