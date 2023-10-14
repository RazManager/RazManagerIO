using bluez.DBus;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


namespace RazManagerIO.Host.Services.Utilities
{
    public enum BluezClientBluetoothConnectionStateType
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
    }
}
