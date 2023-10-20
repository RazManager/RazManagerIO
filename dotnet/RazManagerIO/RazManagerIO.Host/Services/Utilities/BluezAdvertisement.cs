using bluez.DBus;


namespace RazManagerIO.Host.Services.Utilities
{
    public enum BluezAdvertisement : ILEAdvertisement1
    {
        Disabled,
        Enabled,
        Discovering,
        Connected,
        Initialized
    }


    public class BluezClientGattCharacteristic : GattCharacteristic1Properties
    {
        public string? Name { get; set; }

        public int? Length { get; set; }

        public BluezClientGattCharacteristic(GattCharacteristic1Properties properties)
        {
            this.UUID = properties.UUID;
            this.Service = properties.Service;
            this.Value = properties.Value;
            this.Notifying = properties.Notifying;
            this.Flags = properties.Flags;
            this.WriteAcquired = properties.WriteAcquired;
            this.NotifyAcquired = properties.NotifyAcquired;
            this.MTU = properties.MTU;
        }
    }
}
