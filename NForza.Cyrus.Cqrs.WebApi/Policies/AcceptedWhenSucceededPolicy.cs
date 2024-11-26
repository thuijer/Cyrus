﻿using Microsoft.AspNetCore.Http;

namespace NForza.Cyrus.Cqrs.WebApi.Policies;

public class AcceptedWhenSucceededPolicy : CommandResultPolicy
{
    public override IResult? FromCommandResult(CommandResult result)
    {
        if (result.Succeeded)
        {
            return Results.Accepted();
        }
        return null;
    }
}
