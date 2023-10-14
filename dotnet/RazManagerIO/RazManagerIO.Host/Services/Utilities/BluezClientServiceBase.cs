using bluez.DBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using static bluez.DBus.Adapter1Extensions;
using static bluez.DBus.Device1Extensions;


namespace RazManagerIO.Host.Services.Utilities
{
    public abstract class BluezClientServiceBase : IBluezClientServiceBase
    {
        public const string bluezService = "org.bluez";
        public const string bluezAdapterInterface = "org.bluez.Adapter1";
        public const string bluezGattManagerInterface = "org.bluez.GattManager1";
        public const string bluezDeviceInterface = "org.bluez.Device1";
        public const string bluezGattCharacteristicInterface = "org.bluez.GattCharacteristic1";

        private class BluezInterfaceMetadata
        {
            public required string BluezInterface { get; init; }
            public required bool Match { get; init; }
        }

        private ConcurrentDictionary<Tmds.DBus.ObjectPath, IEnumerable<BluezInterfaceMetadata>> _bluezObjectPathInterfaces = new();
        private bluez.DBus.IAdapter1? _bluezAdapterProxy = null;
        private Tmds.DBus.ObjectPath? _deviceObjectPath = null;
        private bluez.DBus.IDevice1? _deviceProxy = null;
        private readonly string _deviceName;
        private readonly Guid _deviceUuid;

        //private readonly ScalextricArcState _scalextricArcState;
        //private readonly Channel<CarIdState> _carIdStateChannel;
        //private readonly Channel<CommandState> _commandStateChannel;
        //private readonly Channel<ConnectDto> _connectionChannel;
        //private readonly Channel<ThrottleProfileState> _throttleProfileStateChannel;
        private readonly ILogger<BluezClientServiceBase> _logger;

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _bluezDiscoveryTask;
        //private Task? _carIdTask;
        //private Task? _commandTask;
        //private Task? _throttleProfileTask;
        //private Task? _connectionTask;


        private bool _discoveryStarted = false;


        public BluezClientServiceBase(string deviceName,
                                      Guid deviceUuid,
                                      //ScalextricArcState scalextricArcState,
                                      //Channel<CarIdState> carIdStateChannel,
                                      //Channel<CommandState> commandStateChannel,
                                      //Channel<ConnectDto> connectionChannel,
                                      //Channel<ThrottleProfileState> throttleProfileStateChannel,
                                      ILogger<BluezClientServiceBase> logger)
        {
            _deviceName = deviceName;
            _deviceUuid = deviceUuid;
            //_scalextricArcState = scalextricArcState;
            //_carIdStateChannel = carIdStateChannel;
            //_commandStateChannel = commandStateChannel;
            //_connectionChannel = connectionChannel;
            //_throttleProfileStateChannel = throttleProfileStateChannel;
            _logger = logger;
        }


        private BluezClientBluetoothConnectionStateType _bluetoothConnectionState;
        public BluezClientBluetoothConnectionStateType BluetoothConnectionState => _bluetoothConnectionState;


        private Device1Properties _deviceProperties;
        public Device1Properties DeviceProperties => _deviceProperties;

        protected List<BluezClientGattCharacteristic> _gattCharacteristics = new();
        public IEnumerable<BluezClientGattCharacteristic> GattCharacteristics => _gattCharacteristics;


        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Task? watchInterfacesAddedTask = null;
            Task? watchInterfacesRemovedTask = null;

            await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);

            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                _logger.LogCritical("You need to be running Linux for this application.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                _bluezObjectPathInterfaces = new();
                _deviceObjectPath = null;
                _deviceProxy = null;
                //_slotCharacteristicWatchTask = null;
                //_throttleCharacteristicWatchTask = null;
                _discoveryStarted = false;

                try
                {
                    // Find all D-Bus objects and their interfaces
                    var objectManager = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IObjectManager>(bluezService, Tmds.DBus.ObjectPath.Root);
                    var dBusObjects = await objectManager.GetManagedObjectsAsync();
                    foreach (var dBusObject in dBusObjects)
                    {
                        InterfaceAdded((dBusObject.Key, dBusObject.Value));
                    }

                    var bluezAdapterObjectPathKp = _bluezObjectPathInterfaces.SingleOrDefault(x => x.Value.Any(i => i.BluezInterface == bluezAdapterInterface));

                    if (string.IsNullOrEmpty(bluezAdapterObjectPathKp.Key.ToString()))
                    {
                        _logger.LogCritical($"{bluezAdapterInterface} does not exist. Please install BlueZ (and an adapter for the needed Bluetooth Low Energy functionality), and then re-start this application.");
                        await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Disabled);
                        return;
                    }

                    _bluezAdapterProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IAdapter1>(bluezService, bluezAdapterObjectPathKp.Key);

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

                    _logger.LogInformation($"BlueZ initialization done. Trying to find the devive...");
                    await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Enabled);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var deviceObjectPathKps = _bluezObjectPathInterfaces.Where(x => x.Value.Any(i => i.Match));

