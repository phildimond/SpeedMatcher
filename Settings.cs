using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeedMatcher
{
    public class Settings
    {
        public string SprogPort { get; set; } = string.Empty;

        public static Settings? LoadSettings() 
        {
            string fn = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyMR/SpeedMatcher/Settings.json"); 
            string s = File.ReadAllText(fn);
            return JsonConvert.DeserializeObject<Settings>(s);
        }

        public static void SaveSettings(Settings? settings)
        {
            if (settings == null) { throw new Exception("Can't save a null settings object."); }

            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Include;
            string fn = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyMR/SpeedMatcher/Settings.json");
            string s = JsonConvert.SerializeObject(settings, Formatting.Indented);
            if (!Directory.Exists(Path.GetDirectoryName(fn))) { Directory.CreateDirectory(Path.GetDirectoryName(fn)); };
            File.WriteAllText(fn, s);
        }

    }
}
