using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using bluez.DBus;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using RazManagerIO.Host.Services.Utilities;


namespace RazManagerIO.Host.Services.CarreraDigital
{
    public class CarreraDigitalBluezClient : BluezClientServiceBase, ICarreraDigitalBluezClient
    {
        private const string notiFyCharacteristicUuid = "39df9999-b1b4-b90b-57f1-7144ae4e4a6a";
        private bluez.DBus.IGattCharacteristic1? _notiFyCharacteristicProxy = null;
        private Task? _notiFyCharacteristicWatchTask = null;

        private const string writeCharacteristicUuid = "39df8888-b1b4-b90b-57f1-7144ae4e4a6a";
        private bluez.DBus.IGattCharacteristic1? _writeCharacteristicProxy = null;

        
     
        private readonly ILogger<CarreraDigitalBluezClient> _logger;


        public CarreraDigitalBluezClient(ILogger<CarreraDigitalBluezClient> logger) : base("Control_Unit", new System.Guid("39df7777-b1b4-b90b-57f1-7144ae4e4a6a"), logger)
        {
            _logger = logger;
        }


        protected override async Task BluetoothConnectionStateChangedAsync()
        {
            _logger.LogInformation(BluetoothConnectionState.ToString());

            if (this.BluetoothConnectionState == BluezClientBluetoothConnectionStateType.Initialized)
            {                
                var value = System.Text.ASCIIEncoding.ASCII.GetBytes("0");
                await _writeCharacteristicProxy.WriteValueAsync(value, new Dictionary<string, object>());
            }
        }


        protected override Task DevicePropertiesChangedAsync()
        {
            return Task.CompletedTask;
        }


        protected override async Task GattCharacteristicResolvedAsync(IGattCharacteristic1 proxy, GattCharacteristic1Properties properties)
        {
            var gattCharacteristic = new BluezClientGattCharacteristic(properties);

            if (!string.IsNullOrEmpty(properties.UUID))
            {
                switch (properties.UUID)
                {
                    case writeCharacteristicUuid:
                        _writeCharacteristicProxy = proxy;
                        break;

                    case notiFyCharacteristicUuid:
                        _notiFyCharacteristicProxy = proxy;
                        await _notiFyCharacteristicProxy.StartNotifyAsync();
                        _notiFyCharacteristicWatchTask = _notiFyCharacteristicProxy.WatchPropertiesAsync(notifyCharacteristicWatchProperties);
                        break;

                default:
                    break;
                }
            }
            _gattCharacteristics.Add(gattCharacteristic);
        }


        private void notifyCharacteristicWatchProperties(Tmds.DBus.PropertyChanges propertyChanges)
        {
            foreach (var item in propertyChanges.Changed)
            {
                Console.WriteLine($"notifyCharacteristicWatchProperties: {item.Key} {item.Value}");
                if (item.Key == "Value")
                {
                    var value = (byte[])item.Value;
                    var valueString = System.Text.ASCIIEncoding.ASCII.GetString(value);
                    Console.WriteLine(valueString);
                }
            }
        }
    }
}
