public class DeviceMessage
{
    public const ushort SyncWord = 0xAA55;
    public required byte[] DeviceId { get; set; }
    public ushort MessageCounter { get; set; }
    public byte MessageType { get; set; }
    public required byte[] Payload { get; set; }
}
