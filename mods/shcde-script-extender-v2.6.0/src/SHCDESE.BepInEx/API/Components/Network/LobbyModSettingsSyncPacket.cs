using MessagePack;

namespace SHCDESE.API.Components.Network;

[MessagePackObject]
public class LobbyModSettingSyncPacket
{
    [Key(0)] public string ModName { get; set; }
    [Key(1)] public string PropertyName { get; set; }
    [Key(2)] public byte[] SerializedValue { get; set; }
    [Key(3)] public string TypeName { get; set; }
    [Key(4)] public int SourcePlayerId { get; set; }
}