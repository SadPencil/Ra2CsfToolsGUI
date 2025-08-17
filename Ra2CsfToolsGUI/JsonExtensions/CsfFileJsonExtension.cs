using SadPencil.Ra2CsfFile;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ra2CsfToolsGUI.JsonExtensions
{
    public static class CsfFileJsonExtension
    {
        private class JsonCsfValue
        {
            [JsonInclude]
            public string Value { get; set; }

            [JsonInclude]
            public string Extra { get; set; } = null;
        }

        private static JsonSerializerOptions GetJsonSerializerOptions() => new()
        {
            WriteIndented = true, // 2 spaces
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Serializes an object to a JSON string with a 4-space indent.
        /// This is the recommended approach for custom indent sizes, as JsonSerializerOptions
        /// does not directly support changing the indent size.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="value">The object to serialize.</param>
        /// <returns>A JSON string with a 4-space indent.</returns>
        private static void SerializeWithFourSpaceIndent<T>(Stream stream, T value)
        {
            // Create JsonWriterOptions with the desired indent size.
            var writerOptions = new JsonWriterOptions
            {
                Indented = true,
                IndentSize = 4, // Set indentation to 4 spaces
                SkipValidation = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            // Create a Utf8JsonWriter with the custom options.
            using (var writer = new Utf8JsonWriter(stream, writerOptions))
            {
                // Create a JsonSerializerOptions instance for serialization.
                var serializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                // Serialize the object using the custom writer.
                JsonSerializer.Serialize(writer, value, serializerOptions);
            }
        }

        public static void WriteJsonFile(this CsfFile csf, Stream stream)
        {
            if (csf == null)
            {
                throw new ArgumentNullException(nameof(csf));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var jsonCsfLabels = new Dictionary<string, JsonCsfValue>(StringComparer.InvariantCultureIgnoreCase);

            foreach (string label in csf.Labels.Keys)
            {
                string value = csf.Labels[label];

                jsonCsfLabels.Add(label, new JsonCsfValue { Value = value });
            }

            SerializeWithFourSpaceIndent(stream, jsonCsfLabels);
        }

        public static CsfFile LoadFromJsonFile(Stream stream, CsfFileOptions options)
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

            var jsonCsfLabels = JsonSerializer.Deserialize<Dictionary<string, JsonCsfValue>>(stream, GetJsonSerializerOptions());

            foreach (string label in jsonCsfLabels.Keys)
            {
                if (!CsfFile.ValidateLabelName(label))
                {
                    throw new Exception("Invalid characters found in label name \"" + label + "\".");
                }

                string value = jsonCsfLabels[label].Value;

                if (value == null)
                {
                    throw new Exception($"The value of label \"{value}\" should not be null.");
                }

                _ = csfFile.AddLabel(label, jsonCsfLabels[label].Value);
            }

            return csfFile;
        }
    }
}
