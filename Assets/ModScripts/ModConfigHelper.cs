using System;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DiscordPlays
{
    public static class ModConfigHelper
    {
        private static readonly string ModSettingsPath;
        
        [NotNull]
        public static T ReadConfig<T>([NotNull] string fileName) where T : new()
        {
            lock (ModSettingsPath)
            {
                if (String.IsNullOrEmpty(fileName))
                    throw new ArgumentNullException("fileName");
                if (!fileName.EndsWith(".json"))
                    fileName += ".json";
                string settingsPath = Path.Combine(ModSettingsPath, fileName);
                T settings;
                try
                {
                    settings = JsonConvert.DeserializeObject<T>(File.ReadAllText(settingsPath));
                }
                catch (FileNotFoundException)
                {
                    settings = new T();
                    File.WriteAllText(settingsPath,
                        JsonConvert.SerializeObject(settings, Formatting.Indented, new StringEnumConverter()));
                }
                if (settings == null)
                    settings = new T();
                return settings;
            }
        }

        static ModConfigHelper()
        {
            ModSettingsPath = Path.Combine(Application.persistentDataPath, "Modsettings");
        }
    }
}