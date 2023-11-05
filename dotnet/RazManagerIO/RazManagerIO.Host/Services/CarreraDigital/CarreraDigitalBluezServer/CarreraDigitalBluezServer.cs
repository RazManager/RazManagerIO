using RazManagerIO.Host.Services.Utilities;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using bluez.DBus;
using Tmds.DBus;
using System.Collections.Generic;
using System.Text;
using System.Reflection.PortableExecutable;


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

                    var bluezAdvertisingManagerInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezLEAdvertisingManagerInterface);
                    if (bluezAdvertisingManagerInterfaceKp is null)
                    {
                        _logger.LogCritical($"{bluezLEAdvertisingManagerInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                        //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                        return;
                    }
                    var bluezAdvertisingManagerProxy = dBusSystemConnection.CreateProxy<bluez.DBus.ILEAdvertisingManager1>(bluezService, bluezAdvertisingManagerInterfaceKp.Key);

                    var bluezGattManagerInterfaceKp = dBusInterfaces.FirstOrDefault(x => x.iface.Key == bluezGattManagerInterface);
                    if (bluezGattManagerInterfaceKp is null)
                    {
                        _logger.LogCritical($"{bluezGattManagerInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                        //await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                        return;
                    }
                    var bluezGattManagerProxy = dBusSystemConnection.CreateProxy<bluez.DBus.IGattManager1>(bluezService, bluezGattManagerInterfaceKp.Key);

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

                    await bluezAdvertisingManagerProxy.WatchPropertiesAsync(propertyChanges => 
                    {
                        Console.WriteLine(@"bluezAdvertisingManagerProxy.WatchPropertiesAsync {propertyChanges}");
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
                    await bluezAdvertisingManagerProxy.RegisterAdvertisementAsync(advertisement.ObjectPath, new Dictionary<string, object>());

                    Console.WriteLine("Advertising.");

                    var gattServiceDescription = new BluezGattServiceDescription
                    {
                        UUID = "12345678-1234-5678-1234-56789abcdef0",
                        Primary = true
                    };

                    var gattCharacteristicDescription = new BluezGattCharacteristicDescription
                    {
                        CharacteristicSource = new ExampleCharacteristicSource(),
                        UUID = "12345678-1234-5678-1234-56789abcdef1",
                        Flags = CharacteristicFlags.Read | CharacteristicFlags.Write | CharacteristicFlags.WritableAuxiliaries
                    };

                    var gattDescriptorDescription = new BluezGattDescriptorDescription
                    {
                        Value = new[] { (byte)'t' },
                        UUID = "12345678-1234-5678-1234-56789abcdef2",
                        Flags = new[] { "read", "write" }
                    };

                    gattCharacteristicDescription.AddDescriptor(gattDescriptorDescription);
                    gattServiceDescription.AddCharacteristic(gattCharacteristicDescription);

                    var appId = Guid.NewGuid().ToString().Substring(0, 8);
                    var applicationObjectPath = $"/{appId}";

                    var application = new BluezGattApplication(applicationObjectPath);
                    await dBusSystemConnection.RegisterObjectAsync(application);

                    var gattService1Properties = new GattService1Properties
                    {
                        UUID = gattServiceDescription.UUID,
                        Primary = gattServiceDescription.Primary,
                        //Characteristics = new ObjectPath[0]
                    };
                    var gattService = application.AddService(gattService1Properties);
                    await dBusSystemConnection.RegisterObjectAsync(gattService);

                    foreach (var characteristicDescription in gattServiceDescription.GattCharacteristicDescriptions)
                    {
                        var gattCharacteristic1Properties = new GattCharacteristic1Properties
                        {
                            UUID = characteristicDescription.UUID,
                            Flags = CharacteristicFlagConverter.ConvertFlags(characteristicDescription.Flags)
                        };
                        var gattCharacteristic = gattService.AddCharacteristic(gattCharacteristic1Properties, characteristicDescription.CharacteristicSource);
                        await dBusSystemConnection.RegisterObjectAsync(gattCharacteristic);

                        foreach (var descriptorDescription in characteristicDescription.Descriptors)
                        {
                            var gattDescriptor1Properties = new GattDescriptor1Properties
                            {
                                UUID = descriptorDescription.UUID,
                                //Flags = descriptorDescription.Flags,
                                Value = descriptorDescription.Value
                            };

                            var gattDescriptor = gattCharacteristic.AddDescriptor(gattDescriptor1Properties);
                            await dBusSystemConnection.RegisterObjectAsync(gattDescriptor);
                        }
                    }

                    await bluezGattManagerProxy.RegisterApplicationAsync(new ObjectPath(applicationObjectPath), new Dictionary<string, object>());

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


    internal class ExampleCharacteristicSource : ICharacteristicSource
    {
        public Task WriteValueAsync(byte[] value)
        {
            Console.WriteLine("Writing value");
            return Task.Run(() => Console.WriteLine(Encoding.ASCII.GetChars(value)));
        }

        public Task<byte[]> ReadValueAsync()
        {
            Console.WriteLine("Reading value");
            return Task.FromResult(Encoding.ASCII.GetBytes("Hello BLE"));
        }
    }
}
