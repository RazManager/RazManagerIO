using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace RazManagerIO.Host.Services.Utilities
{
    public class BluezGattServiceDescription
    {
        public required string UUID { get; init; }
        public required bool Primary { get; init; }

        public IList<BluezGattCharacteristicDescription> GattCharacteristicDescriptions { get; } =
            new List<BluezGattCharacteristicDescription>();

        public void AddCharacteristic(BluezGattCharacteristicDescription characteristic)
        {
            GattCharacteristicDescriptions.Add(characteristic);
        }
    }


    public class BluezGattCharacteristicDescription
    {
        private readonly IList<BluezGattDescriptorDescription> _Descriptors = new List<BluezGattDescriptorDescription>();

        public required string UUID { get; set; }
        public required CharacteristicFlags Flags { get; set; }

        public ICharacteristicSource CharacteristicSource { get; set; }

        public IEnumerable<BluezGattDescriptorDescription> Descriptors => _Descriptors;

        public void AddDescriptor(BluezGattDescriptorDescription gattDescriptorDescription)
        {
            _Descriptors.Add(gattDescriptorDescription);
        }
    }


    public class BluezGattDescriptorDescription
    {
        public byte[] Value { get; set; }
        public string UUID { get; set; }
        public string[] Flags { get; set; }
    }


    [Flags]
    public enum CharacteristicFlags
    {
        Read = 1,
        Write = 2,
        WritableAuxiliaries = 4
    }


    public interface ICharacteristicSource
    {
        Task WriteValueAsync(byte[] value);
        Task<byte[]> ReadValueAsync();
    }


    public class CharacteristicFlagConverter
    {
        private static readonly Dictionary<CharacteristicFlags, string> FlagMappings =
            new Dictionary<CharacteristicFlags, string>
            {
                {CharacteristicFlags.Read, "read"},
                {CharacteristicFlags.Write, "write"},
                {CharacteristicFlags.WritableAuxiliaries, "writable-auxiliaries"}
            };

        public static string[] ConvertFlags(CharacteristicFlags characteristicFlags)
        {
            return (from mapping in FlagMappings where (characteristicFlags & mapping.Key) > 0 select mapping.Value).ToArray();
        }
    }
}
