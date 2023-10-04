using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using static bluez.DBus.Adapter1Extensions;
using static bluez.DBus.Device1Extensions;
using bluez.DBus;


namespace RazManagerIO.Host.Services.Utilities
{
    public class BluezClientServiceBase
    {
        public const string bluezService = "org.bluez";
        public const string bluezAdapterInterface = "org.bluez.Adapter1";
        public const string bluezGattManagerInterface = "org.bluez.GattManager1";
        public const string bluezDeviceInterface = "org.bluez.Device1";
        public const string bluezGattCharacteristicInterface = "org.bluez.GattCharacteristic1";

        private class BluezInterfaceMetadata
        {
            public string BluezInterface { get; init; } = null!;
            public string? DeviceName { get; init; }
        }

        private ConcurrentDictionary<Tmds.DBus.ObjectPath, IEnumerable<BluezInterfaceMetadata>> _bluezObjectPathInterfaces = new();
        private bluez.DBus.IAdapter1? _bluezAdapterProxy = null;
        private Tmds.DBus.ObjectPath? _scalextricArcObjectPath = null;
        private bluez.DBus.IDevice1? _scalextricArcProxy = null;


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


        public BluezClientServiceBase(ScalextricArcState scalextricArcState,
                                      Channel<CarIdState> carIdStateChannel,
                                      Channel<CommandState> commandStateChannel,
                                      Channel<ConnectDto> connectionChannel,
                                      Channel<ThrottleProfileState> throttleProfileStateChannel,
                                      ILogger<BluezClientServiceBase> logger)
        {
            //_scalextricArcState = scalextricArcState;
            //_carIdStateChannel = carIdStateChannel;
            //_commandStateChannel = commandStateChannel;
            //_connectionChannel = connectionChannel;
            //_throttleProfileStateChannel = throttleProfileStateChannel;
            _logger = logger;
        }


        //public async Task StartAsync(CancellationToken cancellationToken)
        //{
        //    if (_scalextricArcState.ConnectionState.Connect)
        //    {
        //        _cancellationTokenSource = new CancellationTokenSource();

        //        _bluezDiscoveryTask = BluezDiscoveryAsync(_cancellationTokenSource.Token);
        //        _carIdTask = CarIdAsync(_cancellationTokenSource.Token);
        //        _commandTask = CommandAsync(_cancellationTokenSource.Token);
        //        _throttleProfileTask = ThrottleProfileAsync(_cancellationTokenSource.Token);
        //    }
        //    else
        //    {
        //        await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Disabled);
        //    }

        //    if (_connectionTask is null)
        //    {
        //        _connectionTask = ConnectionAsync(cancellationToken);
        //    }
        //}


        //public async Task StopAsync(CancellationToken cancellationToken)
        //{
        //    if (_cancellationTokenSource is not null)
        //    {
        //        _cancellationTokenSource.Cancel();

        //        if (_bluezDiscoveryTask is not null)
        //        {
        //            try
        //            {
        //                _bluezDiscoveryTask.Wait();
        //            }
        //            catch (Exception)
        //            {
        //            }
        //        }
        //        if (_carIdTask is not null)
        //        {
        //            try
        //            {
        //                _carIdTask.Wait();
        //            }
        //            catch (Exception)
        //            {
        //            }
        //        }
        //        if (_commandTask is not null)
        //        {
        //            try
        //            {
        //                _commandTask.Wait();
        //            }
        //            catch (Exception)
        //            {
        //            }
        //        }
        //        if (_throttleProfileTask is not null)
        //        {
        //            try
        //            {
        //                _throttleProfileTask.Wait();
        //            }
        //            catch (Exception)
        //            {
        //            }
        //        }
        //        Console.WriteLine("Tasks have been completed.");
        //    }

        //    await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Disabled);
        //}

        private BluezClientBluetoothConnectionStateType _bluetoothConnectionState;
        public BluezClientBluetoothConnectionStateType BluetoothConnectionState => _bluetoothConnectionState;


        private List<BluezClientGattCharacteristic> _gattCharacteristics = new();
        public IEnumerable<BluezClientGattCharacteristic> GattCharacteristics => _gattCharacteristics;


        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Task? watchInterfacesAddedTask = null;
            Task? watchInterfacesRemovedTask = null;

