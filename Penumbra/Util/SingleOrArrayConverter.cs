using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SingleOrArrayConverter< T > : JsonConverter
{
    public override bool CanConvert( Type objectType ) => objectType == typeof( HashSet< T > );

    public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
    {
        var token = JToken.Load( reader );
        return token.Type == JTokenType.Array
            ? token.ToObject< HashSet< T > >()
            : new HashSet< T > { token.ToObject< T >() };
    }

    public override bool CanWrite => true;

    public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
    {
        var v = ( HashSet< T > )value;
        writer.WriteStartArray();
        foreach( var val in v )
        {
            serializer.Serialize( writer, val.ToString() );
        }

        writer.WriteEndArray();
    }
}