                        if (!deviceObjectPathKps.Any())
                        {
                            await ResetAsync(_deviceObjectPath);

                            if (await _bluezAdapterProxy.GetDiscoveringAsync())
                            {
                                _logger.LogInformation("Searching...");
                            }
                            else
                            {
                                // var discoveryProperties = new Dictionary<string, object>
                                // {
                                //     {
                                //         "UUIDs",
                                //         new string[] { "00003b08-0000-1000-8000-00805f9b34fb" }
                                //     }
                                // };
                                // await _bluezAdapterProxy.SetDiscoveryFilterAsync(discoveryProperties);
                                _logger.LogInformation("Starting Bluetooth device discovery.");
                                await _bluezAdapterProxy.StartDiscoveryAsync();
                                _discoveryStarted = true;
                            }
                            await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Discovering);
                        }
                        else
                        {
                            // Bluetooth device discovery not needed, device already found.
                            if (_deviceProxy is not null && await _bluezAdapterProxy.GetDiscoveringAsync() && _discoveryStarted)
                            {
                                _logger.LogInformation("Stopping Bluetooth device discovery.");
                                await _bluezAdapterProxy.StopDiscoveryAsync();
                            }

                            if (deviceObjectPathKps.Count() >= 2)
                            {
                                _logger.LogInformation($"{deviceObjectPathKps.Count()} devices found.");
                            }
                            await CheckObjectPathAsync(deviceObjectPathKps.First().Key, cancellationToken);
                        }

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                        }
                    }

                    if (watchInterfacesAddedTask is not null)
                    {
                        watchInterfacesAddedTask.Dispose();
                    }
                    if (watchInterfacesRemovedTask is not null)
                    {
                        watchInterfacesRemovedTask.Dispose();
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

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                }
            }

            await ResetAsync(_deviceObjectPath);
        }


        private void InterfaceAdded((Tmds.DBus.ObjectPath objectPath, IDictionary<string, IDictionary<string, object>> interfaces) args)
        {
            Console.WriteLine($"{args.objectPath} added.");
            // Console.WriteLine($"{args.objectPath} added with the following interfaces...");
            // foreach (var iface in args.interfaces)
            // {
            //     Console.WriteLine(iface.Key);
            // }


            if (args.interfaces.Keys.Any(x => x.StartsWith(bluezService)))
            {
                var bluezInterfaceMetadata = new List<BluezInterfaceMetadata>();
                foreach (var item in args.interfaces.Where(x => x.Key.StartsWith(bluezService)))
                {
                    //if (item.Key == bluezAdapterInterface)
                    //{
                        var match = item.Value.Any(x => x.Key == "Name" && x.Value.ToString() == _deviceName);
                        //if (args.interfaces.Any(x => x.Key == bluezGattManagerInterface))
                        //{
                            // Console.WriteLine($"    {item.Key}");
                            // foreach (var kp in item.Value)
                            // {
                            //     Console.WriteLine($"        {kp.Key}={kp.Value}");
                            // }

                            // var uuids = (String[])item.Value.SingleOrDefault(x => x.Key == "UUIDs").Value;
                            // if (uuids is not null)
                            // {
                            //     match = uuids.Contains(_deviceUuid.ToString().ToLower());
                            // }

                        //}

                        if (match)
                        {
                            if (args.interfaces.Any(x => x.Key == bluezDeviceInterface))
                            {
                                LogDBusObject(args.objectPath, args.interfaces);
                            }

                        }

                        Console.WriteLine($"        Match={match}");
                        bluezInterfaceMetadata.Add(new BluezInterfaceMetadata
                        {
                            BluezInterface = item.Key,
                            Match = match                            
                        });
                    //}
                }

                _bluezObjectPathInterfaces.TryAdd(args.objectPath, bluezInterfaceMetadata);
            }
        }


        private void InterfaceRemoved((Tmds.DBus.ObjectPath objectPath, string[] interfaces) args)
        {
            Console.WriteLine($"{args.objectPath} removed.");
            _bluezObjectPathInterfaces.TryRemove(args.objectPath, out IEnumerable<BluezInterfaceMetadata>? bluezInterfaceMetadata);
        }


        private async Task CheckObjectPathAsync(Tmds.DBus.ObjectPath objectPath, CancellationToken cancellationToken)
        {
            if (_deviceObjectPath is null)
            {
                try
                {
                    _deviceProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IDevice1>(bluezService, objectPath);
                    var deviceProxyName = (await _deviceProxy.GetNameAsync()).Trim();

                    if (await _deviceProxy.GetConnectedAsync())
                    {
                        _logger.LogInformation($"{deviceProxyName} already connected.");
                        _deviceObjectPath = objectPath;
                    }
                    else
                    {
                        _logger.LogInformation($"Connecting to {deviceProxyName}... ({await _deviceProxy.GetAddressAsync()})");
                        bool success = false;
                        for (int i = 1; i <= 5; i++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            try
                            {
                                await _deviceProxy.ConnectAsync();
                                success = true;
                                break;
                            }
                            catch (Tmds.DBus.DBusException exception)
                            {
                                _logger.LogWarning($"Connection attempt {i}(5) failed: {exception.ErrorName}, {exception.ErrorMessage}");
                                try
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                                }
                                catch (TaskCanceledException)
                                {
                                }
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                        }
                        if (success)
                        {
                            _deviceObjectPath = objectPath;
                            await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Connected);
                            _logger.LogInformation($"Connected to {deviceProxyName}.");
                        }
                        else
                        {
                            await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Enabled);
                            _logger.LogError($"Could not connect to {deviceProxyName}.");
                            await ResetAsync(objectPath);
                            return;
                        }
                    }

                    if (_deviceObjectPath != null)
                    {
                        _logger.LogInformation($"Initiating {deviceProxyName} services...");

                        _deviceProperties = await _deviceProxy.GetAllAsync();
                        await DevicePropertiesChangedAsync();
                        await _deviceProxy.WatchPropertiesAsync(DevicePropertiesWatchProperties);

                        if (!await _deviceProxy.GetServicesResolvedAsync())
                        {
                            for (int i = 1; i <= 5; i++)
                            {
                                _logger.LogInformation($"Waiting for {deviceProxyName} services to be resolved...");
                                if (await _deviceProxy.GetServicesResolvedAsync())
                                {
                                    break;
                                }

                                try
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                                }
                                catch (TaskCanceledException)
                                {
                                }
                            }

                            if (!await _deviceProxy.GetServicesResolvedAsync())
                            {
                                await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Enabled);
                                _logger.LogWarning($"{deviceProxyName} services could not be resolved");
                                await ResetAsync(_deviceObjectPath);
                                return;
                            }
                        }

                        //    _scalextricArcState.GattCharacteristics = new();

                        _gattCharacteristics = new();

                        foreach (var item in _bluezObjectPathInterfaces.Where(x => x.Key.ToString().StartsWith(_deviceObjectPath.ToString()!) && x.Value.Any(i => i.BluezInterface == bluezGattCharacteristicInterface)).OrderBy(x => x.Key))
                        {
                            var proxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IGattCharacteristic1>(bluezService, item.Key);
                            var properties = await proxy.GetAllAsync();
                            Console.WriteLine($"{item.Key} {string.Join(", ", item.Value.Select(x => x.BluezInterface))} {properties.UUID}  {string.Join(", ", properties.Flags)}");

                            await GattCharacteristicResolvedAsync(properties);
                        }
                        await _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType.Initialized);
                        _logger.LogInformation($"{deviceProxyName} services have been initialized.");
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, exception.Message);
                }
            }
        }

        
        protected abstract Task GattCharacteristicResolvedAsync(GattCharacteristic1Properties properties);


        private async Task ResetAsync(Tmds.DBus.ObjectPath? objectPath)
        {
            //_carIdCharacteristicProxy = null;

            //_commandCharacteristicProxy = null;

            //if (_slotCharacteristicWatchTask is not null)
            //{
            //    _slotCharacteristicWatchTask.Dispose();
            //    _slotCharacteristicWatchTask = null;
            //}

            //_slotCharacteristicProxy = null;

            //if (_throttleCharacteristicWatchTask is not null)
            //{
            //    _throttleCharacteristicWatchTask.Dispose();
            //    _throttleCharacteristicWatchTask = null;
            //}

            //_throttleCharacteristicProxy = null;

            //_throttleProfile1CharacteristicProxy = null;
            //_throttleProfile2CharacteristicProxy = null;
            //_throttleProfile3CharacteristicProxy = null;
            //_throttleProfile4CharacteristicProxy = null;
            //_throttleProfile5CharacteristicProxy = null;
            //_throttleProfile6CharacteristicProxy = null;

            //if (_trackCharacteristicWatchTask is not null)
            //{
            //    _trackCharacteristicWatchTask.Dispose();
            //    _trackCharacteristicWatchTask = null;
            //}

            //_trackCharacteristicProxy = null;

            string? deviceProxyName = null; ;

            if (_deviceProxy is not null)
            {
                if (await _deviceProxy.GetConnectedAsync())
                {
                    deviceProxyName = (await _deviceProxy.GetNameAsync()).Trim();
                    try
                    {
                        _logger.LogInformation($"Disconnecting {deviceProxyName}...");
                        await _deviceProxy.DisconnectAsync();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, exception.Message);
                    }
                    _logger.LogInformation($"{deviceProxyName} disconnected.");
                }

                _deviceProxy = null;
            }

            if (_bluezAdapterProxy is not null && objectPath.HasValue)
            {
                try
                {
                    _logger.LogInformation($"Removing {deviceProxyName}...");
                    await _bluezAdapterProxy.RemoveDeviceAsync(objectPath.Value);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, exception.Message);
                }
                _logger.LogInformation($"{deviceProxyName} removed.");
            }

            _deviceObjectPath = null;
        }


        //private void slotCharacteristicWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        //{
        //    foreach (var item in propertyChanges.Changed)
        //    {
        //        if (item.Key == "Value")
        //        {
        //            var value = (byte[])item.Value;
        //            _scalextricArcState.SlotStates[value[1] - 1].SetAsync
        //            (
        //                value[0],
        //                (uint)(value[2] + value[3] * 256 + value[4] * 65536 + value[5] * 16777216),
        //                (uint)(value[6] + value[7] * 256 + value[8] * 65536 + value[9] * 16777216),
        //                (uint)(value[10] + value[11] * 256 + value[12] * 65536 + value[13] * 16777216),
        //                (uint)(value[14] + value[15] * 256 + value[16] * 65536 + value[17] * 16777216)
        //            ).Wait();
        //        }
        //    }
        //}


        //private void throttleCharacteristicWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        //{
        //    foreach (var item in propertyChanges.Changed)
        //    {
        //        if (item.Key == "Value")
        //        {
        //            var value = (byte[])item.Value;
        //            _scalextricArcState.ThrottleState!.SetAsync
        //            (
        //                value[0],
        //                value[12],
        //                value[13],
        //                (value[11] & 0b1) > 0,
        //                (byte)(value[1] & 0b111111),
        //                (value[1] & 0b1000000) > 0,
        //                (value[1] & 0b10000000) > 0,
        //                (value[11] & 0b100) > 0,
        //                value[14],
        //                (byte)(value[2] & 0b111111),
        //                (value[2] & 0b1000000) > 0,
        //                (value[2] & 0b10000000) > 0,
        //                (value[11] & 0b1000) > 0,
        //                value[15],
        //                (byte)(value[3] & 0b111111),
        //                (value[3] & 0b1000000) > 0,
        //                (value[3] & 0b10000000) > 0,
        //                (value[11] & 0b10000) > 0,
        //                value[16],
        //                (byte)(value[4] & 0b111111),
        //                (value[4] & 0b1000000) > 0,
        //                (value[4] & 0b10000000) > 0,
        //                (value[11] & 0b100000) > 0,
        //                value[17],
        //                (byte)(value[5] & 0b111111),
        //                (value[5] & 0b1000000) > 0,
        //                (value[5] & 0b10000000) > 0,
        //                (value[11] & 0b1000000) > 0,
        //                value[18],
        //                (byte)(value[6] & 0b111111),
        //                (value[6] & 0b1000000) > 0,
        //                (value[6] & 0b10000000) > 0,
        //                (value[11] & 0b10000000) > 0,
        //                value[19],
        //                (uint)(value[7] + value[8] * 256 + value[9] * 65536 + value[10] * 16777216)
        //            ).Wait();
        //        }
        //    }
        //}


        //private void trackCharacteristicWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        //{
        //    foreach (var item in propertyChanges.Changed)
        //    {
        //        if (item.Key == "Value")
        //        {
        //            var value = (byte[])item.Value;
        //            _scalextricArcState.TrackState!.SetAsync
        //            (
        //                value[0],
        //                value[1],
        //                value[2],
        //                (uint)(value[3] + value[4] * 256 + value[5] * 65536 + value[6] * 16777216)
        //            );
        //        }
        //    }
        //}

        private Task _bluetoothConnectionStateChangedAsync(BluezClientBluetoothConnectionStateType bluezClientBluetoothConnectionStateType)
        {
            _bluetoothConnectionState = bluezClientBluetoothConnectionStateType;
            return BluetoothConnectionStateChangedAsync();
        }


        protected abstract Task BluetoothConnectionStateChangedAsync();

        protected abstract Task DevicePropertiesChangedAsync();


        private void DevicePropertiesWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        {
            foreach (var item in propertyChanges.Changed)
            {
                Console.WriteLine($"Property change: {item.Key}={item.Value}");
                switch (item.Key)
                {
                    case nameof(Device1Properties.Address):
                        _deviceProperties.Address = item.Value.ToString()!;
                        break;

                    case nameof(Device1Properties.AddressType):
                        _deviceProperties.AddressType = item.Value.ToString()!;
                        break;

                    case nameof(Device1Properties.Paired):
                        _deviceProperties.Paired = (bool)item.Value;
                        break;

                    case nameof(Device1Properties.Trusted):
                        _deviceProperties.Trusted = (bool)item.Value;
                        break;

                    case nameof(Device1Properties.Blocked):
                        _deviceProperties.Blocked = (bool)item.Value;
                        break;

                    case nameof(Device1Properties.LegacyPairing):
                        _deviceProperties.LegacyPairing = (bool)item.Value;
                        break;

                    case nameof(Device1Properties.WakeAllowed):
                        _deviceProperties.WakeAllowed = (bool)item.Value;
                        break;

                    case nameof(Device1Properties.RSSI):
                        _deviceProperties.RSSI = (short)item.Value;
                        break;

                    case nameof(Device1Properties.TxPower):
                        _deviceProperties.TxPower = (short)item.Value;
                        break;

                    case nameof(Device1Properties.Connected):
                        _deviceProperties.Connected = (bool)item.Value;
                        break;

                    case nameof(Device1Properties.ServicesResolved):
                        _deviceProperties.ServicesResolved = (bool)item.Value;
                        break;

                    default:
                        _logger.LogWarning($"Unhandled property change: {item.Key}={item.Value}");
                        break;
                }
            }
            DevicePropertiesChangedAsync().Wait();
        }



        private void LogDBusObject(Tmds.DBus.ObjectPath objectPath, IDictionary<string, IDictionary<string, object>> interfaces)
        {
            Console.WriteLine("===================================================================");
            Console.WriteLine($"objectPath={objectPath}");
            foreach (var iface in interfaces)
            {
                Console.WriteLine("----------------------------------------------------------------");
                Console.WriteLine($"interface={iface.Key}");
                if (iface.Value.Any())
                {
                    Console.WriteLine("Properties:");
                    foreach (var prop in iface.Value)
                    {
                        switch (prop.Value)
                        {
                            case string[]:
                                Console.WriteLine($"Values for property={prop.Key}:");
                                foreach (var item in (string[])prop.Value)
                                {
                                    Console.WriteLine(item);
                                }
                                break;

                            case Dictionary<ushort, object>:
                                Console.WriteLine($"Values for property={prop.Key}:");
                                foreach (var item in (Dictionary<ushort, object>)prop.Value)
                                {
                                    Console.WriteLine($"{item.Key}={item.Value}");
                                }
                                break;

                            case Dictionary<string, object>:
                                Console.WriteLine($"Values for property={prop.Key}:");
                                foreach (var item in (Dictionary<string, object>)prop.Value)
                                {
                                    Console.WriteLine($"{item.Key}={item.Value}");
                                }
                                break;

                            default:
                                Console.WriteLine($"{prop.Key}=#{prop.Value}#");
                                break;
                        }
                    }
                }
            }
        }
    }
}
