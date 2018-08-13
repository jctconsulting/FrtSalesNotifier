INSERT category_ticket_group(event_id, venue_category_id, ticket_count, wholesale_price, retail_price, notes, expected_arrival_date, face_price, cost, internal_notes, tax_exempt, update_datetime, broadcast, create_date, office_id
		   , ticket_group_code_id, unbroadcast_days_before_event, shipping_method_special_id, show_near_term_option_id, price_update_datetime, tg_note_grandfathered, auto_process_web_requests, venue_configuration_zone_id, max_showing)
	OUTPUT INSERTED.category_ticket_group_id
	
	VALUES ( @local_event_id, @venue_category_id, @quantity, @wholesale_price, @retail_price , @external_tg_notes, @expected_arrival_date, @face_price, @ticket_cost , @internal_tg_notes, 0, CURRENT_TIMESTAMP, 1, CURRENT_TIMESTAMP
		    , 1, NULL , 0, @shipping_method_special_id, @near_term_display_option_id, NULL, 0, 1, NULL, NULL)
			
			