            //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Disabled);

            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                _logger.LogCritical("You need to be running Linux for this application.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                _bluezObjectPathInterfaces = new();
                _scalextricArcObjectPath = null;
                _scalextricArcProxy = null;
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
                        //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Disabled);
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

                    _logger.LogInformation("BlueZ initialization done. Trying to find a Scalextric ARC powerbase...");
                    //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Enabled);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var scalextricArcObjectPathKps = _bluezObjectPathInterfaces.Where(x => x.Value.Any(i => i.BluezInterface == bluezDeviceInterface && !string.IsNullOrEmpty(i.DeviceName) && i.DeviceName == "Scalextric ARC"));

                        if (!scalextricArcObjectPathKps.Any())
                        {
                            await ResetAsync(_scalextricArcObjectPath);

                            if (await _bluezAdapterProxy.GetDiscoveringAsync())
                            {
                                _logger.LogInformation("Searching...");
                            }
                            else
                            {
                                var discoveryProperties = new Dictionary<string, object>
                                {
                                    {
                                        "UUIDs",
                                        new string[] { "00003b08-0000-1000-8000-00805f9b34fb" }
                                    }
                                };
                                await _bluezAdapterProxy.SetDiscoveryFilterAsync(discoveryProperties);
                                _logger.LogInformation("Starting Bluetooth device discovery.");
                                await _bluezAdapterProxy.StartDiscoveryAsync();
                                _discoveryStarted = true;
                            }
                            //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Discovering);
                        }
                        else
                        {
                            // Bluetooth device discovery not needed, device already found.
                            if (_scalextricArcProxy is not null && await _bluezAdapterProxy.GetDiscoveringAsync() && _discoveryStarted)
                            {
                                _logger.LogInformation("Stopping Bluetooth device discovery.");
                                await _bluezAdapterProxy.StopDiscoveryAsync();
                            }

                            if (scalextricArcObjectPathKps.Count() >= 2)
                            {
                                _logger.LogInformation($"{scalextricArcObjectPathKps.Count()} Scalextric ARC powerbases found.");
                            }
                            await ScalextricArcChangedAsync(scalextricArcObjectPathKps.First().Key, cancellationToken);
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

            await ResetAsync(_scalextricArcObjectPath);
        }


        private void InterfaceAdded((Tmds.DBus.ObjectPath objectPath, IDictionary<string, IDictionary<string, object>> interfaces) args)
        {
            Console.WriteLine($"{args.objectPath} added.");
            //Console.WriteLine($"{args.objectPath} added with the following interfaces...");
            //foreach (var iface in args.interfaces)
            //{
            //    Console.WriteLine(iface.Key);
            //}

            if (args.interfaces.Any(x => x.Key == bluezDeviceInterface))
            {
                LogDBusObject(args.objectPath, args.interfaces);
            }

            if (args.interfaces.Keys.Any(x => x.StartsWith(bluezService)))
            {
                var bluezInterfaceMetadata = new List<BluezInterfaceMetadata>();
                foreach (var item in args.interfaces.Where(x => x.Key.StartsWith(bluezService)))
                {
                    if (item.Key == bluezAdapterInterface && args.interfaces.Any(x => x.Key == bluezGattManagerInterface) ||
                        item.Key != bluezAdapterInterface)
                    {
                        bluezInterfaceMetadata.Add(new BluezInterfaceMetadata
                        {
                            BluezInterface = item.Key,
                            DeviceName = item.Value.SingleOrDefault(x => item.Key == bluezDeviceInterface && x.Key == "Name").Value?.ToString()?.Trim()
                        });
                    }
                }

                _bluezObjectPathInterfaces.TryAdd(args.objectPath, bluezInterfaceMetadata);
            }
        }


        private void InterfaceRemoved((Tmds.DBus.ObjectPath objectPath, string[] interfaces) args)
        {
            Console.WriteLine($"{args.objectPath} removed.");
            _bluezObjectPathInterfaces.TryRemove(args.objectPath, out IEnumerable<BluezInterfaceMetadata>? bluezInterfaceMetadata);
        }


        private async Task ScalextricArcChangedAsync(Tmds.DBus.ObjectPath objectPath, CancellationToken cancellationToken)
        {
            if (_scalextricArcObjectPath is null)
            {
                try
                {
                    _scalextricArcProxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IDevice1>(bluezService, objectPath);

                    if (await _scalextricArcProxy.GetConnectedAsync())
                    {
                        _logger.LogInformation("Scalextric ARC already connected.");
                        _scalextricArcObjectPath = objectPath;
                    }
                    else
                    {
                        _logger.LogInformation($"Connecting to {(await _scalextricArcProxy.GetNameAsync()).Trim()}... ({await _scalextricArcProxy.GetAddressAsync()})");
                        bool success = false;
                        for (int i = 1; i <= 5; i++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            try
                            {
                                await _scalextricArcProxy.ConnectAsync();
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
                            _scalextricArcObjectPath = objectPath;
                            //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Connected);
                            _logger.LogInformation("Connected to Scalextric ARC.");
                        }
                        else
                        {
                            //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Enabled);
                            _logger.LogError("Could not connect to Scalextric ARC.");
                            await ResetAsync(objectPath);
                            return;
                        }
                    }

                    if (_scalextricArcObjectPath != null)
                    {
                        _logger.LogInformation("Initiating Scalextric ARC services.");

                        var deviceProperties = await _scalextricArcProxy.GetAllAsync();
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Address), deviceProperties.Address);
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.AddressType), deviceProperties.AddressType);
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Class), deviceProperties.Class.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Appearance), deviceProperties.Appearance.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Icon), deviceProperties.Icon);
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Paired), deviceProperties.Paired.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Trusted), deviceProperties.Trusted.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Blocked), deviceProperties.Blocked.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.LegacyPairing), deviceProperties.LegacyPairing.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.RSSI), deviceProperties.RSSI.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Connected), deviceProperties.Connected.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.UUIDs), string.Join(", ", deviceProperties.UUIDs!));
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Modalias), deviceProperties.Modalias);
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Adapter), deviceProperties.Adapter.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.TxPower), deviceProperties.TxPower.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.ServicesResolved), deviceProperties.ServicesResolved.ToString());
                        await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.WakeAllowed), deviceProperties.WakeAllowed.ToString());
                        await _scalextricArcProxy.WatchPropertiesAsync(scalextricArcWatchProperties);

                        if (!await _scalextricArcProxy.GetServicesResolvedAsync())
                        {
                            for (int i = 1; i <= 5; i++)
                            {
                                _logger.LogInformation("Waiting for Scalextric ARC services to be resolved...");
                                if (await _scalextricArcProxy.GetServicesResolvedAsync())
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

                            if (!await _scalextricArcProxy.GetServicesResolvedAsync())
                            {
                                //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Enabled);
                                _logger.LogWarning("Scalextric ARC services could not be resolved");
                                await ResetAsync(_scalextricArcObjectPath);
                                return;
                            }
                        }

                        //Console.WriteLine("Bluez objects and interfaces");
                        //foreach (var item in _bluezObjectPathInterfaces)
                        //{
                        //    Console.WriteLine($"{item.Key} {string.Join(", ", item.Value.Select(x => x.BluezInterface))}");
                        //}

                        _scalextricArcState.GattCharacteristics = new();

                        foreach (var item in _bluezObjectPathInterfaces.Where(x => x.Key.ToString().StartsWith(_scalextricArcObjectPath.ToString()!) && x.Value.Any(i => i.BluezInterface == bluezGattCharacteristicInterface)).OrderBy(x => x.Key))
                        {
                            var proxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IGattCharacteristic1>(bluezService, item.Key);
                            var properties = await proxy.GetAllAsync();

                            var gattCharacteristic = new GattCharacteristic();

                            if (!string.IsNullOrEmpty(properties.UUID))
                            {
                                gattCharacteristic.uuid = properties.UUID;

                                switch (properties.UUID)
                                {
                                    case manufacturerNameCharacteristicUuid:
                                        gattCharacteristic.Name = "Manufacturer name";
                                        break;

                                    case modelNumberCharacteristicUuid:
                                        gattCharacteristic.Name = "Model number";
                                        break;

                                    case hardwareRevisionCharacteristicUuid:
                                        gattCharacteristic.Name = "Hardware revision";
                                        break;

                                    case firmwareRevisionCharacteristicUuid:
                                        gattCharacteristic.Name = "Firmware revision";
                                        break;

                                    case softwareRevisionCharacteristicUuid:
                                        gattCharacteristic.Name = "Software revision";
                                        break;

                                    case dfuControlPointCharacteristicUuid:
                                        gattCharacteristic.Name = "DFU control point";
                                        break;

                                    case dfuPacketCharacteristicUuid:
                                        gattCharacteristic.Name = "DFU packet";
                                        break;

                                    case dfuRevisionCharacteristicUuid:
                                        gattCharacteristic.Name = "DFU revision";
                                        break;

                                    case serviceChangedCharacteristicUuid:
                                        gattCharacteristic.Name = "Service changed";
                                        break;

                                    case carIdCharacteristicUuid:
                                        gattCharacteristic.Name = "Car ID";
                                        break;

                                    case commandCharacteristicUuid:
                                        gattCharacteristic.Name = "Command";
                                        break;

                                    case slotCharacteristicUuid:
                                        gattCharacteristic.Name = "Slot";
                                        break;

                                    case throttleCharacteristicUuid:
                                        gattCharacteristic.Name = "Throttle";
                                        break;

                                    case throttleProfile1CharacteristicUuid:
                                        gattCharacteristic.Name = "Throttle profile 1";
                                        break;

                                    case throttleProfile2CharacteristicUuid:
                                        gattCharacteristic.Name = "Throttle profile 2";
                                        break;

                                    case throttleProfile3CharacteristicUuid:
                                        gattCharacteristic.Name = "Throttle profile 3";
                                        break;

                                    case throttleProfile4CharacteristicUuid:
                                        gattCharacteristic.Name = "Throttle profile 4";
                                        break;

                                    case throttleProfile5CharacteristicUuid:
                                        gattCharacteristic.Name = "Throttle profile 5";
                                        break;

                                    case throttleProfile6CharacteristicUuid:
                                        gattCharacteristic.Name = "Throttle profile 6";
                                        break;

                                    case trackCharacteristicUuid:
                                        gattCharacteristic.Name = "Track";
                                        break;

                                    default:
                                        break;
                                }
                            }

                            if (properties.Flags is not null)
                            {
                                foreach (var flag in properties.Flags)
                                {
                                    if (!string.IsNullOrEmpty(flag))
                                    {
                                        gattCharacteristic.Flags.Add(new BluezClientGattCharacteristicFlag
                                        {
                                            Flag = flag
                                        });
                                    }
                                }

                                if (properties.Flags!.Contains("read"))
                                {
                                    var value = await proxy.ReadValueAsync(new Dictionary<string, object>());
                                    gattCharacteristic.Length = value.Length;
                                    if (value.Length > 0)
                                    {
                                        if (!value.Any(x => x < 32))
                                        {
                                            gattCharacteristic.Value = System.Text.Encoding.UTF8.GetString(value);
                                            if (properties.UUID == modelNumberCharacteristicUuid)
                                            {
                                                await _scalextricArcState.ConnectionState.SetModelNumberAsync(gattCharacteristic.Value);
                                            }
                                        }

                                        if (properties.UUID == carIdCharacteristicUuid)
                                        {
                                            await _scalextricArcState.CarIdState.SetAsync
                                            (
                                                value[0],
                                                false
                                            );
                                        }

                                        if (properties.UUID == commandCharacteristicUuid)
                                        {
                                            await _scalextricArcState.CommandState.SetAsync
                                            (
                                                (CommandType)value[0],
                                                (byte)(value[1] & 0b111111),
                                                (value[1] & 0b1000000) > 0,
                                                (value[1] & 0b10000000) > 0,
                                                value[7],
                                                value[13],
                                                (value[19] & 0b1) > 0,
                                                (byte)(value[2] & 0b111111),
                                                (value[2] & 0b1000000) > 0,
                                                (value[2] & 0b10000000) > 0,
                                                value[8],
                                                value[14],
                                                (value[19] & 0b10) > 0,
                                                (byte)(value[3] & 0b111111),
                                                (value[3] & 0b1000000) > 0,
                                                (value[3] & 0b10000000) > 0,
                                                value[9],
                                                value[15],
                                                (value[19] & 0b100) > 0,
                                                (byte)(value[4] & 0b111111),
                                                (value[4] & 0b1000000) > 0,
                                                (value[4] & 0b10000000) > 0,
                                                value[10],
                                                value[16],
                                                (value[19] & 0b1000) > 0,
                                                (byte)(value[5] & 0b111111),
                                                (value[5] & 0b1000000) > 0,
                                                (value[5] & 0b10000000) > 0,
                                                value[11],
                                                value[17],
                                                (value[19] & 0b10000) > 0,
                                                (byte)(value[6] & 0b111111),
                                                (value[6] & 0b1000000) > 0,
                                                (value[6] & 0b10000000) > 0,
                                                value[12],
                                                value[18],
                                                (value[19] & 0b100000) > 0,
                                                false
                                            );
                                        }

                                        if (properties.UUID == trackCharacteristicUuid)
                                        {
                                            await _scalextricArcState.TrackState.SetAsync
                                            (
                                                value[0],
                                                value[1],
                                                value[2],
                                                (uint)(value[3] + value[4] * 256 + value[5] * 65536 + value[6] * 16777216)
                                            );
                                        }
                                    }
                                }
                            }

                            if (properties.UUID == carIdCharacteristicUuid)
                            {
                                _carIdCharacteristicProxy = proxy;
                            }

                            if (properties.UUID == commandCharacteristicUuid)
                            {
                                _commandCharacteristicProxy = proxy;
                            }

                            if (properties.UUID == slotCharacteristicUuid)
                            {
                                _slotCharacteristicProxy = proxy;
                                await _slotCharacteristicProxy.StartNotifyAsync();
                                _slotCharacteristicWatchTask = _slotCharacteristicProxy.WatchPropertiesAsync(slotCharacteristicWatchProperties);
                            }

                            if (properties.UUID == throttleCharacteristicUuid)
                            {
                                _throttleCharacteristicProxy = proxy;
                                await _throttleCharacteristicProxy.StartNotifyAsync();
                                _throttleCharacteristicWatchTask = _throttleCharacteristicProxy.WatchPropertiesAsync(throttleCharacteristicWatchProperties);
                            }

                            if (properties.UUID == throttleProfile1CharacteristicUuid)
                            {
                                _throttleProfile1CharacteristicProxy = proxy;
                            }

                            if (properties.UUID == throttleProfile2CharacteristicUuid)
                            {
                                _throttleProfile2CharacteristicProxy = proxy;
                            }

                            if (properties.UUID == throttleProfile3CharacteristicUuid)
                            {
                                _throttleProfile3CharacteristicProxy = proxy;
                            }

                            if (properties.UUID == throttleProfile4CharacteristicUuid)
                            {
                                _throttleProfile4CharacteristicProxy = proxy;
                            }

                            if (properties.UUID == throttleProfile5CharacteristicUuid)
                            {
                                _throttleProfile5CharacteristicProxy = proxy;
                            }

                            if (properties.UUID == throttleProfile6CharacteristicUuid)
                            {
                                _throttleProfile6CharacteristicProxy = proxy;
                            }

                            if (properties.UUID == trackCharacteristicUuid)
                            {
                                _trackCharacteristicProxy = proxy;
                                await _trackCharacteristicProxy.StartNotifyAsync();
                                _trackCharacteristicWatchTask = _trackCharacteristicProxy.WatchPropertiesAsync(trackCharacteristicWatchProperties);
                            }

                            _scalextricArcState.GattCharacteristics.Add(gattCharacteristic);
                        }

                        await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Initialized);
                        _logger.LogInformation("Scalextric ARC services have been initialized.");
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, exception.Message);
                }
            }
        }


        private async Task ResetAsync(Tmds.DBus.ObjectPath? objectPath)
        {
            _carIdCharacteristicProxy = null;

            _commandCharacteristicProxy = null;

            if (_slotCharacteristicWatchTask is not null)
            {
                _slotCharacteristicWatchTask.Dispose();
                _slotCharacteristicWatchTask = null;
            }

            _slotCharacteristicProxy = null;

            if (_throttleCharacteristicWatchTask is not null)
            {
                _throttleCharacteristicWatchTask.Dispose();
                _throttleCharacteristicWatchTask = null;
            }

            _throttleCharacteristicProxy = null;

            _throttleProfile1CharacteristicProxy = null;
            _throttleProfile2CharacteristicProxy = null;
            _throttleProfile3CharacteristicProxy = null;
            _throttleProfile4CharacteristicProxy = null;
            _throttleProfile5CharacteristicProxy = null;
            _throttleProfile6CharacteristicProxy = null;

            if (_trackCharacteristicWatchTask is not null)
            {
                _trackCharacteristicWatchTask.Dispose();
                _trackCharacteristicWatchTask = null;
            }

            _trackCharacteristicProxy = null;

            if (_scalextricArcProxy is not null)
            {
                if (await _scalextricArcProxy.GetConnectedAsync())
                {
                    try
                    {
                        _logger.LogInformation("Disconnecting Scalextric ARC...");
                        await _scalextricArcProxy.DisconnectAsync();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, exception.Message);
                    }
                    _logger.LogInformation("Scalextric ARC disconnected.");
                }

                _scalextricArcProxy = null;
            }

            if (_bluezAdapterProxy is not null && objectPath.HasValue)
            {
                try
                {
                    _logger.LogInformation("Removing Scalextric ARC...");
                    await _bluezAdapterProxy.RemoveDeviceAsync(objectPath.Value);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, exception.Message);
                }
                _logger.LogInformation("Scalextric ARC removed.");
            }

            _scalextricArcObjectPath = null;
        }

        private void scalextricArcWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        {
            foreach (var item in propertyChanges.Changed)
            {
                switch (item.Value)
                {
                    case string[]:
                        _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(item.Key, String.Join(", ", (string[])item.Value)).Wait();
                        break;

                    case Dictionary<ushort, object>:
                        break;

                    case Dictionary<string, object>:
                        break;

                    default:
                        _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(item.Key, item.Value.ToString()).Wait();
                        break;
                }
            }
        }

        private void slotCharacteristicWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        {
            foreach (var item in propertyChanges.Changed)
            {
                if (item.Key == "Value")
                {
                    var value = (byte[])item.Value;
                    _scalextricArcState.SlotStates[value[1] - 1].SetAsync
                    (
                        value[0],
                        (uint)(value[2] + value[3] * 256 + value[4] * 65536 + value[5] * 16777216),
                        (uint)(value[6] + value[7] * 256 + value[8] * 65536 + value[9] * 16777216),
                        (uint)(value[10] + value[11] * 256 + value[12] * 65536 + value[13] * 16777216),
                        (uint)(value[14] + value[15] * 256 + value[16] * 65536 + value[17] * 16777216)
                    ).Wait();
                }
            }
        }


        private void throttleCharacteristicWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        {
            foreach (var item in propertyChanges.Changed)
            {
                if (item.Key == "Value")
                {
                    var value = (byte[])item.Value;
                    _scalextricArcState.ThrottleState!.SetAsync
                    (
                        value[0],
                        value[12],
                        value[13],
                        (value[11] & 0b1) > 0,
                        (byte)(value[1] & 0b111111),
                        (value[1] & 0b1000000) > 0,
                        (value[1] & 0b10000000) > 0,
                        (value[11] & 0b100) > 0,
                        value[14],
                        (byte)(value[2] & 0b111111),
                        (value[2] & 0b1000000) > 0,
                        (value[2] & 0b10000000) > 0,
                        (value[11] & 0b1000) > 0,
                        value[15],
                        (byte)(value[3] & 0b111111),
                        (value[3] & 0b1000000) > 0,
                        (value[3] & 0b10000000) > 0,
                        (value[11] & 0b10000) > 0,
                        value[16],
                        (byte)(value[4] & 0b111111),
                        (value[4] & 0b1000000) > 0,
                        (value[4] & 0b10000000) > 0,
                        (value[11] & 0b100000) > 0,
                        value[17],
                        (byte)(value[5] & 0b111111),
                        (value[5] & 0b1000000) > 0,
                        (value[5] & 0b10000000) > 0,
                        (value[11] & 0b1000000) > 0,
                        value[18],
                        (byte)(value[6] & 0b111111),
                        (value[6] & 0b1000000) > 0,
                        (value[6] & 0b10000000) > 0,
                        (value[11] & 0b10000000) > 0,
                        value[19],
                        (uint)(value[7] + value[8] * 256 + value[9] * 65536 + value[10] * 16777216)
                    ).Wait();
                }
            }
        }


        private void trackCharacteristicWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        {
            foreach (var item in propertyChanges.Changed)
            {
                if (item.Key == "Value")
                {
                    var value = (byte[])item.Value;
                    _scalextricArcState.TrackState!.SetAsync
                    (
                        value[0],
                        value[1],
                        value[2],
                        (uint)(value[3] + value[4] * 256 + value[5] * 65536 + value[6] * 16777216)
                    );
                }
            }
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
