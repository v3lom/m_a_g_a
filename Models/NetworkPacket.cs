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
    ///   VIDEO     – base64 video bytes
    ///   HISTORY   – JSON-encoded history export
    ///   AVATAR    – avatar push (TCP)
    ///   PROFILE   – full profile update (name + avatar + bio)
    ///   ACK       – delivery acknowledgement
    ///   READ      – read acknowledgement
    ///   EDIT      – message edit (MessageId + Content)
    ///   DELETE    – message deletion (MessageId)
    ///   REACT     – reaction (MessageId + Content=emoji + Extra=userId)
    ///   ENCRYPT_REQ – request encrypted chat (Content = initiator's public suggestion)
    ///   ENCRYPT_ACK – accept encrypted chat
    [DataContract]
    public class NetworkPacket
    {
        [DataMember] public string PacketType   { get; set; }
        [DataMember] public string MessageId    { get; set; }   // unique per message
        [DataMember] public string SenderId     { get; set; }   // stable MAC+hostname hash
        [DataMember] public string SenderName   { get; set; }
        [DataMember] public string SenderAvatar { get; set; }   // base64 PNG
        [DataMember] public string SenderBio    { get; set; }   // bio text (max 1024)
        [DataMember] public string MacAddress   { get; set; }   // physical MAC
        [DataMember] public string Hostname     { get; set; }   // machine name
        [DataMember] public string IPv4         { get; set; }
        [DataMember] public string IPv6         { get; set; }
        [DataMember] public string Content      { get; set; }   // text or base64 audio/image/file
        [DataMember] public string FileName     { get; set; }   // original filename for FILE/IMAGE/VIDEO
        [DataMember] public string Timestamp    { get; set; }
        [DataMember] public int    TcpPort      { get; set; }
        [DataMember] public string Extra        { get; set; }   // general-purpose field (e.g. userId in REACT)
        /// <summary>When true the Content field is AES-256-CBC encrypted (base64).</summary>
        [DataMember] public bool   IsEncrypted  { get; set; }
    }
}
