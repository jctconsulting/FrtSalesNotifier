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
            string CattoSell = "382077"; //test cat tgid goes here
            Program.poidString = "211176";
            Program.invString = "109502";

            Program.SellCatListing(CattoSell);


        }

        [TestMethod]
        public void TestAddSpec()
        {
            Program.tgidString = "421195"; //test tgid goes here

            Program.AddSpecSale();


        }

        public void CreateCat()
        {
            string soldtg = ""; // put sold listing tgid here
            Program.CreateCatListing(soldtg);
        }


    }
}
