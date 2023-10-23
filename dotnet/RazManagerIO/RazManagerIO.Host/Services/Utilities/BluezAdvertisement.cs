using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;


namespace RazManagerIO.Host.Services.Utilities
{
    [DBusInterface("org.bluez.LEAdvertisement1")]
    interface ILEAdvertisement1 : IDBusObject
    {
        Task ReleaseAsync();
        Task<object> GetAsync(string prop);
        Task<LEAdvertisement1Properties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    public class LEAdvertisement1Properties
    {
        private string _Type = default(string);
        public string Type
        {
            get
            {
                return _Type;
            }

            set
            {
                _Type = (value);
            }
        }

        private string[] _ServiceUUIDs = default(string[]);
        public string[] ServiceUUIDs
        {
            get
            {
                return _ServiceUUIDs;
            }

            set
            {
                _ServiceUUIDs = (value);
            }
        }

        private IDictionary<ushort, object> _ManufacturerData = default(IDictionary<ushort, object>);
        public IDictionary<ushort, object> ManufacturerData
        {
            get
            {
                return _ManufacturerData;
            }

            set
            {
                _ManufacturerData = (value);
            }
        }

        private string[] _SolicitUUIDs = default(string[]);
        public string[] SolicitUUIDs
        {
            get
            {
                return _SolicitUUIDs;
            }

            set
            {
                _SolicitUUIDs = (value);
            }
        }

        private IDictionary<string, object> _ServiceData;
        public IDictionary<string, object> ServiceData
        {
            get => _ServiceData;
            set => _ServiceData = value;
        }

        private bool _IncludeTxPower;
        public bool IncludeTxPower
        {
            get => _IncludeTxPower;
            set => _IncludeTxPower = value;
        }

        private string _LocalName = default(string);
        public string LocalName
        {
            get
            {
                return _LocalName;
            }

            set
            {
                _LocalName = (value);
            }
        }
    }



    public class BluezAdvertisement : BluezDBusObjectPropertiesBase<LEAdvertisement1Properties>, ILEAdvertisement1
    {
        public BluezAdvertisement(ObjectPath objectPath, LEAdvertisement1Properties properties) : base(objectPath, properties)
        {
        }


        public Task ReleaseAsync()
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }

        public event Action<PropertyChanges> OnPropertyChanges;


    }
}
