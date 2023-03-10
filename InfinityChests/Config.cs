using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;

namespace InfinityChests
{
    public class Config
    {
        public List<string> regions = new();
        public List<string> chestnames = new();

        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        public static Config Read(string path)
        {
            if (!File.Exists(path))
            {
                Config config = new Config();
                
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
                return config;
            }
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
        }
    }
}
