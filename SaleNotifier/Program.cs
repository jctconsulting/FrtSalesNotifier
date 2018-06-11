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


            // we will want to add a time interval qualifier to invoice date after testing is done
            string sqlstr = "SELECT dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id, COUNT(*) AS numsold FROM dbo.invoice INNER JOIN dbo.ticket ON dbo.invoice.invoice_id = dbo.ticket.invoice_id INNER JOIN dbo.ticket_group ON dbo.ticket.ticket_group_id = dbo.ticket_group.ticket_group_id WHERE (dbo.ticket_group.internal_notes LIKE \'%Romans%\') GROUP BY dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id";
                // "SELECT invoice.isautoprocessed, invoice.client_broker_id_for_mercury_buyer , invoice.generated_by_pos_api , ticket_group.ticket_group_id, invoice.create_date, invoice.user_office_id, invoice.invoice_id FROM invoice INNER JOIN ticket ON invoice.invoice_id = ticket.invoice_id INNER JOIN ticket_group ON ticket.ticket_group_id = ticket_group.ticket_group_id WHERE(ticket_group.internal_notes LIKE \'%Romans%\')";


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
                    Console.WriteLine( reader[0].ToString(), reader[1].ToString());
                    Uri endpoint = new Uri("https://jessica-cr.xyz/listings/consignment/sold");  
                    string requeststr = "{\"ticketGroupId\":\"" + reader[0].ToString() + "\" ,\"soldQuantity\":" + reader[2].ToString() +"}";
                    statusflag = false;
                    GetPOSTResponse(endpoint,requeststr);
                    if (statusflag){
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.CreateNoWindow = true;
                        startInfo.UseShellExecute = false;
                        startInfo.FileName = "c:\\microservice\\ConsoleUnbroadcastTG.exe";
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.Arguments = reader[0].ToString();
                        Process.Start(startInfo);

                        
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

            using (Stream requestStream = request.GetRequestStream())
            {
                // Send the data.
                requestStream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();

                //Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                //StreamReader readStream = new StreamReader(responseStream, encode);
                //var msgstr = readStream.ReadToEnd();              
                //statusMessage msg = JsonConvert.DeserializeObject<statusMessage>(msgstr);
                
                if (response.StatusCode == HttpStatusCode.OK)
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




    }
}
