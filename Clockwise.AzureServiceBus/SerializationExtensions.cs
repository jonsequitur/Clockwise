using Newtonsoft.Json;

namespace Clockwise.AzureServiceBus
{
    internal static class SerializationExtensions
    {
        public static string ToJson<T>(this T obj, Formatting formatting = Formatting.None) =>
            JsonConvert.SerializeObject(obj, formatting);

        public static T FromJsonTo<T>(this string json) =>
            JsonConvert.DeserializeObject<T>(json);
    }
}