using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ClientLauncher.Dtos
{

    [DataContract]
    public class DtoServerModInfo 
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "file_path")]
        public string FilePath { get; set; }

        [DataMember(Name = "digest")]
        public string Digest { get; set; }

        [DataMember(Name = "downloadable")]
        public bool Downloadable { get; set; }
    }

    [DataContract]
    public class DtoGameServer
    {
        public DtoGameServer()
        {
        }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "ip")]
        public string IP { get; set; }

        [DataMember(Name = "port")]
        public ushort Port { get; set; }

        [DataMember(Name = "mods_download_url")]
        public string ModsDownloadURL { get; set; }

        [DataMember(Name = "max_players")]
        public int MaxPlayers { get; set; }

        [DataMember(Name = "num_players")]
        public int NumPlayers { get; set; }

        [DataMember(Name = "last_ping")]
        public DateTimeOffset LastPing { get; set; }

        [DataMember(Name = "region")]
        public string Region { get; set; } = null;

        // This is a public token used to allow clients to bookmark and favourite servers uniquely
        [DataMember(Name = "public_token")]
        public string PublicToken { get; set; }

        [DataMember(Name = "mods")]
        public List<DtoServerModInfo> Mods { get; set; }

        //
        // Custom Displays
        //
        public bool IsStarred { get; set; } = false;

        public bool IsPrivate { get; set; } = false;

        public Visibility StarredResourceVisibility
        {
            get
            {
                return IsStarred ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public string DisplayPing { get; set; } = "...";

        public string DisplayPlayers
        {
            get
            {
                return $"{NumPlayers}/{MaxPlayers}";
            }
        }

        public string DisplayHostInfo
        {
            get
            {
                return $"{IP}:{Port}";
            }
        }
    }
}
