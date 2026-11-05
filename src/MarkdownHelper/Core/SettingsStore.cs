using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace MarkdownHelper.Core
{
    /// <summary>Loads and saves settings as JSON. A missing or damaged file gives the defaults.</summary>
    public sealed class SettingsStore
    {
        private readonly string path;

        public SettingsStore(string path)
        {
            this.path = path;
        }

        public static string DefaultPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "MarkdownHelper", "settings.json");
        }

        public AppSettings Load()
        {
            AppSettings settings;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    settings = (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(stream);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is SerializationException || e is XmlException)
            {
                settings = new AppSettings();
            }

            settings = settings ?? new AppSettings();
            settings.Normalize();
            return settings;
        }

        public void Save(AppSettings settings)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            string temp = path + ".tmp";
            using (var stream = File.Create(temp))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, ownsStream: false, indent: true))
            {
                new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(writer, settings);
            }

            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
