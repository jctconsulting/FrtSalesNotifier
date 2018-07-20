using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace SaleNotifier
{
    class Program
    {
        public static Boolean statusflag;




        static void Main(string[] args)
        {

          //test  -   CreateCatListing("121783");


            // we will want to add a time interval qualifier to invoice date after testing is done
            string sqlstr = "SELECT dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id, COUNT(*) AS numsold FROM dbo.invoice INNER JOIN dbo.ticket ON dbo.invoice.invoice_id = dbo.ticket.invoice_id INNER JOIN dbo.ticket_group ON dbo.ticket.ticket_group_id = dbo.ticket_group.ticket_group_id WHERE (dbo.ticket_group.internal_notes LIKE \'%Romans%\') GROUP BY dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id";
            // "SELECT invoice.isautoprocessed, invoice.client_broker_id_for_mercury_buyer , invoice.generated_by_pos_api , ticket_group.ticket_group_id, invoice.create_date, invoice.user_office_id, invoice.invoice_id FROM invoice INNER JOIN ticket ON invoice.invoice_id = ticket.invoice_id INNER JOIN ticket_group ON ticket.ticket_group_id = ticket_group.ticket_group_id WHERE(ticket_group.internal_notes LIKE \'%Romans%\')";

            //sandbox
            string connectionString = "Data Source=10.10.25.143;Initial Catalog=indux;Persist Security Info=True;User ID=sa;Password=Dnt721976";
           //production
            // string connectionString = "Data Source=10.10.25.6;Initial Catalog=indux;Persist Security Info=True;User ID=FRT;Password=Dnt721976";
            //string providerName = "System.Data.SqlClient";


            SqlConnection connection = new SqlConnection(connectionString);

            SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandText = sqlstr;
            


            connection.Open();
            SqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    // Make a REST post to jessica here - 
                    Console.WriteLine( reader[0].ToString(), reader[1].ToString());
                    Uri endpoint = new Uri("https://jessica-cr.xyz/listings/consignment/sold");  
                    string requeststr = "{\"ticketGroupId\":\"" + reader[0].ToString() + "\" ,\"soldQuantity\":" + reader[2].ToString() +"}";
                    statusflag = false;
                    GetPOSTResponse(endpoint,requeststr);
                    if (statusflag){

                        /* *************Removed for testing************

                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.CreateNoWindow = true;
                        startInfo.UseShellExecute = false;
                        startInfo.FileName = "c:\\microservice\\ConsoleUnbroadcastTG.exe";
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.Arguments = reader[0].ToString();
                        Process.Start(startInfo);
                        */

                        //Create cat even if we aren't reversing inv/po out  
                        CreateCatListing(reader[0].ToString());
                        bool tntrans = false;
                        tntrans = checkTransactionType(Int32.Parse(reader[0].ToString()));
                        if (!tntrans)
                        {
                            int poid = VoidInvoice(Int32.Parse(reader[1].ToString()));
                            VoidPO(poid);
                        }
                     //   NotifyFRT(); none for now - Ari get from jessica 

                        
                    }
                }
            }
            else 
            {
                Console.WriteLine("No rows found.");
            }
            reader.Close();


        }

        
      /*  public class statusMessage
        {
            public Boolean status { get; set}
            public string message { get; set} 

        } */
        public static void GetPOSTResponse(Uri uri, string data)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);

            request.Method = "POST"; //"GET"; 
            request.ContentType = "application/json;charset=utf-8";
            request.Headers.Add("Authorization", "Basic YXBwdXNlcjp5dWROOWJZRkdKdVlRRHBq");

            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            byte[] bytes = encoding.GetBytes(data);

            request.ContentLength = bytes.Length;
            request.Timeout = 60000;
            using (Stream requestStream = request.GetRequestStream())
            {
                // Send the data.
                requestStream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                HttpWebResponse response= (HttpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();
           
                if ((response.StatusCode == HttpStatusCode.OK) ||  (response.StatusCode == HttpStatusCode.Accepted))
                {
                    statusflag = true;
                }
                else
                {
                    statusflag = false;
                }


              //  statusflag = msg.status;
                response.Close();
            }
            catch
            {
                Console.WriteLine("Post Failed");
                
            }


        }
        public static int VoidInvoice(int invoiceid)
        {
            int poid = 0;
            string invconnectionString = "Data Source=10.10.25.6;Initial Catalog=indux;Persist Security Info=True;User ID=FRT;Password=Dnt721976";
            SqlConnection invconnection = new SqlConnection(invconnectionString);
            string invsqlstring = "EXECUTE @RC = [dbo].[pos_invoice_void] " + invoiceid + ",0,0, ,,1"; 
            SqlCommand catlistCommand = new SqlCommand();
            catlistCommand.Connection = invconnection;
            catlistCommand.CommandText = invsqlstring;
            catlistCommand.ExecuteScalar();
            invconnection.Close();

            //get invoice id for this sale
            // what to do if multiple invoices?
                
            return poid;

        }

        public static int VoidPO(int poid)
        {

            // call purchase_oder_void_po


            return 0;

        }

        public static int CreateCatListing(string oldtg)
        {
            int tgid=0;
            // GetTGInfo

            string sqlstring =  "Select * from ticket_group where ticket_group_id = " + oldtg;
            string clconnectionString = "Data Source=10.10.25.6;Initial Catalog=indux;Persist Security Info=True;User ID=FRT;Password=Dnt721976";
            SqlConnection clconnection = new SqlConnection(clconnectionString);
            SqlCommand command = new SqlCommand();
            command.Connection = clconnection;
            command.CommandText = sqlstring;
            clconnection.Open();
            SqlDataReader reader = command.ExecuteReader();


            reader.Read();     
            // get the event info we need
            SqlCommand eventCommand = new SqlCommand();
            eventCommand.Connection = clconnection;
            eventCommand.CommandText = "Select * from event where event_id = " + reader[14].ToString();

            String rowString = reader[9].ToString();
            String sectionString =   reader[10].ToString();
            String seathighString = "0"; //reader[22].ToString();
            String seatlowString = "0"; //reader[23].ToString();
            String qtyString = reader[4].ToString();
            String localeventString = reader[14].ToString();


            //  clconnection.Open();
            reader.Close();
            SqlDataReader eventReader = eventCommand.ExecuteReader();

            eventReader.Read();
            int exchangeEventId = Int32.Parse(eventReader[12].ToString());
            string eventName = eventReader[1].ToString();
            DateTime eventDate = DateTime.Parse(eventReader[2].ToString());
            DateTime onHand = eventDate.AddDays(-4);
            int venueId = Int32.Parse(eventReader[7].ToString());



            //get Venue Info we need
            SqlCommand venueCommand = new SqlCommand();
            venueCommand.Connection = clconnection;
            venueCommand.CommandText = "Select name from venue where venue_id = " + eventReader[7].ToString();
            //    clconnection.Open();
            eventReader.Close();
            SqlDataReader venueReader = venueCommand.ExecuteReader();
            venueReader.Read();
            string venueName = venueReader[0].ToString();
            venueReader.Close();
            //make CatListing
           
            string catsqlstring = "Declare @RC int EXECUTE @RC = [dbo].[api_category_ticket_group_create] " + exchangeEventId + ",\'" + /*eventName +*/ "\','" + eventDate + "\'," + venueId + ",\'" + venueName + "\'," + sectionString + "," + "\'\'" + "," + rowString + "," + "\'\'" + "," + seatlowString + "," + seathighString + "," + qtyString + "," + "0,0,0,0,\'" + onHand  + "\',\'Sale - RC\'," +"\'\'" +"," + "6," + "2," + localeventString;

  

            SqlCommand catlistCommand = new SqlCommand();
            catlistCommand.Connection = clconnection;
            catlistCommand.CommandText =catsqlstring;
            catlistCommand.ExecuteScalar();
            //clconnection.Open();


            // note should indicate this is a sale

            return tgid;
        }


        public static Boolean checkTransactionType(int TGID)
        {
            string connectionString = "Data Source=10.10.25.6;Initial Catalog=indux;Persist Security Info=True;User ID=FRT;Password=Dnt721976";
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand Command = new SqlCommand();
            Command.Connection = connection;

            string sqlstr = @"SELECT DISTINCT event.event_name, event.event_datetime, venue.name, ticket_group.ticket_group_id, ticket_group.original_ticket_count, ticket_group.remaining_ticket_count, ticket_group.retail_price, ticket_group.face_price, ticket_group.cost, 
                         ticket_group.wholesale_price, ticket_group.row, ticket_group.section, ticket_group.internal_notes, ticket_group.notes, ticket_group.event_id, ticket_group.status_id, ticket_group.ticket_group_seating_type_id, 
                         ticket_group.ticket_group_type_id, ticket_group.client_broker_id, ticket_group.client_broker_employee_id, ticket_group.actual_purchase_date, ticket_group.office_id, ticket_group.price_update_datetime, 
                         ticket_group.last_wholesale_price, ticket_group.update_datetime, invoice.mercury_transaction_id, invoice.invoice_balance_due, invoice.invoice_total, invoice.invoice_total_due
              FROM invoice INNER JOIN
                         ticket ON invoice.invoice_id = ticket.invoice_id RIGHT OUTER JOIN
                         ticket_group INNER JOIN
                         event ON ticket_group.event_id = event.event_id INNER JOIN
                         venue ON event.venue_id = venue.venue_id ON ticket.ticket_group_id = ticket_group.ticket_group_id
              WHERE        (ticket_group.client_broker_id = 5640) AND(ticket_group.internal_notes LIKE 'FRT') and(invoice.external_PO like \'0\') and ticket_group.ticket_group_id =";

            sqlstr = sqlstr + TGID.ToString(); 
            Command.CommandText = sqlstr;
            connection.Open();
            SqlDataReader Reader = Command.ExecuteReader();
           // string venueName = Reader[0].ToString();

            if (Reader.HasRows)
            {               
                //this is a not tnet trans
                return false;
            }
            else
            {
                return true;
            }

        }




    }
}
