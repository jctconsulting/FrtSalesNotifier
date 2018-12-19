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
            string CattoSell = ""; //test cat tgid goes here

            Program.SellCatListing(CattoSell);


        }

        [TestMethod]
        public void TestAddSpec()
        {
            Program.tgidString = ""; //test tgid goes here

            Program.AddSpecSale();


        }


    }
}
