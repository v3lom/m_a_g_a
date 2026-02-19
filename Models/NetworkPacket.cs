using System.Runtime.Serialization;

namespace M_A_G_A.Models
{
    [DataContract]
    public class NetworkPacket
    {
        [DataMember] public string PacketType { get; set; }   // DISCOVER, TEXT, VOICE, BYE, AVATAR
        [DataMember] public string SenderId { get; set; }
        [DataMember] public string SenderName { get; set; }
        [DataMember] public string SenderAvatar { get; set; } // base64
        [DataMember] public string Content { get; set; }      // text or base64 audio
        [DataMember] public string Timestamp { get; set; }
        [DataMember] public int TcpPort { get; set; }
    }
}
