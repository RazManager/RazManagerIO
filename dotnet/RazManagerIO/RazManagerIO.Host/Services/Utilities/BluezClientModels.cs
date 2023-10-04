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


    public class BluezClientGattCharacteristic
    {
        [Required]
        public string Uuid { get; set; } = null!;

        public string? Name { get; set; }

        public string? Value { get; set; }

        public int? Length { get; set; }

        [Required]
        public List<BluezClientGattCharacteristicFlag> Flags { get; init; } = new();
    }


    public class BluezClientGattCharacteristicFlag
    {
        [Required]
        public required string Flag { get; init; }
    }


}
