using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UndetectedChromeDriver
{
    public static class Json
    {
        public static Dictionary<string, object> DeserializeData(string data)
        {
            return DeserializeData(JsonConvert.DeserializeObject<Dictionary<string, object>>(data)) as Dictionary<string, object>;
        }

        private static IDictionary<string, object> DeserializeData(JObject data)
        {
            return DeserializeData(data.ToObject<Dictionary<string, object>>());
        }

        private static IDictionary<string, object> DeserializeData(IDictionary<string, object> data)
        {
            foreach (string key in data.Keys.ToArray())
            {
                object value = data[key];

                if (value is JObject)
                {
                    data[key] = DeserializeData(value as JObject);
                }

                if (value is JArray)
                {
                    data[key] = DeserializeData(value as JArray);
                }
            }

            return data;
        }

        private static IList<object> DeserializeData(JArray data)
        {
            List<object> list = data.ToObject<List<object>>();

            for (int i = 0; i < list.Count; i++)
            {
                object value = list[i];

                if (value is JObject)
                {
                    list[i] = DeserializeData(value as JObject);
                }

                if (value is JArray)
                {
                    list[i] = DeserializeData(value as JArray);
                }
            }

            return list;
        }
    }
}