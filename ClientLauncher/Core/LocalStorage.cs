using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace ClientLauncher.Core
{
    public class LocalStorage
    {
        private static readonly int    InternalStorageRevision = 2;
        private static readonly string InternalAppDataFolder = "NewVegasMultiplayer";
        private static readonly string InternalDataFilename = "newvegasmp.config";

        public EventHandler Loaded;
        public EventHandler Saved;

        private static List<string> TrustedDefaultServers = new List<string>
        {
            "murica-east.nv-mp.com:27017"
        };

        [XmlRoot("dictionary")]
        public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
        {

            #region IXmlSerializable Members
            public System.Xml.Schema.XmlSchema GetSchema()
            {
                return null;
            }


            public void ReadXml(System.Xml.XmlReader reader)
            {
                XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
                XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

                bool wasEmpty = reader.IsEmptyElement;
                reader.Read();

                if (wasEmpty)
                    return;

                while (reader.NodeType != System.Xml.XmlNodeType.EndElement)
                {
                    reader.ReadStartElement("item");
                    reader.ReadStartElement("key");
                    TKey key = (TKey)keySerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    reader.ReadStartElement("value");
                    TValue value = (TValue)valueSerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    this.Add(key, value);
                    reader.ReadEndElement();
                    reader.MoveToContent();
                }

                reader.ReadEndElement();
            }



            public void WriteXml(System.Xml.XmlWriter writer)
            {
                XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
                XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

                foreach (TKey key in this.Keys)
                {
                    writer.WriteStartElement("item");
                    writer.WriteStartElement("key");
                    keySerializer.Serialize(writer, key);
                    writer.WriteEndElement();
                    writer.WriteStartElement("value");
                    TValue value = this[key];
                    valueSerializer.Serialize(writer, value);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

            }

            #endregion

        }

        /// <remarks>
        /// The default value is <c>DateTimeOffset.MinValue</c>. This is a value
        /// type and has the same hash code as <c>DateTimeOffset</c>! Implicit
        /// assignment from <c>DateTime</c> is neither implemented nor desirable!
        /// </remarks>
        public struct Iso8601SerializableDateTimeOffset : IXmlSerializable
        {
            private DateTimeOffset value;

            public Iso8601SerializableDateTimeOffset(DateTimeOffset value)
            {
                this.value = value;
            }

            public static implicit operator Iso8601SerializableDateTimeOffset(DateTimeOffset value)
            {
                return new Iso8601SerializableDateTimeOffset(value);
            }

            public static implicit operator DateTimeOffset(Iso8601SerializableDateTimeOffset instance)
            {
                return instance.value;
            }

            public static bool operator ==(Iso8601SerializableDateTimeOffset a, Iso8601SerializableDateTimeOffset b)
            {
                return a.value == b.value;
            }

            public static bool operator !=(Iso8601SerializableDateTimeOffset a, Iso8601SerializableDateTimeOffset b)
            {
                return a.value != b.value;
            }

            public static bool operator <(Iso8601SerializableDateTimeOffset a, Iso8601SerializableDateTimeOffset b)
            {
                return a.value < b.value;
            }

            public static bool operator >(Iso8601SerializableDateTimeOffset a, Iso8601SerializableDateTimeOffset b)
            {
                return a.value > b.value;
            }

            public override bool Equals(object o)
            {
                if (o is Iso8601SerializableDateTimeOffset)
                    return value.Equals(((Iso8601SerializableDateTimeOffset)o).value);
                else if (o is DateTimeOffset)
                    return value.Equals((DateTimeOffset)o);
                else
                    return false;
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            public System.Xml.Schema.XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(System.Xml.XmlReader reader)
            {
                var text = reader.ReadElementString();
                value = DateTimeOffset.ParseExact(text, format: "o", formatProvider: null);
            }

            public override string ToString()
            {
                return value.ToString(format: "o");
            }

            public string ToString(string format)
            {
                return value.ToString(format);
            }

            public void WriteXml(System.Xml.XmlWriter writer)
            {
                writer.WriteString(value.ToString(format: "o"));
            }
        }


        [DataContract]
        [XmlSerializerFormat]
        public class AuthenticationKey
        {
            [DataMember]
            public string IssuingServerToken { get; set; }

            [DataMember]
            public string ClientID { get; set; }

            [DataMember]
            public string AuthenticatorType { get; set; }

            [DataMember]
            public string Name { get; set; }

            [DataMember]
            public string AuthorizationBlob { get; set; }

            [DataMember]
            public string RefreshToken { get; set; }

            [DataMember]
            public Iso8601SerializableDateTimeOffset ExpiresAt { get; set; }

            [DataMember]
            public string ImageURL { get; set; }
        }

        [DataContract]
        [XmlSerializerFormat]
        public class DataStore
        {
            [DataMember]
            public int Revision { get; set; } = InternalStorageRevision;

            [DataMember]
            public string CustomToken { get; set; } = "";

            [DataMember]
            public string PreviousCustomIP { get; set; } = "localhost:27015";

            [DataMember]
            public string GamePathOverride { get; set; } = null;

            [DataMember]
            public List<string> JoinedServersList { get; set; } = new List<string>();

            [DataMember]
            public List<string> StarredServers { get; set; } = new List<string>();

            [DataMember]
            public SerializableDictionary<string, AuthenticationKey> SavedServerKeys { get; set; } = new SerializableDictionary<string, AuthenticationKey>();
        }

        public string  CustomToken
        {
            set
            {
                if (InternalData.CustomToken != value)
                {
                    InternalData.CustomToken = value;
                    Save();
                }
            }
            get
            {
                return InternalData.CustomToken;
            }
        }

        public string GamePathOverride
        {
            set
            {
                if (InternalData.GamePathOverride != value)
                {
                    InternalData.GamePathOverride = value;
                    Save();
                }
            }
            get
            {
                return InternalData.GamePathOverride;
            }
        }

        public string PreviousCustomIP
        {
            set
            {
                if (InternalData.PreviousCustomIP != value)
                {
                    InternalData.PreviousCustomIP = value;
                    Save();
                }
            }
            get
            {
                return InternalData.PreviousCustomIP;
            }
        }

        public IEnumerable<string> StarredServers
        {
            get
            {
                return InternalData.StarredServers;
            }
        }

        public SerializableDictionary<string, AuthenticationKey> AuthKeys
        {
            set
            {
                InternalData.SavedServerKeys = value;
                Save();
            }
            get
            {
                return InternalData.SavedServerKeys;
            }
        }

        private string AppdataStorage
        {
            get
            {
                return $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\{InternalAppDataFolder}";
            }
        }

        private string FilenameStorage
        {
            get
            {
                return $"{AppdataStorage}\\{InternalDataFilename}";
            }
        }

        private DataStore InternalData;

        public LocalStorage()
        {
            InternalData = new DataStore();
            if (!Directory.Exists(AppdataStorage))
            {
                Directory.CreateDirectory(AppdataStorage);
            }
        }

        public void TryFlushExpiredKeys()
        {
            if (InternalData == null)
            {
                return;
            }

            var keysToFlush = new List<string>();
            var now = DateTimeOffset.UtcNow;

            foreach (var pair in InternalData.SavedServerKeys)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    Trace.WriteLine("Flushing key...");
                    keysToFlush.Add(pair.Key);
                }
            }

            if (keysToFlush.Count != 0)
            {
                foreach (var key in keysToFlush)
                {
                    InternalData.SavedServerKeys.Remove(key);
                }

                Save();
            }
        }

        public bool TryLoadSavedData()
        {
            try
            {
                if (File.Exists(FilenameStorage))
                {
                    DataStore loadedData = null;
                    using (FileStream file = File.Open(FilenameStorage, FileMode.OpenOrCreate))
                    {
                        var ser = new XmlSerializer(typeof(DataStore));

                        try
                        {
                            loadedData = (DataStore)ser.Deserialize(file);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Existing launcher settings were corrupted!", "New Vegas: Multiplayer");
                        }

                        file.Close();
                    }

                    // If the revision number changes, then dispose of the loaded data
                    if (loadedData.Revision == InternalStorageRevision)
                    {
                        InternalData = loadedData;
                    }
                    else
                    {
                        // Try to keep the stored directory if we roll over, this file path doesn't really change
                        // This would majorly piss people off if we deleted their custom path if they are using a custom folder
                        InternalData = new DataStore
                        {
                            GamePathOverride = loadedData.GamePathOverride
                        };

                        try
                        {
                            // Purge the LauncherBackground if the revision changes
                            string GameDir = FalloutFinder.GameDir(this);

                            if (GameDir != null)
                            {
                                if (Directory.Exists(GameDir + "\\nvmp\\res"))
                                {
                                    if (File.Exists(GameDir + "\\nvmp\\res\\LauncherBackground.png"))
                                    {
                                        File.Delete(GameDir + "\\nvmp\\res\\LauncherBackground.png");
                                    }
                                }
                            }
                        } catch (Exception) { }

                        Save();
                    }

                    // Ensure that the trusted servers list is always up to date with the current loaded data.
                    var trustedServersMissing = TrustedDefaultServers.Where(x => !InternalData.StarredServers.Contains(x)).ToArray();
                    foreach (var server in trustedServersMissing)
                    {
                        InternalData.StarredServers.Add(server);
                    }

                    if (trustedServersMissing.Any())
                    {
                        Save();
                    }

                    // Ensure that saved keys are flushed out
                    TryFlushExpiredKeys();

                    if (Loaded != null)
                    {
                        Loaded.Invoke(null, null);
                    }

                    return true;
                }
                else
                {
                    // Create some default data and then save it
                    InternalData = new DataStore();
                    Save();
                }

            } catch (Exception) { }

            return false;
        }

        public void Save()
        {
            if (!File.Exists(FilenameStorage))
            {
                File.Create(FilenameStorage).Dispose();
            }

            using (FileStream file = File.Open(FilenameStorage, FileMode.Truncate))
            {
                var ser = new XmlSerializer(typeof(DataStore));
                ser.Serialize(file, InternalData);
                file.Close();
            }

            if (Saved != null)
            {
                Saved.Invoke(null, null);
            }
        }
    }
}
