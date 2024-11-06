using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;

namespace InfinityChests
{
    public class Config
    {
        private static readonly string path = @"tshock\InfinityChests.json";

        public List<string> regions = new();
        public List<string> chestnames = new();

        public void Write()
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        public static Config Read()
        {
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
