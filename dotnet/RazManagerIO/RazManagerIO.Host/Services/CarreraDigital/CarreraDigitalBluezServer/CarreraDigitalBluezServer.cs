using RazManagerIO.Host.Services.Utilities;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.AspNetCore.Server.Kestrel;
using bluez.DBus;
using Tmds.DBus;
using System.Collections.Generic;
using DotnetBleServer.Core;
using DotnetBleServer.Advertisements;


namespace RazManagerIO.Host.Services.CarreraDigital
{
    public class CarreraDigitalBluezServer: ICarreraDigitalBluezServer
    {
        public const string bluezService = "org.bluez";
        public const string bluezAdapterInterface = "org.bluez.Adapter1";
        public const string bluezGattManagerInterface = "org.bluez.GattManager1";
        public const string bluezDeviceInterface = "org.bluez.Device1";
        public const string bluezGattCharacteristicInterface = "org.bluez.GattCharacteristic1";
        public const string bluezLEAdvertisingManagerInterface = "org.bluez.LEAdvertisingManager1";

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
                using (var dBusSystemConnection = new Tmds.DBus.Connection(Address.System))
                {
                    await dBusSystemConnection.ConnectAsync();

                    var advertisementProperties = new AdvertisementProperties
                    {
                        Type = "peripheral",
                        ServiceUUIDs = new[] { "12345678-1234-5678-1234-56789abcdef0" },
                        LocalName = "F",
                    };

                   // Find all D-Bus objects and their interfaces
                   var objectManager = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IObjectManager>(bluezService, Tmds.DBus.ObjectPath.Root);
                   var dBusObjects = await objectManager.GetManagedObjectsAsync();

                   var dBusInterfaces = dBusObjects.SelectMany(dBusObject => dBusObject.Value, (ObjectPath, iface) => new { ObjectPath.Key, iface });

                   var bluezAdapterInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezAdapterInterface);
                   if (bluezAdapterInterfaceKp is null)
                   {
                       _logger.LogCritical($"{bluezAdapterInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                       //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                       return;
                   }

                   var bluezAdapterProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IAdapter1>(bluezService, bluezAdapterInterfaceKp.Key);
                   await bluezAdapterProxy.SetPoweredAsync(true);

                   var advertisingManagerInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezLEAdvertisingManagerInterface);
                   if (advertisingManagerInterfaceKp is null)
                   {
                       _logger.LogCritical($"{bluezLEAdvertisingManagerInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                       //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                       return;
                   }

                    var advertisingManagerProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.ILEAdvertisingManager1>(bluezService, advertisingManagerInterfaceKp.Key);


                    var advertisingManager = dBusSystemConnection.CreateProxy<DotnetBleServer.Core.ILEAdvertisingManager1>("org.bluez", "/org/bluez/hci0");

                    var advertisement = new Advertisement("/org/bluez/example/advertisement0", advertisementProperties);

                    await dBusSystemConnection.RegisterObjectAsync(advertisement);
                    Console.WriteLine($"advertisement object {advertisement.ObjectPath} created");

                    await advertisingManager.RegisterAdvertisementAsync(((IDBusObject) advertisement).ObjectPath,
                        new Dictionary<string, object>());

                    Console.WriteLine($"advertisement {advertisement.ObjectPath} registered in BlueZ advertising manager");

                    //await SampleGattApplication.RegisterGattApplication(serverContext);

                    Console.WriteLine("Advertising...");

                    await Task.Delay(Timeout.Infinite);
                }

                ////var dBusSystem = Tmds.DBus.Connection.System;
                //using (var dBusSystemConnection = new Tmds.DBus.Connection(Address.System))
                //{
                //    var connectionInfo = await dBusSystemConnection.ConnectAsync();

                //    // Find all D-Bus objects and their interfaces
                //    var objectManager = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IObjectManager>(bluezService, Tmds.DBus.ObjectPath.Root);
                //    var dBusObjects = await objectManager.GetManagedObjectsAsync();

                //    var dBusInterfaces = dBusObjects.SelectMany(dBusObject => dBusObject.Value, (ObjectPath, iface) => new { ObjectPath.Key, iface });

                //    var bluezAdapterInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezAdapterInterface);
                //    if (bluezAdapterInterfaceKp is null)
                //    {
                //        _logger.LogCritical($"{bluezAdapterInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                //        //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                //        return;
                //    }

                //    var bluezAdapterProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IAdapter1>(bluezService, bluezAdapterInterfaceKp.Key);
                //    await bluezAdapterProxy.SetPoweredAsync(true);

                //    var advertisingManagerInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezLEAdvertisingManagerInterface);
                //    if (advertisingManagerInterfaceKp is null)
                //    {
                //        _logger.LogCritical($"{bluezLEAdvertisingManagerInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                //        //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                //        return;
                //    }

                //    var advertisingManagerProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.ILEAdvertisingManager1>(bluezService, advertisingManagerInterfaceKp.Key);

                //    var advertisementProperties = new LEAdvertisement1Properties
                //    {
                //        Type = "peripheral",
                //        ServiceUUIDs = new[] { "12345678-1234-5678-1234-56789abcdef0" },
                //        LocalName = "DotNetBle"
                //    };

                //    var advertisement = new BluezAdvertisement(new ObjectPath("/org/bluez/example/advertisement1"), advertisementProperties);
                //    await dBusSystemConnection.RegisterObjectAsync(advertisement);
                //    //var x = advertisement as IDBusObject;
                //    //await dBusSystem.RegisterObjectAsync(x);

                //    await advertisingManagerProxy.RegisterAdvertisementAsync(((IDBusObject)advertisement).ObjectPath, new Dictionary<string, object>());

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
