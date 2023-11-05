using bluez.DBus;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;


namespace RazManagerIO.Host.Services.Utilities
{
    public class BluezGattDescriptor : BluezDBusObjectPropertiesBase<GattDescriptor1Properties>, IGattDescriptor1
    {
        public BluezGattDescriptor(ObjectPath objectPath, GattDescriptor1Properties gattDescriptor1Properties)
            : base(objectPath, gattDescriptor1Properties)
        {

        }

        public Task<byte[]> ReadValueAsync(IDictionary<string, object> Options)
        {
            return Task.FromResult(Properties.Value);
        }

        public Task WriteValueAsync(byte[] Value, IDictionary<string, object> Options)
        {
            throw new System.NotImplementedException();
        }

        public IDictionary<string, IDictionary<string, object>> GetProperties()
        {
            return new Dictionary<string, IDictionary<string, object>>()
            {
                {
                    "org.bluez.GattDescriptor1", new Dictionary<string, object>
                    {
                        { "Characteristic", Properties.Characteristic },
                        { "UUID", Properties.UUID },
                        //{ "Flags", Properties.Flags }
                    }
                }
                //public string UUID
                //public ObjectPath Characteristic
                //public byte[] Value
            };
        }
    }
}