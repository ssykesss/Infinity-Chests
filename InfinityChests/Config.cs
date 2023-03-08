using System;
using TShockAPI.Configuration;
using Newtonsoft;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace InfinityChests
{
    public class Config
    {
        //public string ChestRegion;
        public List<string> regions = new();

        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        public static Config Read(string path)
        {
            if (!File.Exists(path))
            {
                Config config = new Config();

                config.regions.Add("Chests");
                config.regions.Add("Items");
                config.regions.Add("Warp_items");

                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
                return config;
            }
            return File.Exists(path) ? JsonConvert.DeserializeObject<Config>(File.ReadAllText(path)) : new();
        }
    }
}
