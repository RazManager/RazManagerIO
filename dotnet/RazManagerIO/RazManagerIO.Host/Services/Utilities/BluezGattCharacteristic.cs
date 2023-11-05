using bluez.DBus;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Tmds.DBus;


namespace RazManagerIO.Host.Services.Utilities
{
    public class BluezGattCharacteristic : BluezDBusObjectPropertiesBase<GattCharacteristic1Properties>, IGattCharacteristic1
    {
        public IList<BluezGattDescriptor> Descriptors { get; } = new List<BluezGattDescriptor>();

        private readonly ICharacteristicSource _CharacteristicSource;

        public BluezGattCharacteristic(ObjectPath objectPath, GattCharacteristic1Properties properties, ICharacteristicSource characteristicSource) : base(objectPath, properties)
        {
            _CharacteristicSource = characteristicSource;
        }


        public Task<byte[]> ReadValueAsync(IDictionary<string, object> Options)
        {
            return _CharacteristicSource.ReadValueAsync();
        }

        public Task WriteValueAsync(byte[] Value, IDictionary<string, object> Options)
        {
            return _CharacteristicSource.WriteValueAsync(Value);
        }

        public Task<(CloseSafeHandle fd, ushort mtu)> AcquireWriteAsync(IDictionary<string, object> Options)
        {
            throw new NotImplementedException();
        }

        public Task<(CloseSafeHandle fd, ushort mtu)> AcquireNotifyAsync(IDictionary<string, object> Options)
        {
            throw new NotImplementedException();
        }

        public Task StartNotifyAsync()
        {
            throw new NotImplementedException();
        }

        public Task StopNotifyAsync()
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, IDictionary<string, object>> GetProperties()
        {
            return new Dictionary<string, IDictionary<string, object>>
            {
                {
                    "org.bluez.GattCharacteristic1", new Dictionary<string, object>
                    {
                        {"Service", Properties.Service},
                        {"UUID", Properties.UUID},
                        {"Flags", Properties.Flags},
                        //{"Descriptors", Descriptors.Select(d => d.ObjectPath).ToArray()}
                    }

                    //public string UUID
                    //public ObjectPath Service
                    //public byte[] Value
                    //public bool Notifying
                    //public string[] Flags
                    //public bool WriteAcquired
                    //public bool NotifyAcquired
                    //public ushort MTU
                }
            };
        }

        public BluezGattDescriptor AddDescriptor(GattDescriptor1Properties gattDescriptorProperties)
        {
            gattDescriptorProperties.Characteristic = ObjectPath;
            var gattDescriptor = new BluezGattDescriptor(NextDescriptorPath(), gattDescriptorProperties);
            Descriptors.Add(gattDescriptor);
            return gattDescriptor;
        }

        private ObjectPath NextDescriptorPath()
        {
            return ObjectPath + "/descriptor" + Descriptors.Count;
        }
    }
}
