﻿using bluez.DBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

                    _logger.LogInformation($"BlueZ initialization done. Trying to find the devive...");
                    //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Enabled);

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
                            //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Discovering);
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
                            //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Connected);
                            _logger.LogInformation($"Connected to {deviceProxyName}.");
                        }
                        else
                        {
                            //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Enabled);
                            _logger.LogError($"Could not connect to {deviceProxyName}.");
                            await ResetAsync(objectPath);
                            return;
                        }
                    }

                    if (_deviceObjectPath != null)
                    {
                        _logger.LogInformation($"Initiating {deviceProxyName} services...");

                        //var deviceProperties = await _deviceProxy.GetAllAsync();
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Address), deviceProperties.Address);
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.AddressType), deviceProperties.AddressType);
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Class), deviceProperties.Class.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Appearance), deviceProperties.Appearance.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Icon), deviceProperties.Icon);
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Paired), deviceProperties.Paired.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Trusted), deviceProperties.Trusted.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Blocked), deviceProperties.Blocked.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.LegacyPairing), deviceProperties.LegacyPairing.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.RSSI), deviceProperties.RSSI.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Connected), deviceProperties.Connected.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.UUIDs), string.Join(", ", deviceProperties.UUIDs!));
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Modalias), deviceProperties.Modalias);
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.Adapter), deviceProperties.Adapter.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.TxPower), deviceProperties.TxPower.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.ServicesResolved), deviceProperties.ServicesResolved.ToString());
                        //await _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(nameof(deviceProperties.WakeAllowed), deviceProperties.WakeAllowed.ToString());
                        //await _deviceProxy.WatchPropertiesAsync(scalextricArcWatchProperties);

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
                                //await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Enabled);
                                _logger.LogWarning($"{deviceProxyName} services could not be resolved");
                                await ResetAsync(_deviceObjectPath);
                                return;
                            }
                        }

                        //    _scalextricArcState.GattCharacteristics = new();

                        foreach (var item in _bluezObjectPathInterfaces.Where(x => x.Key.ToString().StartsWith(_deviceObjectPath.ToString()!) && x.Value.Any(i => i.BluezInterface == bluezGattCharacteristicInterface)).OrderBy(x => x.Key))
                        {
                            var proxy = Tmds.DBus.Connection.System.CreateProxy<bluez.DBus.IGattCharacteristic1>(bluezService, item.Key);
                            var properties = await proxy.GetAllAsync();
                            Console.WriteLine($"{item.Key} {string.Join(", ", item.Value.Select(x => x.BluezInterface))} {properties.UUID}  {string.Join(", ", properties.Flags)}");

                            await GattCharacteristicResolvedAsync(properties);

                        //        var gattCharacteristic = new GattCharacteristic();

                        //        if (!string.IsNullOrEmpty(properties.UUID))
                        //        {
                        //            gattCharacteristic.uuid = properties.UUID;

                        //            switch (properties.UUID)
                        //            {
                        //                case manufacturerNameCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Manufacturer name";
                        //                    break;

                        //                case modelNumberCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Model number";
                        //                    break;

                        //                case hardwareRevisionCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Hardware revision";
                        //                    break;

                        //                case firmwareRevisionCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Firmware revision";
                        //                    break;

                        //                case softwareRevisionCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Software revision";
                        //                    break;

                        //                case dfuControlPointCharacteristicUuid:
                        //                    gattCharacteristic.Name = "DFU control point";
                        //                    break;

                        //                case dfuPacketCharacteristicUuid:
                        //                    gattCharacteristic.Name = "DFU packet";
                        //                    break;

                        //                case dfuRevisionCharacteristicUuid:
                        //                    gattCharacteristic.Name = "DFU revision";
                        //                    break;

                        //                case serviceChangedCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Service changed";
                        //                    break;

                        //                case carIdCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Car ID";
                        //                    break;

                        //                case commandCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Command";
                        //                    break;

                        //                case slotCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Slot";
                        //                    break;

                        //                case throttleCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Throttle";
                        //                    break;

                        //                case throttleProfile1CharacteristicUuid:
                        //                    gattCharacteristic.Name = "Throttle profile 1";
                        //                    break;

                        //                case throttleProfile2CharacteristicUuid:
                        //                    gattCharacteristic.Name = "Throttle profile 2";
                        //                    break;

                        //                case throttleProfile3CharacteristicUuid:
                        //                    gattCharacteristic.Name = "Throttle profile 3";
                        //                    break;

                        //                case throttleProfile4CharacteristicUuid:
                        //                    gattCharacteristic.Name = "Throttle profile 4";
                        //                    break;

                        //                case throttleProfile5CharacteristicUuid:
                        //                    gattCharacteristic.Name = "Throttle profile 5";
                        //                    break;

                        //                case throttleProfile6CharacteristicUuid:
                        //                    gattCharacteristic.Name = "Throttle profile 6";
                        //                    break;

                        //                case trackCharacteristicUuid:
                        //                    gattCharacteristic.Name = "Track";
                        //                    break;

                        //                default:
                        //                    break;
                        //            }
                        //        }

                        //        if (properties.Flags is not null)
                        //        {
                        //            foreach (var flag in properties.Flags)
                        //            {
                        //                if (!string.IsNullOrEmpty(flag))
                        //                {
                        //                    gattCharacteristic.Flags.Add(new BluezClientGattCharacteristicFlag
                        //                    {
                        //                        Flag = flag
                        //                    });
                        //                }
                        //            }

                        //            if (properties.Flags!.Contains("read"))
                        //            {
                        //                var value = await proxy.ReadValueAsync(new Dictionary<string, object>());
                        //                gattCharacteristic.Length = value.Length;
                        //                if (value.Length > 0)
                        //                {
                        //                    if (!value.Any(x => x < 32))
                        //                    {
                        //                        gattCharacteristic.Value = System.Text.Encoding.UTF8.GetString(value);
                        //                        if (properties.UUID == modelNumberCharacteristicUuid)
                        //                        {
                        //                            await _scalextricArcState.ConnectionState.SetModelNumberAsync(gattCharacteristic.Value);
                        //                        }
                        //                    }

                        //                    if (properties.UUID == carIdCharacteristicUuid)
                        //                    {
                        //                        await _scalextricArcState.CarIdState.SetAsync
                        //                        (
                        //                            value[0],
                        //                            false
                        //                        );
                        //                    }

                        //                    if (properties.UUID == commandCharacteristicUuid)
                        //                    {
                        //                        await _scalextricArcState.CommandState.SetAsync
                        //                        (
                        //                            (CommandType)value[0],
                        //                            (byte)(value[1] & 0b111111),
                        //                            (value[1] & 0b1000000) > 0,
                        //                            (value[1] & 0b10000000) > 0,
                        //                            value[7],
                        //                            value[13],
                        //                            (value[19] & 0b1) > 0,
                        //                            (byte)(value[2] & 0b111111),
                        //                            (value[2] & 0b1000000) > 0,
                        //                            (value[2] & 0b10000000) > 0,
                        //                            value[8],
                        //                            value[14],
                        //                            (value[19] & 0b10) > 0,
                        //                            (byte)(value[3] & 0b111111),
                        //                            (value[3] & 0b1000000) > 0,
                        //                            (value[3] & 0b10000000) > 0,
                        //                            value[9],
                        //                            value[15],
                        //                            (value[19] & 0b100) > 0,
                        //                            (byte)(value[4] & 0b111111),
                        //                            (value[4] & 0b1000000) > 0,
                        //                            (value[4] & 0b10000000) > 0,
                        //                            value[10],
                        //                            value[16],
                        //                            (value[19] & 0b1000) > 0,
                        //                            (byte)(value[5] & 0b111111),
                        //                            (value[5] & 0b1000000) > 0,
                        //                            (value[5] & 0b10000000) > 0,
                        //                            value[11],
                        //                            value[17],
                        //                            (value[19] & 0b10000) > 0,
                        //                            (byte)(value[6] & 0b111111),
                        //                            (value[6] & 0b1000000) > 0,
                        //                            (value[6] & 0b10000000) > 0,
                        //                            value[12],
                        //                            value[18],
                        //                            (value[19] & 0b100000) > 0,
                        //                            false
                        //                        );
                        //                    }

                        //                    if (properties.UUID == trackCharacteristicUuid)
                        //                    {
                        //                        await _scalextricArcState.TrackState.SetAsync
                        //                        (
                        //                            value[0],
                        //                            value[1],
                        //                            value[2],
                        //                            (uint)(value[3] + value[4] * 256 + value[5] * 65536 + value[6] * 16777216)
                        //                        );
                        //                    }
                        //                }
                        //            }
                        //        }

                        //        if (properties.UUID == carIdCharacteristicUuid)
                        //        {
                        //            _carIdCharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == commandCharacteristicUuid)
                        //        {
                        //            _commandCharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == slotCharacteristicUuid)
                        //        {
                        //            _slotCharacteristicProxy = proxy;
                        //            await _slotCharacteristicProxy.StartNotifyAsync();
                        //            _slotCharacteristicWatchTask = _slotCharacteristicProxy.WatchPropertiesAsync(slotCharacteristicWatchProperties);
                        //        }

                        //        if (properties.UUID == throttleCharacteristicUuid)
                        //        {
                        //            _throttleCharacteristicProxy = proxy;
                        //            await _throttleCharacteristicProxy.StartNotifyAsync();
                        //            _throttleCharacteristicWatchTask = _throttleCharacteristicProxy.WatchPropertiesAsync(throttleCharacteristicWatchProperties);
                        //        }

                        //        if (properties.UUID == throttleProfile1CharacteristicUuid)
                        //        {
                        //            _throttleProfile1CharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == throttleProfile2CharacteristicUuid)
                        //        {
                        //            _throttleProfile2CharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == throttleProfile3CharacteristicUuid)
                        //        {
                        //            _throttleProfile3CharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == throttleProfile4CharacteristicUuid)
                        //        {
                        //            _throttleProfile4CharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == throttleProfile5CharacteristicUuid)
                        //        {
                        //            _throttleProfile5CharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == throttleProfile6CharacteristicUuid)
                        //        {
                        //            _throttleProfile6CharacteristicProxy = proxy;
                        //        }

                        //        if (properties.UUID == trackCharacteristicUuid)
                        //        {
                        //            _trackCharacteristicProxy = proxy;
                        //            await _trackCharacteristicProxy.StartNotifyAsync();
                        //            _trackCharacteristicWatchTask = _trackCharacteristicProxy.WatchPropertiesAsync(trackCharacteristicWatchProperties);
                        //        }

                        //        _scalextricArcState.GattCharacteristics.Add(gattCharacteristic);
                        //    }

                        }
                        //    await _scalextricArcState.ConnectionState.SetBluetoothStateAsync(BluetoothConnectionStateType.Initialized);
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

            if (_deviceProxy is not null)
            {
                if (await _deviceProxy.GetConnectedAsync())
                {
                    try
                    {
                        _logger.LogInformation("Disconnecting Scalextric ARC...");
                        await _deviceProxy.DisconnectAsync();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, exception.Message);
                    }
                    _logger.LogInformation("Scalextric ARC disconnected.");
                }

                _deviceProxy = null;
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

            _deviceObjectPath = null;
        }

        //private void scalextricArcWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        //{
        //    foreach (var item in propertyChanges.Changed)
        //    {
        //        switch (item.Value)
        //        {
        //            case string[]:
        //                _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(item.Key, String.Join(", ", (string[])item.Value)).Wait();
        //                break;

        //            case Dictionary<ushort, object>:
        //                break;

        //            case Dictionary<string, object>:
        //                break;

        //            default:
        //                _scalextricArcState.ConnectionState.SetBluetoothPropertyAsync(item.Key, item.Value.ToString()).Wait();
        //                break;
        //        }
        //    }
        //}

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
