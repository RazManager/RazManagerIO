using bluez.DBus;
using System.Collections.Generic;

namespace RazManagerIO.Host.Services.Utilities
{
    public class BluezGattService : BluezDBusObjectPropertiesBase<GattService1Properties>, IGattService1
    {
        private readonly IList<BluezGattCharacteristic> _Characteristics = new List<BluezGattCharacteristic>();

        public IEnumerable<BluezGattCharacteristic> Characteristics => _Characteristics;

        public BluezGattService(string objectPath, GattService1Properties properties) : base(objectPath, properties)
        {
        }

        public IDictionary<string, IDictionary<string, object>> GetProperties()
        {
            return new Dictionary<string, IDictionary<string, object>>
            {
                {
                    "org.bluez.GattService1", new Dictionary<string, object>
                    {
                        {"UUID", Properties.UUID},
                        {"Primary", Properties.Primary},
                        //{"Characteristics", Characteristics.Select(c => c.ObjectPath).ToArray()}
                    }
                    //public string UUID
                    //public ObjectPath Device
                    //public bool Primary
                    //public ObjectPath[] Includes
                }
            };
        }


        public BluezGattCharacteristic AddCharacteristic(GattCharacteristic1Properties characteristic, ICharacteristicSource characteristicSource)
        {
            characteristic.Service = ObjectPath;
            var gattCharacteristic = new BluezGattCharacteristic(NextCharacteristicPath(), characteristic, characteristicSource);
            _Characteristics.Add(gattCharacteristic);

            //Properties.Characteristics = Properties.Characteristics.Append(NextCharacteristicPath()).ToArray();

            return gattCharacteristic;
        }

        private string NextCharacteristicPath()
        {
            return ObjectPath + "/characteristic" + _Characteristics.Count;
        }
    }
}
