public class DeviceMessage
{
    public const ushort SyncWord = 0xAA55;
    public byte[] DeviceId { get; set; }
    public ushort MessageCounter { get; set; }
    public byte MessageType { get; set; }
    public byte[] Payload { get; set; }
}
