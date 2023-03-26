﻿using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace ResumableFunctions.Core.Helpers;

public class ObjectToJsonConverter : ValueConverter<object, string>
{
    public ObjectToJsonConverter() : base(o => ObjectToJson(o), json => JsonToObject(json))
    {
    }

    private static object JsonToObject(string json)
    {
        return JsonConvert.DeserializeObject(json);
    }

    private static string ObjectToJson(object obj)
    {
        return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }
}