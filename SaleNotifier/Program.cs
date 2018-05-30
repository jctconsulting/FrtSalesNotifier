using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.IO;

namespace SaleNotifier
{
    class Program
    {
       




        static void Main(string[] args)
        {


            // we will want to add a time interval qualifier to invoice date after testing is done
            string sqlstr = "SELECT invoice.isautoprocessed, invoice.client_broker_id_for_mercury_buyer , invoice.generated_by_pos_api , ticket_group.ticket_group_id, invoice.create_date, invoice.user_office_id, invoice.invoice_id FROM invoice INNER JOIN ticket ON invoice.invoice_id = ticket.invoice_id INNER JOIN ticket_group ON ticket.ticket_group_id = ticket_group.ticket_group_id WHERE(ticket_group.internal_notes LIKE \'%Romans%\')";


            string connectionString = "Data Source=10.10.25.6;Initial Catalog=indux;Persist Security Info=True;User ID=FRT;Password=Dnt721976";
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
                    Console.WriteLine( reader[3].ToString(), reader[4].ToString());
                    Uri endpoint = new Uri("https://jessica-cr.xyz/listings/consignment/sold");  
                    string requeststr = "{\"ticketGroupId\":\"" + reader[3].ToString() + "\"}";
                    GetPOSTResponse(endpoint,requeststr);
                }
            }
            else 
            {
                Console.WriteLine("No rows found.");
            }
            reader.Close();


        }

        public static void GetPOSTResponse(Uri uri, string data)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);

            request.Method = "POST"; //"GET"; 
            request.ContentType = "application/json;charset=utf-8";
            request.Headers.Add("Authorization", "Basic YXBwdXNlcjp5dWROOWJZRkdKdVlRRHBq");

            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            byte[] bytes = encoding.GetBytes(data);

            request.ContentLength = bytes.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                // Send the data.
                requestStream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }
            catch
            {
                Console.WriteLine("Post Failed");
                
            }


        }




    }
}
