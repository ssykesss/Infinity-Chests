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
            Config config = new Config();
            if (!File.Exists(path))
            {
                config.Write(path);
                return config;
            }
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            return config; 
        }
    }
}
