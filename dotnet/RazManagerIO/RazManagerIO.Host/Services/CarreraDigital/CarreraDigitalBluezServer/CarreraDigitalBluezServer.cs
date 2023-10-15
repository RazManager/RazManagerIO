using RazManagerIO.Host.Services.Utilities;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace RazManagerIO.Host.Services.CarreraDigital
{
    public class CarreraDigitalBluezServer: ICarreraDigitalBluezServer
    {
        public const string bluezService = "org.bluez";
        public const string bluezAdapterInterface = "org.bluez.Adapter1";
        public const string bluezGattManagerInterface = "org.bluez.GattManager1";
        public const string bluezDeviceInterface = "org.bluez.Device1";
        public const string bluezGattCharacteristicInterface = "org.bluez.GattCharacteristic1";

        private readonly ILogger<CarreraDigitalBluezServer> _logger;

        public CarreraDigitalBluezServer(ILogger<CarreraDigitalBluezServer> logger)
        {
            _logger = logger;
        }


        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Task? watchInterfacesAddedTask = null;
            Task? watchInterfacesRemovedTask = null;

            //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);

            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                _logger.LogCritical("You need to be running Linux for this application.");
                return;
            }

            try
            {

                // Find all D-Bus objects and their interfaces
                var objectManager = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IObjectManager>(bluezService, Tmds.DBus.ObjectPath.Root);
                var dBusObjects = await objectManager.GetManagedObjectsAsync();

                var ifaces = dBusObjects.SelectMany(dBusObject => dBusObject.Value, (dBusObject, iface) => new { dBusObject, iface });

                //foreach (var item in args.interfaces.Where(x => x.Key.StartsWith(bluezService)))


                //if (string.IsNullOrEmpty(bluezAdapterObjectPathKp.Key.ToString()))
                //{
                //    _logger.LogCritical($"{bluezAdapterInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                //    //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                //    return;
                //}

                //_bluezAdapterProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IAdapter1>(bluezService, bluezAdapterObjectPathKp.Key);

            }

            catch (Tmds.DBus.DBusException exception)
            {
                _logger.LogError(exception, $"{exception.ErrorName}, {exception.ErrorMessage}");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
            }


            //await ResetAsync(_deviceObjectPath);
        }
    }
}
