using SadPencil.Ra2CsfFile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;

namespace Ra2CsfToolsGUI.YamlExtensions
{
    public static class CsfFileExtension
    {
        private const int YAML_VERSION = 1;
        private const string YAML_TYPE_NAME = "SadPencil.Ra2CsfFile.Yaml";

        public static void WriteYamlFile(this CsfFile csf, Stream stream)
        {
            if (csf == null)
            {
                throw new ArgumentNullException(nameof(csf));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var yamlRawSections = new Dictionary<string, string>
            {
                { "SadPencil.Ra2CsfFile.Yaml:YamlVersion", YAML_VERSION.ToString(CultureInfo.InvariantCulture) },
                { "SadPencil.Ra2CsfFile.Yaml:CsfVersion", csf.Version.ToString(CultureInfo.InvariantCulture) },
                { "SadPencil.Ra2CsfFile.Yaml:CsfLang", ((int)csf.Language).ToString(CultureInfo.InvariantCulture) }
            };

            foreach (var label in csf.Labels)
            {
                string key = label.Key;
                string value = label.Value;
                if (!CsfFile.ValidateLabelName(key))
                {
                    throw new Exception("Invalid characters found in label name \"" + key + "\".");
                }
                yamlRawSections.Add(key, value);
            }

            var serializer = new SerializerBuilder()
                .WithDefaultScalarStyle(YamlDotNet.Core.ScalarStyle.Literal)
                .Build();
            string yaml = serializer.Serialize(yamlRawSections);

            using (var streamWriter = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                streamWriter.Write(yaml);
            }
        }

        public static CsfFile LoadFromYamlFile(Stream stream, CsfFileOptions options)
        {

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var csfFile = new CsfFile(options);
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();

            using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            string yaml = reader.ReadToEnd();
            var yamlRawSections = deserializer.Deserialize<Dictionary<string, string>>(yaml);

            if (!yamlRawSections.ContainsKey("SadPencil.Ra2CsfFile.Yaml:CsfVersion"))
            {
                throw new Exception("Invalid SadPencil.Ra2CsfFile.Yaml file. Missing key \"CsfVersion\" in SadPencil.Ra2CsfFile.Yaml:CsfVersion.");
            }

            string csfVersionValue = yamlRawSections["SadPencil.Ra2CsfFile.Yaml:CsfVersion"];
            csfFile.Version = Convert.ToInt32(csfVersionValue, CultureInfo.InvariantCulture);

            if (!yamlRawSections.ContainsKey("SadPencil.Ra2CsfFile.Yaml:YamlVersion"))
            {
                throw new Exception("Invalid SadPencil.Ra2CsfFile.Yaml file. Missing key \"YamlVersion\" in SadPencil.Ra2CsfFile.Yaml:YamlVersion.");
            }

            string yamlVersionValue = yamlRawSections["SadPencil.Ra2CsfFile.Yaml:YamlVersion"];
            if (Convert.ToInt32(yamlVersionValue, CultureInfo.InvariantCulture) != YAML_VERSION)
            {
                throw new Exception($"Unknown {YAML_TYPE_NAME} file version. The version should be {YAML_VERSION}. Is this a {YAML_TYPE_NAME} file from future?");
            }

            if (!yamlRawSections.ContainsKey("SadPencil.Ra2CsfFile.Yaml:CsfLang"))
            {
                throw new Exception("Invalid SadPencil.Ra2CsfFile.Yaml file. Missing key \"CsfLang\" in SadPencil.Ra2CsfFile.Yaml:CsfLang.");
            }

            string csfLangValue = yamlRawSections["SadPencil.Ra2CsfFile.Yaml:CsfLang"];
            csfFile.Language = CsfLangHelper.GetCsfLang(Convert.ToInt32(csfLangValue, CultureInfo.InvariantCulture));
            Dictionary<string, string> csfKeyValueDictionary = [];

            foreach (var section in yamlRawSections)
            {
                if (section.Key.StartsWith("SadPencil.Ra2CsfFile.Yaml"))
                {
                    continue;
                }

                csfKeyValueDictionary.Add(section.Key, section.Value);
            }

            foreach (var csfItem in csfKeyValueDictionary)
            {
                string csfLabel = csfItem.Key;
                if (!CsfFile.ValidateLabelName(csfLabel))
                {
                    throw new Exception("Invalid characters found in label name \"" + csfLabel + "\".");
                }

                csfLabel = CsfFile.LowercaseLabelName(csfLabel);
                _ = csfFile.AddLabel(csfLabel, csfItem.Value);

            }
            return csfFile;
        }
    }
}
