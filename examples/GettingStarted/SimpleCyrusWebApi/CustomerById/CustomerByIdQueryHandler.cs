﻿using Microsoft.EntityFrameworkCore;
using SimpleCyrusWebApi.Storage;

namespace SimpleCyrusWebApi;

// Handles requests for getting a customer by Id
public class CustomerByIdQueryHandler(DemoContext context)
{
    public async Task<Customer?> Query(CustomerByIdQuery query)
        => await context.Customers.FirstOrDefaultAsync(c => c.Id == query.Id);
}