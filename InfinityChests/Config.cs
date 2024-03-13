using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;

namespace InfinityChests
{
    public class Config
    {
        public List<string> regions = new();
        public List<string> chestnames = new();

        public void Write()
        {
            string path = @"tshock\InfinityChests.json";
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        public static Config Read()
        {
            string path = @"tshock\InfinityChests.json";
            Config config = new();
            if (!File.Exists(path))
            {
                config.Write();
                return config;
            }
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            config.Write();
            return config; 
        }
    }
}
