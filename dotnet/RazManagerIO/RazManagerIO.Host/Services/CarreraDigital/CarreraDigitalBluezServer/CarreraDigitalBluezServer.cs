using RazManagerIO.Host.Services.Utilities;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using bluez.DBus;
using Tmds.DBus;
using System.Collections.Generic;


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
                //var dBusSystem = Tmds.DBus.Connection.System;
                using (var dBusSystemConnection = new Tmds.DBus.Connection(Address.System))
                {
                    await dBusSystemConnection.ConnectAsync();

                   // Find all D-Bus objects and their interfaces
                   var objectManager = dBusSystemConnection.CreateProxy<bluez.DBus.IObjectManager>(bluezService, Tmds.DBus.ObjectPath.Root);
                   var dBusObjects = await objectManager.GetManagedObjectsAsync();

                   var dBusInterfaces = dBusObjects.SelectMany(dBusObject => dBusObject.Value, (ObjectPath, iface) => new { ObjectPath.Key, iface });

                   var bluezAdapterInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezAdapterInterface);
                   if (bluezAdapterInterfaceKp is null)
                   {
                       _logger.LogCritical($"{bluezAdapterInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                       //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                       return;
                   }

                   var bluezAdapterProxy = dBusSystemConnection.CreateProxy<bluez.DBus.IAdapter1>(bluezService, bluezAdapterInterfaceKp.Key);
                   await bluezAdapterProxy.SetPoweredAsync(true);

                   var advertisingManagerInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezLEAdvertisingManagerInterface);
                   if (advertisingManagerInterfaceKp is null)
                   {
                       _logger.LogCritical($"{bluezLEAdvertisingManagerInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                       //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                       return;
                   }

                    var advertisingManagerProxy = dBusSystemConnection.CreateProxy<bluez.DBus.ILEAdvertisingManager1>(bluezService, advertisingManagerInterfaceKp.Key);

                    if (watchInterfacesAddedTask is not null)
                    {
                        watchInterfacesAddedTask.Dispose();
                    }
                    watchInterfacesAddedTask = objectManager.WatchInterfacesAddedAsync(
                        InterfaceAdded,
                        exception =>
                        {
                            _logger.LogError(exception, exception.Message);
                        }
                    );

                    if (watchInterfacesRemovedTask is not null)
                    {
                        watchInterfacesRemovedTask.Dispose();
                    }
                    watchInterfacesRemovedTask = objectManager.WatchInterfacesRemovedAsync(
                        InterfaceRemoved,
                        exception =>
                        {
                            _logger.LogError(exception, exception.Message);
                        }
                    );

                    await bluezAdapterProxy.WatchPropertiesAsync(propertyChanges => 
                    {
                        Console.WriteLine(@"bluezAdapterProxy.WatchPropertiesAsync {propertyChanges}");
                    });

                    await advertisingManagerProxy.WatchPropertiesAsync(propertyChanges => 
                    {
                        Console.WriteLine(@"advertisingManagerProxy.WatchPropertiesAsync {propertyChanges}");
                    });

                    _logger.LogInformation($"BlueZ initialization done.");
                    //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Enabled);

                    var advertisementProperties = new LEAdvertisement1Properties
                    {
                        Type = "peripheral",
                        ServiceUUIDs = new[] { "12345678-1234-5678-1234-56789abcdef0" },
                        LocalName = "A2",
                        Discoverable = true
                    };

                    var advertisement = new BluezAdvertisement(new ObjectPath("/org/bluez/example/advertisement1"), advertisementProperties);
                    await dBusSystemConnection.RegisterObjectAsync(advertisement);

                    await advertisingManagerProxy.RegisterAdvertisementAsync(advertisement.ObjectPath, new Dictionary<string, object>());

                    //await SampleGattApplication.RegisterGattApplication(serverContext);

                    Console.WriteLine("Advertising.");

                    await Task.Delay(Timeout.Infinite);
                }
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


        private void InterfaceAdded((Tmds.DBus.ObjectPath objectPath, IDictionary<string, IDictionary<string, object>> interfaces) args)
        {
            //Console.WriteLine($"{args.objectPath} added.");
            Console.WriteLine($"{args.objectPath} added with the following interfaces...");
            foreach (var iface in args.interfaces)
            {
                Console.WriteLine(iface.Key);
            }
        }


        private void InterfaceRemoved((Tmds.DBus.ObjectPath objectPath, string[] interfaces) args)
        {
            Console.WriteLine($"{args.objectPath} removed.");
        }
    }
}
