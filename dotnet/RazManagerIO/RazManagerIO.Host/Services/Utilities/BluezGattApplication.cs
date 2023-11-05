using bluez.DBus;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace RazManagerIO.Host.Services.Utilities
{
    [DBusInterface("org.freedesktop.DBus.ObjectManager")]
    public class BluezGattApplication : IObjectManager
    {
        private readonly ObjectPath _objectPath;
        private BluezGattService _gattService;


        public BluezGattApplication(string objectPath)
        {
            _objectPath = objectPath;
        }

        public ObjectPath ObjectPath => _objectPath;

        public Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync()
        {
            IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>> result =
                new Dictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>();
            result[_gattService.ObjectPath] = _gattService.GetProperties();
            foreach (var characteristic in _gattService.Characteristics)
            {
                result[characteristic.ObjectPath] = characteristic.GetProperties();
                foreach (var descriptor in characteristic.Descriptors)
                {
                    result[descriptor.ObjectPath] = descriptor.GetProperties();
                }
            }

            return Task.FromResult(result);
        }

        public Task<IDisposable> WatchInterfacesAddedAsync(Action<(ObjectPath @object, IDictionary<string, IDictionary<string, object>> interfaces)> handler, Action<Exception> onError = null)
        {
            throw new NotImplementedException();
        }

        public Task<IDisposable> WatchInterfacesRemovedAsync(Action<(ObjectPath @object, string[] interfaces)> handler, Action<Exception> onError = null)
        {
            throw new NotImplementedException();
        }


        public BluezGattService AddService(GattService1Properties gattServiceProperties)
        {
            var servicePath = ObjectPath + "/service";
            _gattService = new BluezGattService(servicePath, gattServiceProperties);
            return _gattService;
        }
    }
}
