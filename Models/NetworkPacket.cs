using System.Runtime.Serialization;

namespace M_A_G_A.Models
{
    /// PacketType values:
    ///   DISCOVER  – periodic broadcast (UDP)
    ///   BYE       – leaving (UDP)
    ///   TEXT      – plain / markdown text message
    ///   VOICE     – base64 WAV audio message
    ///   IMAGE     – base64 image bytes
    ///   FILE      – base64 arbitrary file
    ///   HISTORY   – JSON-encoded history export
    ///   AVATAR    – avatar push (TCP)
    [DataContract]
    public class NetworkPacket
    {
        [DataMember] public string PacketType   { get; set; }
        [DataMember] public string MessageId    { get; set; }   // unique per message
        [DataMember] public string SenderId     { get; set; }   // stable MAC+hostname hash
        [DataMember] public string SenderName   { get; set; }
        [DataMember] public string SenderAvatar { get; set; }   // base64 PNG
        [DataMember] public string MacAddress   { get; set; }   // physical MAC
        [DataMember] public string Hostname     { get; set; }   // machine name
        [DataMember] public string IPv4         { get; set; }
        [DataMember] public string IPv6         { get; set; }
        [DataMember] public string Content      { get; set; }   // text or base64 audio/image/file
        [DataMember] public string FileName     { get; set; }   // original filename for FILE/IMAGE
        [DataMember] public string Timestamp    { get; set; }
        [DataMember] public int    TcpPort      { get; set; }
    }
}
