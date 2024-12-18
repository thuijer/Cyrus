﻿using Microsoft.AspNetCore.Http;

namespace NForza.Cyrus.WebApi.Policies;

internal class QueryResultFuncPolicy(Func<object?, IResult> resultFunc) : QueryResultPolicy
{
    public override IResult? CreateResultFromQueryResult(object? result)
        => resultFunc(result);
}