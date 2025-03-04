using System;
using System.IO;
using System.Collections.Generic;
using System.Linq; // Wichtig für LINQ
using Newtonsoft.Json;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JsonToYamlConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            // JSON-Datei einlesen
            string jsonInput = File.ReadAllText("StiebelEltron.json");
            var data = JsonConvert.DeserializeObject<List<Item>>(jsonInput);

            var yamlList = new List<YamlItem>();

            foreach (var item in data)
            {
                var yamlItem = new YamlItem
                {
                    name = item.Name.DE,
                    unique_id = GenerateUniqueId(item.Name.DE),
                    state_topic = "heating/"+item.MqttTopic.TrimStart('/'),
                    unit_of_measurement = item.Unit,
                    qos = 0,
                    value_template = "{{ value | float | round(2) }}"
                };
                yamlList.Add(yamlItem);
            }

            // YAML serialisieren mit benutzerdefiniertem Konverter
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeInspector(inner => new QuoteStringTypeInspector(inner))
                .Build();

            var yaml = serializer.Serialize(yamlList);

            // YAML-Datei speichern
            File.WriteAllText("output.yaml", yaml);

            Console.WriteLine("YAML-Datei wurde erfolgreich erstellt.");
        }

        // Methode zur Generierung der unique_id
        static string GenerateUniqueId(string name)
        {
            var id = name.ToLower()
                .Replace(" ", "_")
                .Replace("ß", "ss")
                .Replace("ä", "ae")
                .Replace("ö", "oe")
                .Replace("ü", "ue")
                .Replace(":", "")
                .Replace(".", "")
                .Replace(",", "");

            return id;
        }
    }

    // Klassen zur Abbildung der JSON-Struktur
    public class Item
    {
        public string Index { get; set; }
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public string Unit { get; set; }
        public string Converter { get; set; }
        public Name Name { get; set; }
        public Description Description { get; set; }
        public Values Values { get; set; }
        public string MqttTopic { get; set; }
        public string Default { get; set; }
        public object MinValue { get; set; }
        public object MaxValue { get; set; }
        public bool ReadOnly { get; set; }
        public bool IgnorePolling { get; set; }
    }

    public class Name
    {
        public string EN { get; set; }
        public string DE { get; set; }
    }

    public class Description
    {
        public string EN { get; set; }
        public string DE { get; set; }
    }

    public class Values
    {
        public Dictionary<string, string> EN { get; set; }
        public Dictionary<string, string> DE { get; set; }
    }

    // Klasse zur Abbildung der gewünschten YAML-Struktur
    public class YamlItem
    {
        public string name { get; set; }
        public string unique_id { get; set; }
        public string state_topic { get; set; }
        public string unit_of_measurement { get; set; }
        public int qos { get; set; }
        public string value_template { get; set; }
    }

    // Benutzerdefinierter TypeInspector, um bestimmte Strings zu quoten
    public class QuoteStringTypeInspector : ITypeInspector
    {
        private readonly ITypeInspector _innerTypeInspector;

        public QuoteStringTypeInspector(ITypeInspector innerTypeInspector)
        {
            _innerTypeInspector = innerTypeInspector ?? throw new ArgumentNullException(nameof(innerTypeInspector));
        }

        public IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container)
        {
            return _innerTypeInspector.GetProperties(type, container)
                .Select(OverrideProperty);
        }

        private IPropertyDescriptor OverrideProperty(IPropertyDescriptor propertyDescriptor)
        {
            // Wir überschreiben nur, wenn der Typ des Properties ein String ist
            if (propertyDescriptor.Type == typeof(string))
            {
                return new QuotedStringPropertyDescriptor(propertyDescriptor);
            }
            return propertyDescriptor;
        }

        public IPropertyDescriptor GetProperty(Type type, object? container, string name, bool ignoreUnmatched, bool caseInsensitivePropertyMatching)
        {
            return _innerTypeInspector.GetProperty(type, container, name, ignoreUnmatched, caseInsensitivePropertyMatching);
        }

        public string GetEnumName(Type enumType, string name)
        {
            return _innerTypeInspector.GetEnumName(enumType, name);
        }

        public string GetEnumValue(object enumValue)
        {
            return _innerTypeInspector.GetEnumValue(enumValue);
        }
    }

    // Benutzerdefinierter PropertyDescriptor, um den ScalarStyle zu setzen
    public class QuotedStringPropertyDescriptor : IPropertyDescriptor
    {
        private readonly IPropertyDescriptor _baseDescriptor;

        public QuotedStringPropertyDescriptor(IPropertyDescriptor baseDescriptor)
        {
            _baseDescriptor = baseDescriptor ?? throw new ArgumentNullException(nameof(baseDescriptor));
            ScalarStyle = ScalarStyle.DoubleQuoted;
        }

        // Implementierung der IPropertyDescriptor-Mitglieder

        public string Name => _baseDescriptor.Name;

        public bool CanWrite => _baseDescriptor.CanWrite;

        public Type Type => _baseDescriptor.Type;

        public Type? TypeOverride
        {
            get => _baseDescriptor.TypeOverride;
            set => _baseDescriptor.TypeOverride = value;
        }

        public int Order
        {
            get => _baseDescriptor.Order;
            set => _baseDescriptor.Order = value;
        }

        public ScalarStyle ScalarStyle
        {
            get => _baseDescriptor.ScalarStyle;
            set => _baseDescriptor.ScalarStyle = value;
        }

        public bool Required => _baseDescriptor.Required;

        public bool AllowNulls => _baseDescriptor.AllowNulls;

        public Type? ConverterType => _baseDescriptor.ConverterType;

        public T? GetCustomAttribute<T>() where T : Attribute
        {
            return _baseDescriptor.GetCustomAttribute<T>();
        }

        public IObjectDescriptor Read(object target)
        {
            return _baseDescriptor.Read(target);
        }

        public void Write(object target, object? value)
        {
            _baseDescriptor.Write(target, value);
        }
    }
}
