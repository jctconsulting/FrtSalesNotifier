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
using System.Configuration;

namespace SaleNotifier
{
    public class Program
    {
        public static Boolean statusflag;
        public static String tgidString;
        public static String poidString;
        public static String invString;
        public static String soldString;
        public static String priceString;
        public static String extpoString;
        public static int brokerNum;
        public static bool tntrans;





        static void Main(string[] args)
        {

            //test  -   CreateCatListing("121783");
            

            // we will want to add a time interval qualifier to invoice date after testing is done
            string sqlstr = "SELECT dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id, COUNT(*) AS numsold,ticket.purchase_order_id FROM dbo.invoice INNER JOIN dbo.ticket ON dbo.invoice.invoice_id = dbo.ticket.invoice_id INNER JOIN dbo.ticket_group ON dbo.ticket.ticket_group_id = dbo.ticket_group.ticket_group_id WHERE (dbo.ticket_group.internal_notes LIKE \'%Romans%\') or (dbo.ticket_group.internal_notes LIKE \'FRT-Unbroadcast%\') GROUP BY dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id,ticket.purchase_order_id";
            // "SELECT invoice.isautoprocessed, invoice.client_broker_id_for_mercury_buyer , invoice.generated_by_pos_api , ticket_group.ticket_group_id, invoice.create_date, invoice.user_office_id, invoice.invoice_id FROM invoice INNER JOIN ticket ON invoice.invoice_id = ticket.invoice_id INNER JOIN ticket_group ON ticket.ticket_group_id = ticket_group.ticket_group_id WHERE(ticket_group.internal_notes LIKE \'%Romans%\')";

            //sandbox - frt-vivdsql
            string connectionString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;
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
                    tgidString = reader[0].ToString();
                    poidString = reader[3].ToString();
                    invString = reader[1].ToString();
                    soldString = reader[2].ToString();

                    // check to see if we have notified before
                    string specSaleString = ConfigurationManager.ConnectionStrings["TicketTracker"].ConnectionString;
                    SqlConnection specSaleConnection = new SqlConnection(specSaleString);
                    specSaleConnection.Open();
                    SqlCommand saleCommand = new SqlCommand();
                    saleCommand.Connection = specSaleConnection;
                    saleCommand.CommandText = "Select * from SpecSales where ticket_group_id = " + tgidString;
                    SqlDataReader saleReader = saleCommand.ExecuteReader();
                    saleReader.Read();
                    Boolean notified = saleReader.HasRows;
                   
                   

                    // Make a REST post to jessica here - 
                    Console.WriteLine( reader[0].ToString(), reader[1].ToString());
                    Uri endpoint = new Uri("https://spec.pokemonion.com/listings/consignment/sold");        //("https://jessica-cr.xyz/listings/consignment/sold");  
                    string requeststr = "{\"ticketGroupId\":\"" + tgidString + "\" ,\"soldQuantity\":" + soldString +"}";
                    statusflag = false; //false -production
                    if (!notified)
                    {
                        GetPOSTResponse(endpoint, requeststr);
                    }
                    
                    if (statusflag){


                        // *************Removed for testing************
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.CreateNoWindow = true;
                        startInfo.UseShellExecute = false;
                        startInfo.FileName = "c:\\microservice\\ConsoleUnbroadcastTG.exe";
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.Arguments = reader[0].ToString();
                        Process.Start(startInfo);
                                                                    
                        tntrans = false;
                        tntrans = checkTransactionType(Int32.Parse(tgidString));

                        if (!notified)
                        {
                            AddSpecSale();
                            //Create cat even if we aren't reversing inv/po out - but don't do listing has already been processed
                            CreateCatListing(reader[0].ToString());
                        }
                        specSaleConnection.Close(); 
                        
                        if (!tntrans)
                        {
                            // Don't void PO if invoice fails to void - creates orphaned invoice
                            if (VoidInvoice(Int32.Parse(invString))==0)
                            {
                                VoidPO(Int32.Parse(poidString));
                            }
                            else
                            {
                                LogEntry("PO not voided - Invoice void failed", "fail");
                            }
                        }
                        else
                        {
                            LogEntry("Tnet Transaction - Nothing Voided", "warn");
                        }
                       

                    }
                    else
                    {
                        if (!notified)//only log an actual fail rather than  skipped
                        {
                            LogEntry("Jessica did not Accept", "fail");
                        }

                    }
                }
            }
            else 
            {
                Console.WriteLine("No rows found.");
                Environment.Exit(1);
            }
            reader.Close();
            Environment.Exit(0);


        }

        
      
        public static void GetPOSTResponse(Uri uri, string data)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);

            request.Method = "POST"; //"GET"; 
            request.ContentType = "application/json;charset=utf-8";
            request.Headers.Add("Authorization", "Basic c3BlYzptcDJ3ODRuUU05VlNOa1BK");

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
          
            string invconnectionString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;            
            SqlConnection invconnection = new SqlConnection(invconnectionString);
            invconnection.Open();

            

            string invsqlstring = "DECLARE @RC int EXECUTE @RC = [dbo].[pos_invoice_void] " + invoiceid + ",0,0,0,\'Romans\',1"; 
            SqlCommand catlistCommand = new SqlCommand();
            catlistCommand.Connection = invconnection;
            catlistCommand.CommandText = invsqlstring;
            try
            {
                catlistCommand.ExecuteScalar();
            }
            catch(Exception ex)
            {
                LogEntry("Invoice Void Failed:" + ex.Message , "fail");
                invconnection.Close();
                return 1;
            }

            invconnection.Close();
            LogEntry("Invoice Voided", "success");
            return 0;

        }

        public static int VoidPO(int poid)
        {

            // call purchase_order_void_po
            
            string voidString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;
            
            SqlConnection voidCon = new SqlConnection(voidString);
            voidCon.Open();
            SqlCommand voidCom = new SqlCommand();
            voidCom.Connection = voidCon;
            voidCom.CommandText = "declare @RC int Execute @RC = [dbo].[purchase_order_void_po] " + poid + ",1,6"; //tranofficeid,sysuserid
            try
            {
                voidCom.ExecuteScalar();                
                LogEntry("PO Voided", "success");
                voidCon.Close();
                return 0;
            }
            catch(Exception ex)
            {
                LogEntry("PO not voided:" + ex.Message, "fail");
                return 1;
            }


            

        }

        public static int CreateCatListing(string oldtg)
        {
            int tgid=0;
            // GetTGInfo

            string sqlstring =  "Select * from ticket_group where ticket_group_id = " + oldtg;
            string clconnectionString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;
            
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

            String qtyString = soldString;//reader[4].ToString();

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
            // account for ' in venue name
            venueName = venueName.Replace("'", "''");
            venueReader.Close();
            //make CatListing
          
            string catsqlstring = "Declare @RC int EXECUTE @RC = [dbo].[api_category_ticket_group_create] " + exchangeEventId + ",\'" + /*eventName +*/ "\','" + eventDate + "\'," + venueId + ",\'" + venueName + "\',\'" + sectionString + "\',\'\',\'" + rowString + "\'," + "\'\'" + "," + seatlowString + "," + seathighString + "," + qtyString + "," + "0,0,0,0,\'" + onHand  + "\',\'Sale - RC\'," +"\'\'" +"," + "6," + "2," + localeventString;

  

            SqlCommand catlistCommand = new SqlCommand();
            catlistCommand.Connection = clconnection;
            catlistCommand.CommandText =catsqlstring;
            try
            {
                SqlDataReader catListingReader;
                catListingReader=  catlistCommand.ExecuteReader();
                catListingReader.Read();
                string result = catListingReader[0].ToString();
                string catTgid = catListingReader[2].ToString();
                if (catTgid == "-1") //api function call failed - zoned event likely -> force it
                {
                    string vcInsertstr = @" INSERT dbo.venue_category (section_high , section_low, row_low , row_high, seat_low, seat_high, default_wholesale_price, default_retail_price, venue_id, default_cost, default_face_price, text_desc, show_to_sales_staff, default_ticket_group_notes, ticket_group_stock_type_id, ticket_group_type_id, venue_configuration_zone_id)
                                           VALUES ( '' , @section_low, @row_low, @row_high, '', '', 0.00, 0.00, @venue_id, 0.00, 0.00, '', 1, '', 1, 1, NULL );";

                    SqlCommand vcInsert = new SqlCommand();
                    SqlConnection vcCon = new SqlConnection(ConfigurationManager.ConnectionStrings["indux"].ConnectionString);
                    vcCon.Open();
                    vcInsert.Connection =vcCon ; // clconnection;

                    vcInsert.CommandText = vcInsertstr;
             //       vcInsert.CommandType = System.Data.CommandType.StoredProcedure;

                    SqlParameter section = new SqlParameter();
                    section.ParameterName = "@section_low";
                    section.Value = sectionString;
                    vcInsert.Parameters.Add(section);

                    SqlParameter rowhigh = new SqlParameter();
                    rowhigh.ParameterName = "@row_high";
                    rowhigh.Value = rowString;
                    vcInsert.Parameters.Add(rowhigh);

                    SqlParameter rowlow = new SqlParameter();
                    rowlow.ParameterName = "@row_low";
                    rowlow.Value = rowString;
                    vcInsert.Parameters.Add(rowlow);

                    SqlParameter venue = new SqlParameter();
                    venue.ParameterName = "@venue_id";
                    venue.Value = venueId;
                    vcInsert.Parameters.Add(venue);


               
                    vcInsert.ExecuteScalar();
                    vcCon.Close();
                    vcCon.Open();
                    //Make sure vconfig was inserted and get its id
                    SqlCommand vcCheck = new SqlCommand();
                    vcCheck.Connection = vcCon;
                    vcCheck.CommandText = "SELECT * FROM dbo.venue_category vc	WHERE vc.venue_id = " + venueId + " AND vc.row_high = \'" +rowString + "\' AND vc.row_low = \'" + rowString+  "\' AND vc.section_low = \'" + sectionString + "\'" ;
                    SqlDataReader vcReader = vcCheck.ExecuteReader();
                    vcReader.Read();
                    if (vcReader.HasRows)
                    {
                        // add catlisting directly
                        SqlCommand directCat = new SqlCommand();
                        SqlConnection fclCon = new SqlConnection(ConfigurationManager.ConnectionStrings["indux"].ConnectionString);
                        directCat.Connection = fclCon;
                        fclCon.Open();
                        directCat.CommandText = @"INSERT category_ticket_group(event_id, venue_category_id, ticket_count, wholesale_price, retail_price, notes, expected_arrival_date, face_price, cost, internal_notes, tax_exempt, update_datetime, broadcast, create_date, office_id
		                                          , ticket_group_code_id, unbroadcast_days_before_event, shipping_method_special_id, show_near_term_option_id, price_update_datetime, tg_note_grandfathered, auto_process_web_requests, venue_configuration_zone_id, max_showing)
	                                              OUTPUT INSERTED.category_ticket_group_id
                                                  VALUES(@local_event_id, @venue_category_id, @quantity, 0, 0, '', @expected_arrival_date, 0, 0, 'Sale-RC', 0, CURRENT_TIMESTAMP, 0, CURRENT_TIMESTAMP
                                                  , 1, NULL, 0, 6, 2, NULL, 0, 1, NULL, NULL)";

                        SqlParameter localevent = new SqlParameter();
                        localevent.ParameterName = "@local_event_id";
                        localevent.Value = localeventString;
                        directCat.Parameters.Add(localevent);

                        SqlParameter vcid = new SqlParameter();
                        vcid.ParameterName = "@venue_category_id";
                        vcid.Value = vcReader[0].ToString();
                        directCat.Parameters.Add(vcid);

                        SqlParameter qty = new SqlParameter();
                        qty.ParameterName = "@quantity";
                        qty.Value = qtyString;
                        directCat.Parameters.Add(qty);

                        SqlParameter arrival = new SqlParameter();
                        arrival.ParameterName = "@expected_arrival_date";
                        arrival.Value = onHand;
                        directCat.Parameters.Add(arrival);
/*
                        directCat.Parameters["@local_event_id"].Value = localeventString;
                        directCat.Parameters["@venue_category_id"].Value =vcReader[0].ToString() ;
                        directCat.Parameters["@quantity"].Value = qtyString;
                        directCat.Parameters["@expected_arrival_date"].Value = onHand;*/
                        catTgid = directCat.ExecuteScalar().ToString();
                        if (Int32.Parse(catTgid) > 0)
                        {
                            for (int i = 1; i < Int32.Parse(qtyString)+1; i++)
                            {
                                directCat.CommandText = "INSERT into dbo.category_ticket (category_ticket_group_id,position,actual_price,invoice_id,ticket_id,processed,system_user_id,exchange_request_id,fill_date) Values(" + catTgid + "," + i.ToString() + ",-1.00,NULL,NULL,0,NULL,NULL,NULL)";
                                directCat.Parameters.Clear();
                                directCat.ExecuteScalar();

                            }
                        } 


                        vcCon.Close();
                        fclCon.Close();
                        

                    }
                    else
                    {
                        LogEntry("Cat LIsting Not Created: VC not added" , "fail");
                        vcCon.Close();
                        return 0;
                    }

                } 
          
                LogEntry("Cat LIsting Created: " + catTgid, "success");
                if (!tntrans) { SellCatListing(catTgid); }
                return Int32.Parse(catTgid);
            }
            catch (Exception ex){

                LogEntry("Cat LIsting Creation Failed:" + ex.Message, "fail");
                return 0;
            }
            //clconnection.Open();


            // note should indicate this is a sale

            
        }

        public static Boolean SellCatListing(string catListStr)
        {
            string getTixStr = "Select [category_ticket_id],0,0," + priceString + " from Category_ticket where category_ticket_group_id = " + catListStr;
            string connectionString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand Command = new SqlCommand();
            Command.Connection = connection;
            Command.CommandText = getTixStr;
            SqlDataAdapter da = new SqlDataAdapter(Command);
            DataTable tix = new DataTable();
            da.Fill(tix);



          /* 

            string shipStr = @"INSERT into dbo.shipping_tracking(shipping_tracking_number, shipped_on_date, arrived_on_date, estimated_arrival_date, estimated_ship_date, notes, shipping_account_type_id, shipping_account_number_id, shipping_account_delivery_method_id, shipping_tracking_status_id, shipping_tracking_cost, will_call_pickup_name, runner_id, delivered_datetime, shipment_signed_for, runner_delivered_shipment_on, isreturned, cod_label_tracking_number, marked_for_call, called_awaiting_shipment, isdrop, save_path, ftp_location, system_user_id)
 Values('', Getdate(), '1900-01-01 00:00:00.000', '1900-01-01 00:00:00.000', NULL, '', 4, NULL, 22, 1, 0.00, '', NULL, NULL, '-', NULL, 0, NULL, 0, 0, 1, '', '', NULL); set @id = SCOPE_IDENTITY();";
            SqlCommand shipInsert = new SqlCommand();
            shipInsert.Connection = connection;
            connection.Open();
            shipInsert.CommandText = shipStr;
            SqlParameter id = new SqlParameter();
            id.SqlDbType = System.Data.SqlDbType.Int;
            id.ParameterName = "@id";
            id.Value = 0;
            id.Direction = System.Data.ParameterDirection.Output;


            shipInsert.Parameters.Add(id);
            shipInsert.ExecuteNonQuery();
            string shipnum = id.Value.ToString();
*/

            string orderString = "Select [external_po_number],[client_broker_id] from dbo.purchase_Order where purchase_order_id = " + poidString;
            SqlCommand ordCommand = new SqlCommand();
            ordCommand.Connection = connection;
            ordCommand.CommandText = orderString;
            connection.Open();
            SqlDataReader ordreader = ordCommand.ExecuteReader();
            string addressid = "";
            string extpo = "";


            string cbinvoice = "SELECT [invoice_id],[client_broker_id],[client_broker_employee_id] FROM [dbo].[client_broker_invoice]  where invoice_id =" + invString;

            if (ordreader.HasRows)
            {
                ordreader.Read();
                extpo = ordreader[0].ToString();

            }
            connection.Close();
            ordreader.Close();
            ordCommand.CommandText = cbinvoice;            
            connection.Open();
            ordreader = ordCommand.ExecuteReader();


            //  ordreader.Open
            if (ordreader.HasRows)
            {
                ordreader.Read();

                //get broker address 
                brokerNum = Int32.Parse(ordreader[1].ToString());
                string brokerString = "Select main_address_id from dbo.client_broker where client_broker_id = " + brokerNum;
                SqlCommand brokerCommand = new SqlCommand();
                SqlConnection brokerCon = new SqlConnection(connectionString);
                brokerCommand.Connection = brokerCon;
                brokerCommand.CommandText = brokerString;
                brokerCon.Open();
                SqlDataReader brokerReader = brokerCommand.ExecuteReader();

                if (brokerReader.HasRows)
                {
                    brokerReader.Read();
                    addressid = brokerReader[0].ToString();

                }
               
                

            }


            string invNotes = "Romans";
            // empty table for invoice tix
            // Create a new DataTable.
            System.Data.DataTable rtix = new DataTable("invoiceTix");
            // Declare variables for DataColumn and DataRow objects.
            DataColumn column;
            DataRow row;

            // Create new DataColumn, set DataType, 
            // ColumnName and add to DataTable.    
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Int32");
            column.ColumnName = "ticket_id";
            column.ReadOnly = true;
            column.Unique = true;

            rtix.Columns.Add(column);

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Int32");
            column.ColumnName = "ticket_group_id";
            column.ReadOnly = true;
            column.Unique = true;

            rtix.Columns.Add(column);

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Int32");
            column.ColumnName = "seat_number";
            column.ReadOnly = true;
            column.Unique = true;

            rtix.Columns.Add(column);

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Single"); 
            column.ColumnName = "actual_sold_price";
            column.ReadOnly = true;
            column.Unique = true;

            rtix.Columns.Add(column);


            //==========================================


            string invstr = "execute [dbo].[api_invoice_create] " + "\'\'" + ",NULL,24,null,4,null,null," + brokerNum + "," + addressid+ ",0,0,0,4," + "\'" + invNotes + "\',\'\',\'" + extpoString + "\',5,1,1,\'\',@realtix,@tix,1,0,0,0";

            SqlParameter rtixParm = new SqlParameter();
            rtixParm.ParameterName = "@tix";
            rtixParm.Value = tix;
            rtixParm.TypeName = "[dbo].[invoice_category_ticket_tvt]";
            //empty real tix
            SqlParameter tixParm = new SqlParameter();
            tixParm.ParameterName = "@realtix";
            tixParm.Value = rtix;
            tixParm.TypeName = "[dbo].[invoice_ticket_tvt]";


            connection.Close();
            SqlCommand invInsert = new SqlCommand();
            invInsert.Connection = connection;
            connection.Open();
            invInsert.CommandText = invstr;
            invInsert.Parameters.Add(tixParm);
            invInsert.Parameters.Add(rtixParm);
            string retval;
            retval = invInsert.ExecuteScalar().ToString();
            connection.Close();

            return false;
        }


        public static Boolean checkTransactionType(int TGID)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;
          
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
              WHERE        (invoice.mercury_transaction_id is null) and   (invoice.external_PO not like '0') and  (invoice.external_PO not like 'n/a%') and ticket_group.ticket_group_id =";

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
         public static void LogEntry(string message,string status)
        {
            string logString = ConfigurationManager.ConnectionStrings["TicketTracker"].ConnectionString;
            SqlConnection logConnection = new SqlConnection(logString);
            logConnection.Open();
            string logQuery = @"INSERT INTO [TicketTracker].[dbo].[Sales_log]  ([TGID],[POID] ,[InvoiceID],[message] ,[status])
                                 VALUES (" + tgidString + "," + poidString + "," + invString + ",\'" + message + "\',\'" + status + "\')";
            SqlCommand logCommand = new SqlCommand();
            logCommand.Connection = logConnection;
            logCommand.CommandText = logQuery;
            logCommand.ExecuteNonQuery();
            logConnection.Close();

        }

        public static void AddSpecSale()
        {

            //check for previos log of this tg

            //   NotifyFRT(); none for now - Ari get from jessica 
            string specSalestr = @"SELECT DISTINCT invoice.mercury_transaction_id,invoice.external_PO,ticket_group.client_broker_id,event.event_name, event.event_datetime, venue.name, ticket_group.ticket_group_id,  ticket_group.cost, 
                         ticket_group.wholesale_price, ticket_group.row, ticket_group.section, ticket_group.internal_notes, ticket_group.notes, ticket_group.last_wholesale_price,  invoice.invoice_balance_due, invoice.invoice_total, ticket_group.actual_purchase_date,invoice.sent_in_update_datetime
                         FROM invoice INNER JOIN ticket ON invoice.invoice_id = ticket.invoice_id RIGHT OUTER JOIN ticket_group INNER JOIN event ON ticket_group.event_id = event.event_id INNER JOIN
                         venue ON event.venue_id = venue.venue_id ON ticket.ticket_group_id = ticket_group.ticket_group_id
			             where      ticket_group.ticket_group_id =" + tgidString;

            SqlCommand specsale = new SqlCommand();
            SqlConnection srconnection = new SqlConnection(ConfigurationManager.ConnectionStrings["Indux"].ConnectionString);
            srconnection.Open();
            specsale.Connection = srconnection;
            specsale.CommandText = specSalestr;
            SqlDataReader specReader = specsale.ExecuteReader();

            string specSaleString = ConfigurationManager.ConnectionStrings["TicketTracker"].ConnectionString;
            SqlConnection specSaleConnection = new SqlConnection(specSaleString);
            specSaleConnection.Open();
            specReader.Read();
            string eventString = specReader[3].ToString();
            eventString = eventString.Replace("'", "''");

            string venueString = specReader[5].ToString();
            venueString = venueString.Replace("'", "''");
            //set this here so we can use for selling out cat
            //use atif calc total div qty - account for takebacks from exchanges.
            int qty;
            Int32.TryParse(soldString, out qty);
            float price = (float.Parse(specReader[15].ToString()) / qty );
            priceString = price.ToString();
            extpoString = specReader[1].ToString();

            string specRecord = @"INSERT INTO [dbo].[SpecSales]
                                            ([Ticket_group_id],[invoice_id],[purchase_order_id],[Ordernum],[ExternalPO],[EventName],[EventDate],[VenueName],[State],[City],[Quantity],[Section],[Row],[SalePrice],[OrderTotal],[SaleDate])
                                            VALUES
                                            (" + tgidString + "," + invString + "," + poidString + ",\'" +specReader[0].ToString() +  "\',\'" +  specReader[1].ToString() + "\',\'" + eventString + "\',\'" + specReader[4].ToString() + "\',\'" + venueString + "\',\'" + "\',\'\',\'"  + soldString +"\',\'" + specReader[10].ToString() + "\',\'" + specReader[9].ToString() + "\',\'" + specReader[8].ToString() + "\',\'" + specReader[15].ToString() + "\',\'" + specReader[17].ToString() + "\')";

            SqlCommand insSpecRecord = new SqlCommand();
            insSpecRecord.Connection = specSaleConnection;
            insSpecRecord.CommandText = specRecord;
            insSpecRecord.ExecuteNonQuery();
            specSaleConnection.Close();
            srconnection.Close();
        }


    }
}
