SELECT DISTINCT invoice.mercury_transaction_id,invoice.external_PO,invoice.invoice_id ,ticket_group.client_broker_id,event.event_name, event.event_datetime, venue.name, ticket_group.ticket_group_id,  ticket_group.cost, 
                         ticket_group.wholesale_price, ticket_group.row, ticket_group.section, ticket_group.internal_notes, ticket_group.notes, ticket_group.last_wholesale_price,  invoice.invoice_balance_due, invoice.invoice_total, ticket_group.actual_purchase_date,invoice.sent_in_update_datetime
              FROM invoice INNER JOIN
                         ticket ON invoice.invoice_id = ticket.invoice_id RIGHT OUTER JOIN
                         ticket_group INNER JOIN
                         event ON ticket_group.event_id = event.event_id INNER JOIN
                         venue ON event.venue_id = venue.venue_id ON ticket.ticket_group_id = ticket_group.ticket_group_id
			
              where      ticket_group.ticket_group_id =123468