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
using System.Net.Mail;
using RestSharp;

namespace SaleNotifier
{
    public class Program
    {
        public static Boolean statusflag;
        public static String tgidString;
        public static String poidString;
        public static String invString;
        public static String catInvString;
        public static String soldString;
        public static String priceString;
        public static String extpoString;
        public static int brokerNum;
        public static bool tntrans;

        public class specListing
        {

            String id { get; set; }
            int ticketGroupId { get; set; }
            String floor { get; set; }
            String section { get; set; }
            String startRow { get; set; }
            String row { get; set; }
            int quantity { get; set; }
            float lowerPrice { get; set; }
            float price { get; set; }
            float extraFee { get; set; }
            public float priceMultiplier { get; set; }
            int rulePriceMultiplierIndex { get; set; }
            String offerId { get; set; }
            String status { get; set; }
            String creationType { get; set; }
            String creationDate { get; set; }
            String statusChangeDate { get; set; }
            String pricingRuleMultiplierChangeTime { get; set; }
            String eventId { get; set; }
            public String exchange { get; set; }
            String eventName { get; set; }
            String venueName { get; set; }
            String inconsistencyReason { get; set; }
            
        }





        static void Main(string[] args)
        {

            //test  -   CreateCatListing("121783");

            //[Logdate],[Level],[message],[type],[start_time],[end_time]
            string specLogString = ConfigurationManager.ConnectionStrings["TicketTracker"].ConnectionString;
            SqlConnection specLogConnection = new SqlConnection(specLogString);
            specLogConnection.Open();
            SqlCommand logCommand = new SqlCommand();
            logCommand.Connection = specLogConnection;
            logCommand.CommandText = "Insert into status_logs([Logdate] ,[Level],[message] ,[type])   values(Getdate(),'info','Notifier Start','success') ";
            logCommand.ExecuteNonQuery();
            specLogConnection.Close();

            // we will want to add a time interval qualifier to invoice date after testing is done
            string sqlstr = "SELECT dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id, COUNT(*) AS numsold,ticket.purchase_order_id FROM dbo.invoice INNER JOIN dbo.ticket ON dbo.invoice.invoice_id = dbo.ticket.invoice_id INNER JOIN dbo.ticket_group ON dbo.ticket.ticket_group_id = dbo.ticket_group.ticket_group_id WHERE (dbo.invoice.create_date > getdate()-100) and ( (dbo.ticket_group.internal_notes LIKE \'%Romans%\') or (dbo.ticket_group.internal_notes LIKE \'FRT-Unbroadcast%\')) GROUP BY dbo.ticket_group.ticket_group_id, dbo.invoice.invoice_id,ticket.purchase_order_id";
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
                    specSaleConnection.Close();


                    // Make a REST post to Spec here - 
                    Console.WriteLine(reader[0].ToString(), reader[1].ToString());
                    Uri endpoint = new Uri("https://spec.pokemonion.com/listings/consignment/sold");
                    string requeststr = "{\"ticketGroupId\":\"" + tgidString + "\" ,\"soldQuantity\":" + soldString + "}";
                    statusflag = false; //false -production
                    if (!notified)
                    {
                        GetPOSTResponse(endpoint, requeststr);
                    }
                    
                    if (statusflag) {

                        //unbroadcast listing to avoid double sale if void process doesn't execute properly (or we have a customer order)
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


                        if (!tntrans)
                        {
                            // Don't void PO if invoice fails to void - creates orphaned invoice
                            if (VoidInvoice(Int32.Parse(invString)) == 0)
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
            specLogString = ConfigurationManager.ConnectionStrings["TicketTracker"].ConnectionString;
            specLogConnection = new SqlConnection(specLogString);
            specLogConnection.Open();
            logCommand = new SqlCommand();
            logCommand.Connection = specLogConnection;
            logCommand.CommandText = "Insert into status_logs([Logdate] ,[Level],[message] ,[type])   values(Getdate(),'info','Notifier Finish','success') ";
            logCommand.ExecuteNonQuery();
            specLogConnection.Close();
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
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();

                if ((response.StatusCode == HttpStatusCode.OK) || (response.StatusCode == HttpStatusCode.Accepted))
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
            catch (Exception ex)
            {
                LogEntry("Invoice Void Failed:" + ex.Message, "fail");
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
            catch (Exception ex)
            {
                LogEntry("PO not voided:" + ex.Message, "fail");
                voidCon.Close();
                return 1;
            }




        }

        public static int CreateCatListing(string oldtg)
        {
            int tgid = 0;
            // GetTGInfo

            string sqlstring = "Select * from ticket_group where ticket_group_id = " + oldtg;
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
            String sectionString = reader[10].ToString();
            String seathighString = "0"; //reader[22].ToString();
            String seatlowString = "0"; //reader[23].ToString();

            String qtyString = soldString;//reader[4].ToString();

            String localeventString = reader[14].ToString();
            String deliveryStr = reader[38].ToString();

           
             
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


            //removed 11-13 to match API update
            // exchangeEventId + ",\'" + /*eventName +*/ "\','" + eventDate + "\'," + venueId + ",\'" + venueName + "\',\'" +

            string catsqlstring = "Declare @RC int EXECUTE @RC = [dbo].[api_category_ticket_group_create] " + "\'" + sectionString + "\',\'\',\'" + rowString + "\'," + "\'\'" + "," + seatlowString + "," + seathighString + "," + qtyString + "," + "0,0,0,0,\'" + onHand + "\',\'Sale - RC\'," + "\'\'" + "," + deliveryStr +"," + "2," + localeventString;



            SqlCommand catlistCommand = new SqlCommand();
            catlistCommand.Connection = clconnection;
            catlistCommand.CommandText = catsqlstring;
            try
            {
                SqlDataReader catListingReader;
                catListingReader = catlistCommand.ExecuteReader();
                catListingReader.Read();
                string result = catListingReader[0].ToString();
                string catTgid = catListingReader[2].ToString();
                if (catTgid == "-1") //api function call failed - zoned event likely -> force it
                {
                    LogEntry("VC adding", "warn");
                    string vcInsertstr = @" INSERT dbo.venue_category (section_high , section_low, row_low , row_high, seat_low, seat_high, default_wholesale_price, default_retail_price, venue_id, default_cost, default_face_price, text_desc, show_to_sales_staff, default_ticket_group_notes, ticket_group_stock_type_id, ticket_group_type_id, venue_configuration_zone_id)
                                           VALUES ( '' , @section_low, @row_low, @row_high, '', '', 0.00, 0.00, @venue_id, 0.00, 0.00, '', 1, '', 1, 1, NULL );";

                    SqlCommand vcInsert = new SqlCommand();
                    SqlConnection vcCon = new SqlConnection(ConfigurationManager.ConnectionStrings["indux"].ConnectionString);
                    vcCon.Open();
                    vcInsert.Connection = vcCon; // clconnection;

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
                    vcCheck.CommandText = "SELECT * FROM dbo.venue_category vc	WHERE vc.venue_id = " + venueId + " AND vc.row_high = \'" + rowString + "\' AND vc.row_low = \'" + rowString + "\' AND vc.section_low = \'" + sectionString + "\'";
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

                        catTgid = directCat.ExecuteScalar().ToString();
                        if (Int32.Parse(catTgid) > 0)
                        {
                            for (int i = 1; i < Int32.Parse(qtyString) + 1; i++)
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
                        LogEntry("Cat LIsting Not Created: VC not added", "fail");
                        vcCon.Close();
                        return 0;
                    }

                }
                clconnection.Close();
                LogEntry("Cat LIsting Created: " + catTgid, "success");
                if (!tntrans) { SellCatListing(catTgid); }
                else {
                    // sell to 0 if merc
                    string mercstring = @"SELECT DISTINCT  ticket_group.ticket_group_id, ticket_group.internal_notes, ticket_group.client_broker_id, ticket_group.client_broker_employee_id, invoice.external_PO, invoice.mercury_transaction_id,invoice.ticket_request_id
                  FROM invoice INNER JOIN
                             ticket ON invoice.invoice_id = ticket.invoice_id RIGHT OUTER JOIN
                             ticket_group  ON ticket.ticket_group_id = ticket_group.ticket_group_id
                  where ticket_group.ticket_group_id = ";

                    mercstring += tgidString;
                    string mercID = "";
                    SqlConnection merccon = new SqlConnection(ConfigurationManager.ConnectionStrings["indux"].ConnectionString);
                    merccon.Open();
                    SqlCommand mercCommand = new SqlCommand();
                    mercCommand.CommandText = mercstring;
                    mercCommand.Connection = merccon;
                    SqlDataReader mercReader = mercCommand.ExecuteReader();
                    mercReader.Read();
                    if (mercReader.HasRows)
                    {
                        mercID = mercReader[5].ToString();
                    }
                    merccon.Close();
                    if (mercID.Length > 0)
                    {
                        SellCatListing(catTgid, true);
                        //we need mercid on invoicenotes
                    }
                    //it is a customer order
                    else
                    {
                        // get ticket request id before we void 
                        string trid = "";
                        string invtot = "";
                        string expense = "";
                        string ship = "";
                        SqlConnection trcon = new SqlConnection(ConfigurationManager.ConnectionStrings["indux"].ConnectionString);
                        trcon.Open();
                        string trstring = "Select ticket_request_id,invoice_total,invoice_total_expense,invoice_total_shipping_cost from invoice where invoice_id = " + invString;
                        SqlCommand trcommand = new SqlCommand();
                        trcommand.Connection = trcon;
                        trcommand.CommandText = trstring;
                        SqlDataReader trreader = trcommand.ExecuteReader();
                        trreader.Read();
                        if (trreader.HasRows)
                        {
                            trid = trreader[0].ToString();
                            invtot = trreader[1].ToString();
                            expense = trreader[2].ToString();
                            ship = trreader[3].ToString();
                        }
                        trcon.Close();
                        // we can only void and so forth if we can attach trid to new invoice
                        if (trid.Length > 0)
                        {



                            if (VoidInvoice(Int32.Parse(invString)) == 0)
                            {
                                VoidPO(Int32.Parse(poidString));
                            }
                            else
                            {
                                LogEntry("PO not voided - Invoice void failed", "fail");
                            }


                            //make sure new invoice has old ticket request id
                            string custstring = "Select client_id from client_invoice where invoice_id = " + invString;
                            SqlConnection custcon = new SqlConnection(ConfigurationManager.ConnectionStrings["indux"].ConnectionString);
                            custcon.Open();
                            SqlCommand custCommand = new SqlCommand();
                            custCommand.CommandText = custstring;
                            custCommand.Connection = custcon;
                            SqlDataReader clientReader = custCommand.ExecuteReader();
                            clientReader.Read();
                            string clientID = "";
                            if (clientReader.HasRows)
                            {
                                clientID = clientReader[0].ToString();
                            }
                            clientReader.Close();
                            //need to figure out what to do it client is not found
                            if (clientID.Length > 0)
                            {
                                SqlParameter rc = new SqlParameter();
                                rc.ParameterName = "@RC";
                                rc.Value = "";
                                custCommand.Parameters.Add(rc);

                                //  float subtotal = float.Parse(invtot) - float.Parse(expense) - float.Parse(ship);

                                custstring = "EXECUTE @RC = [dbo].[pos_AddCredit_InvoiceCredit]" + invtot + ", \'\'  ,4  ," + clientID + ",-1  ," + invString + ",-1 ";
                                custCommand.CommandText = custstring;
                                if (custCommand.ExecuteNonQuery() == 0)
                                {
                                    LogEntry("Credit Not Created for Client:" + clientID, "fail");
                                }

                                string creditid = rc.Value.ToString();
                            }
                                // we need to get this invoice number
                            SellCatListing(catTgid);
                            string invnum = catInvString;
                            custCommand.CommandText = "Update invoice set  ticket_request_id = " + trid + " where invoice_id = " + catInvString;
                            custCommand.ExecuteNonQuery();
                            if (clientID.Length > 0)
                            {
                                string paystring = "Execute [dbo].[invoice_payment_insert]  2," + invnum + ",NULL  ,17 ," + invtot + ",\'n/a\'  ,\'n/a\'  ,\'n/a\'  ,NULL  ,NULL  ,12  ,1 ,1  , \'" + DateTime.Now + "\'";

                                custCommand.CommandText = paystring;
                                custCommand.ExecuteNonQuery();

                                custstring = "update invoice_credit  set available_for_use = 0,system_user_id =12 where invoice_id =" + invnum;
                                custCommand.CommandText = custstring;
                                custCommand.ExecuteNonQuery();
                            }
                            
                            /* custCommand.CommandText = "Select invoice_id from invoice where ticket_request_id = " + trid;
                             SqlDataReader invreader = custCommand.ExecuteReader();
                             invreader.Read();
                             if (invreader.HasRows)
                             {
                                 invnum = invreader[0].ToString();
                                 invreader.Close();
                                 if(invnum.Length > 0)
                                 {
                                     string paystring = "Execute [dbo].[invoice_payment_insert]  \'" + DateTime.Now + "\',2  ," + invnum + ",NULL  ,17  ," + invtot + ",NULL  ,NULL  ,NULL  ,Null  ,NULL  ,12  ,1 ,1  , \'" + DateTime.Now + "\'";

                                     custCommand.CommandText = paystring;
                                     custCommand.ExecuteNonQuery();

                                     custstring = "update invoice_credit  set available_for_use = 0,system_user_id =12 where credit_id =" + rc.Value.ToString();
                                     custCommand.CommandText = custstring;
                                     custCommand.ExecuteNonQuery();
                                 }
                                 else
                                 {
                                     LogEntry("Cat Invoice not found - Credit not applied TRID:" + trid, "fail");
                                 }

                             }
                             else
                             {
                                 LogEntry("Cat Invoice not found - Credit not applied TRID:" + trid, "fail");
                             }
                             */



                        }
                        else
                        {
                            LogEntry("TicketRequestID not found", "fail");
                        }





                    }

                }

                return Int32.Parse(catTgid);
            }
            catch (Exception ex) {

                LogEntry("Cat LIsting Creation Failed:" + ex.Message, "fail");
                clconnection.Close();
                return 0;
            }
            //clconnection.Open();


            // note should indicate this is a sale


        }

        public static void SellCatListing(string catListStr, bool merc = false)
        {
            bool client = false;
            bool fulfillment = false;
            int shippingid = 0;
            string email = "";
            string rname = "";

            //========================old get tickets location=============================
            string connectionString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;
            SqlConnection connection = new SqlConnection(connectionString);
            //===============================================================================





            string orderString = "Select [external_po_number],[client_broker_id],[shipping_tracking_id] from dbo.purchase_Order where purchase_order_id = " + poidString;
            SqlCommand ordCommand = new SqlCommand();
            ordCommand.Connection = connection;
            ordCommand.CommandText = orderString;
            connection.Open();
            SqlDataReader ordreader = ordCommand.ExecuteReader();
            string addressid = "";
            string extpo = "";




            if (ordreader.HasRows)
            {
                ordreader.Read();
                extpo = ordreader[0].ToString();
                shippingid = Int32.Parse(ordreader[2].ToString());
                //get email address for fufillment orders - needs to be put into shipping_tracking_Address of cat
                ordreader.Close();



            }
            connection.Close();


            string cbinvoice = "SELECT [invoice_id],[client_broker_id],[client_broker_employee_id] FROM [dbo].[client_broker_invoice]  where invoice_id =" + invString;
            ordCommand.CommandText = cbinvoice;
            connection.Open();
            ordreader = ordCommand.ExecuteReader();


            //  ordreader.Open
            if (ordreader.HasRows)
            {
                ordreader.Read();

                //get broker address 
                brokerNum = Int32.Parse(ordreader[1].ToString());
                if (brokerNum == 471) { fulfillment = true; }
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


                brokerCon.Close();
            }
            else //might be customer order - look for client_address
            {
                ordreader.Close();
                cbinvoice = "SELECT [invoice_id],[client_id] FROM [dbo].[client_invoice]  where invoice_id =" + invString;
                ordCommand.CommandText = cbinvoice;
                // connection.Open();
                ordreader = ordCommand.ExecuteReader();
                //get client address
                if (ordreader.HasRows)
                {
                    ordreader.Read();

                    brokerNum = Int32.Parse(ordreader[1].ToString());
                    string brokerString = "Select address_id from dbo.client_address where client_id = " + brokerNum;
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
                        client = true;

                    }


                    brokerCon.Close();
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
            string invstr = "";
            string expense = "0";
            string ship = "0";
            string tax = "0";
            if ((client) || (fulfillment))  // put brokernum in param client_id rather than client_broker_id
            {

                //get expenses from consign invoice to place in cat invoice
                string invSQL = "Select  [invoice_total_expense],[invoice_total_shipping_cost],[invoice_total_taxes],[shipping_tracking_id] from dbo.invoice where invoice_id = " + invString;
                SqlCommand invCommand = new SqlCommand();
                SqlConnection invCon = new SqlConnection(connectionString);
                invCommand.Connection = invCon;
                invCommand.CommandText = invSQL;
                invCon.Open();
                SqlDataReader invReader = invCommand.ExecuteReader();

                invReader.Read();
                expense = invReader[0].ToString();
                ship = invReader[1].ToString();
                tax = invReader[2].ToString();
                shippingid = Int32.Parse(invReader[3].ToString());
                invReader.Close();

                String sta = $"Select [recipient_email],[recipient_name] from shipping_tracking_address where shipping_tracking_id = {shippingid}";
                SqlCommand StaCommand = new SqlCommand();
                StaCommand.Connection = invCon;
                StaCommand.CommandText = sta;
                SqlDataReader staReader = StaCommand.ExecuteReader();
                if (staReader.HasRows)
                {
                    staReader.Read();
                    email = staReader[0].ToString();
                    rname = staReader[1].ToString();

                }

                if (client)
                {
                    invstr = "execute [dbo].[api_invoice_create] " + "\'\'" + ",NULL,22,null,4,null," + brokerNum + ",null," + addressid + "," + expense + "," + ship + "," + tax + ",4," + "\'" + invNotes + "\',\'\',\'" + extpoString + "\',5,1,1,\'\',@realtix,@tix,1,0,0,0";
                }
                if (fulfillment)
                {
                    invstr = "execute [dbo].[api_invoice_create] " + "\'\'" + ",NULL,22,null,4,null,null," + brokerNum + "," + addressid + ",0,0,0,4," + "\'" + invNotes + "\',\'\',\'" + extpoString + "\',5,1,1,\'\',@realtix,@tix,1,0,0,0";
                    //need to substitute email on new shipping_tracking_address

                }
            }
            else
            {
                invstr = "execute [dbo].[api_invoice_create] " + "\'\'" + ",NULL,22,null,4,null,null," + brokerNum + "," + addressid + ",0,0,0,4," + "\'" + invNotes + "\',\'\',\'" + extpoString + "\',5,1,1,\'\',@realtix,@tix,1,0,0,0";
            }


            //new place for getting tickets=======================================================
            string getTixStr = "";
            if (merc)
            {
                getTixStr = "Select [category_ticket_id],0,0,0 from Category_ticket where category_ticket_group_id = " + catListStr;
            }

            else
            {
                string ticketprice = priceString;

                if ((client) || (fulfillment))
                {
                    // remove expenses from the unit ticket price so we can add them back on the invoice
                    int qtytix = Int32.Parse(soldString);
                    float tixprice = (float.Parse(priceString)) - (float.Parse(expense) / qtytix) - (float.Parse(ship) / qtytix) - (float.Parse(tax) / qtytix);
                    ticketprice = tixprice.ToString();

                }

                getTixStr = "Select [category_ticket_id],0,0," + ticketprice + " from Category_ticket where category_ticket_group_id = " + catListStr;
            }
            string tixconnectionString = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;
            SqlConnection tixconnection = new SqlConnection(tixconnectionString);
            SqlCommand Command = new SqlCommand();
            Command.Connection = tixconnection;
            Command.CommandText = getTixStr;
            SqlDataAdapter da = new SqlDataAdapter(Command);
            DataTable tix = new DataTable();
            tixconnection.Open();
            da.Fill(tix);



            //========================================================================
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

            string i = "";
            //new section - we need inv # returned to customer order so we can disburse credit
            if ((client) || (fulfillment))
            {
                string retval;
                SqlDataReader invReader = invInsert.ExecuteReader();
                invReader.Read();
                if (invReader.HasRows)
                {
                    // string test = invReader[0].ToString();
                    invReader.NextResult(); //first result set is status and message - second is created invoice info
                    invReader.Read();
                    catInvString = invReader[0].ToString();
                }

                connection.Close();
                string expensestr = "Insert Into  [dbo].[invoice_expenses] SELECT  [expense_amt],[expense_desc] ,[expense_type_id],[account_id] ," + catInvString + ",[notes]  FROM [dbo].[invoice_expenses] where invoice_id =" + invString;
                SqlCommand expInsert = new SqlCommand();
                expInsert.Connection = connection;
                connection.Open();
                expInsert.CommandText = expensestr;
                expInsert.ExecuteScalar();

                float total = float.Parse(priceString) * float.Parse(soldString);
                SqlCommand updInvoice = new SqlCommand();
                updInvoice.Connection = connection;
                updInvoice.CommandText = "Update invoice set invoice_total = " + total.ToString() + "where invoice_id = " + catInvString;
                updInvoice.ExecuteNonQuery();
                connection.Close();
                if (fulfillment)
                {   //need to replace email address for the new invoice 
                    String stid = "";
                    connection.Open();
                    SqlCommand GetSta = new SqlCommand($"Select shipping_tracking_id from invoice where invoice_id = {catInvString}");
                    GetSta.Connection = connection;
                    SqlDataReader staReader = GetSta.ExecuteReader();
                    if (staReader.HasRows)
                    {
                        staReader.Read();
                        stid = staReader[0].ToString();
                    }
                    staReader.Close();
                    SqlCommand updSta = new SqlCommand();
                    updSta.Connection = connection;
                    updSta.CommandText = $"update shipping_tracking_address set recipient_email = '{email}',recipient_name='{rname}' where shipping_tracking_id = {stid}";
                    updSta.ExecuteNonQuery();

                }
            }
            else
            {
                i = invInsert.ExecuteScalar().ToString();
            }
            tixconnection.Close();
            connection.Close();
            if (i.Length > 0)
            {
                if (i == "1") { LogEntry("Cat Sold ", "success"); }
                else { LogEntry("Cat Invoice creation Failed ", "fail"); }
            }
            else { LogEntry("Cat Sold ", "success"); }
            // return retval;
            //return false; 
        }


        public static Boolean checkTransactionType(int TGID)
        {
            //=====================update for Fulfillment================================
            string connectionString1 = ConfigurationManager.ConnectionStrings["indux"].ConnectionString;

            SqlConnection connection1 = new SqlConnection(connectionString1);
            SqlCommand Command1 = new SqlCommand();
            Command1.Connection = connection1;
          
            string sqlstr1 = "SELECT [invoice_id],[client_broker_id],[client_broker_employee_id] FROM [dbo].[client_broker_invoice]  where invoice_id =" + invString;
            //sqlstr1 = sqlstr1 + TGID.ToString();
            Command1.CommandText = sqlstr1;
            connection1.Open();
            SqlDataReader Reader1 = Command1.ExecuteReader();
            // string venueName = Reader[0].ToString();

            if (Reader1.HasRows)
            {
                Reader1.Read();
                string brokerstr = Reader1[1].ToString();
                if ( Reader1[1].ToString() == "471"){ //471 is TND for fulfillment


                    connection1.Close();

                    // new sql returns tnet trans types
                    return true;
                }
                else
                {
                    connection1.Close();
                }

            }



            //==============================================================================



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
              WHERE        ((invoice.mercury_transaction_id is not null) or   (invoice.external_PO  like '0') or  (invoice.external_PO  like 'n/a%') or (invoice.external_PO is null) or (invoice.external_PO  like '') ) and ticket_group.ticket_group_id =";

            sqlstr = sqlstr + TGID.ToString();
            Command.CommandText = sqlstr;
            connection.Open();
            SqlDataReader Reader = Command.ExecuteReader();
            // string venueName = Reader[0].ToString();

            if (Reader.HasRows)
            {
                
                connection.Close();          

                // new sql returns tnet trans types
                return true;
            }
            else
            {

                connection.Close();
                // return true;
                return false;
            }


        }
        public static void LogEntry(string message, string status)
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
            float price = (float.Parse(specReader[15].ToString()) / qty);
            priceString = price.ToString();
            extpoString = specReader[1].ToString();
            //we are using getConsigncost process to set broker so we don't need an extra lookup here
            //string clientBroker = ""; // specReader[2].ToString();

            /*
            string specRecord = @"INSERT INTO [dbo].[SpecSales]
                                            ([Ticket_group_id],[invoice_id],[purchase_order_id],[Ordernum],[ExternalPO],[EventName],[EventDate],[VenueName],[State],[City],[Quantity],[Section],[Row],[SalePrice],[OrderTotal],[SaleDate])
                                            VALUES
                                            (" + tgidString + "," + invString + "," + poidString + ",\'" +specReader[0].ToString() +  "\',\'" +  specReader[1].ToString() + "\',\'" + eventString + "\',\'" + specReader[4].ToString() + "\',\'" + venueString + "\',\'" + "\',\'\',\'"  + soldString +"\',\'" + specReader[10].ToString() + "\',\'" + specReader[9].ToString() + "\',\'" + specReader[8].ToString() + "\',\'" + specReader[15].ToString() + "\',\'" + specReader[17].ToString() + "\')";


    */
            //get client broker

            string specRecord = @"INSERT INTO [dbo].[SpecSales]
                                            ([Ticket_group_id],[invoice_id],[purchase_order_id],[Ordernum],[ExternalPO],[EventName],[EventDate],[VenueName],[State],[City],[Quantity],[Section],[Row],[SalePrice],[OrderTotal],[SaleDate],[filled],[shipped],[assigned],[soldto])
                                            VALUES
                                            (" + tgidString + "," + invString + "," + poidString + ",\'" + specReader[0].ToString() + "\',\'" + specReader[1].ToString() + "\',\'" + eventString + "\',\'" + specReader[4].ToString() + "\',\'" + venueString + "\',\'" + "\',\'\',\'" + soldString + "\',\'" + specReader[10].ToString() + "\',\'" + specReader[9].ToString() + "\',\'" + specReader[8].ToString() + "\',\'" + specReader[15].ToString() + "\',\'" + specReader[17].ToString() + "\'" + ",null,null,null,null" + ")";




            SqlCommand insSpecRecord = new SqlCommand();
            insSpecRecord.Connection = specSaleConnection;
            insSpecRecord.CommandText = specRecord;
            insSpecRecord.ExecuteNonQuery();
            
            // send mail before speccon closes - we use specreader data  
            /* disable mail send to start - needs retest
            MailMessage mail = new MailMessage("jct@jct-tech.com", "frtspecsales@gmail.com");
            SmtpClient client = new SmtpClient();
            client.Port = 587;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Host = "smtp.authsmtp.com";
            mail.Subject = "Incoming  Sale";*/
          //  mail.Body = eventString + ":Event\n " + specReader[4].ToString() + ":Eventdate\n" + venueString + ":Venue\n " + soldString + ":Qty\n " + specReader[10].ToString() /*section*/ + ":Section\n " + specReader[9].ToString() /*row*/ + ":Row\n " + specReader[17].ToString() /*SaleDate*/;
           // client.Credentials = new NetworkCredential("ac77574", "tic-lenny-room-pq");

           // client.EnableSsl = true; - keep this commented was not in use

            /*
            try
            {
                client.Send(mail);
            }
            catch
            {
                LogEntry("Mail failed", "fail");
            }

*/
            specSaleConnection.Close();
            srconnection.Close();
            GetPriceMultiplier(tgidString);



        }

        public static void GetPriceMultiplier(String TGID)
        {


            string ticketGroup = TGID;

            string connectionString = ConfigurationManager.ConnectionStrings["TicketTracker"].ConnectionString;
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandText = "Select * from SpecSales  where ticket_group_id = " + ticketGroup;



            connection.Open();
            SqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Read();
                //for each record in spec sales - set price multiplier
                ticketGroup = reader[0].ToString();
                RestClient client = new RestClient("https://spec.pokemonion.com/listings/consignment/tgid/");
                RestRequest request = new RestRequest(ticketGroup);
                request.AddHeader("Authorization", "Basic c3BlYzptcDJ3ODRuUU05VlNOa1BK");
                var response = client.Execute<specListing>(request);
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    specListing listing = response.Data;
                    SqlCommand command2 = new SqlCommand();
                    command2.CommandText = "Update SpecSales Set priceMultiplier = " + listing.priceMultiplier.ToString() + " where ticket_group_id = " + ticketGroup;
                    SqlConnection connection2 = new SqlConnection(connectionString);
                    command2.Connection = connection2;
                    connection2.Open();
                    command2.ExecuteNonQuery();
                    command2.CommandText = "Update SpecSales Set exchange = \'" + listing.exchange.ToString() + "\' where ticket_group_id = " + ticketGroup;
                    command2.Connection = connection2;
                    command2.ExecuteNonQuery();
                    connection2.Close();
                }



            }
            connection.Close();
        }



        
    
    }


}

