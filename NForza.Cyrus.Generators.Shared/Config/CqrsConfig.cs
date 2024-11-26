﻿using System.Collections.Generic;
using System.Linq;
using NForza.Generators;

namespace NForza.Cyrus.Cqrs.Generator.Config;

public class CqrsConfig : IYamlConfig<CqrsConfig>
{
    public CqrsConfig InitFrom(Dictionary<string, List<string>> config)
    {
        var contracts = config["contracts"];
        if (contracts != null)
        {
            Contracts = [.. contracts];
        }
        var suffix = config["suffix"];
        if (suffix != null)
        {
            Commands.Suffix = suffix.First();
        }
        var handlerName = config["handlerName"];
        if (handlerName != null)
        {
            Commands.HandlerName = handlerName.First();
        }

        var eventBus = config["eventBus"];
        if (eventBus != null)
        {
            EventBus = eventBus.First();
        }
        return this;
    }

    public string[] Contracts { get; set; } = ["Contracts"];
    public CommandConfig Commands { get; set; } = new();
    public CommandConfig Queries { get; set; } = new() { HandlerName = "Query", Suffix = "Query" };
    public string EventBus { get; set; } = "Local";
}