﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SaleNotifier {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class SQLStrings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal SQLStrings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SaleNotifier.SQLStrings", typeof(SQLStrings).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT category_ticket_group(event_id, venue_category_id, ticket_count, wholesale_price, retail_price, notes, expected_arrival_date, face_price, cost, internal_notes, tax_exempt, update_datetime, broadcast, create_date, office_id
        ///		   , ticket_group_code_id, unbroadcast_days_before_event, shipping_method_special_id, show_near_term_option_id, price_update_datetime, tg_note_grandfathered, auto_process_web_requests, venue_configuration_zone_id, max_showing)
        ///	OUTPUT INSERTED.category_ticket_group_id
        ///	
        ///	VALU [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string insertCat {
            get {
                return ResourceManager.GetString("insertCat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT dbo.venue_category	  (section_high
        ///				   , section_low
        ///				   , row_low
        ///				   , row_high
        ///				   , seat_low
        ///				   , seat_high
        ///				   , default_wholesale_price
        ///				   , default_retail_price
        ///				   , venue_id
        ///				   , default_cost
        ///				   , default_face_price
        ///				   , text_desc
        ///				   , show_to_sales_staff
        ///				   , default_ticket_group_notes
        ///				   , ticket_group_stock_type_id
        ///				   , ticket_group_type_id
        ///				   , venue_configuration_zone_id)
        ///			
        ///			VALUES ( &apos;&apos;
        ///				    , @section_l [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string venueconfigadd {
            get {
                return ResourceManager.GetString("venueconfigadd", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT * FROM dbo.venue_category vc	WHERE vc.venue_id = @venue_id AND vc.row_high = @row_high AND vc.row_low = @row_low AND vc.section_low = @section_low.
        /// </summary>
        internal static string venueconfiglookup {
            get {
                return ResourceManager.GetString("venueconfiglookup", resourceCulture);
            }
        }
    }
}