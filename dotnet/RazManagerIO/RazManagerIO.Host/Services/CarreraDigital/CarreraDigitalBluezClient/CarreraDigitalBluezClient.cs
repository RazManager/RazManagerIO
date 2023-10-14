using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using bluez.DBus;
using Microsoft.AspNetCore.Server.IIS.Core;
using Microsoft.Extensions.Logging;
using RazManagerIO.Host.Services.Utilities;


namespace RazManagerIO.Host.Services.CarreraDigital
{
    public class CarreraDigitalBluezClient : BluezClientServiceBase, ICarreraDigitalBluezClient
    {
        private readonly ILogger<CarreraDigitalBluezClient> _logger;

        public CarreraDigitalBluezClient(ILogger<CarreraDigitalBluezClient> logger) : base("Control_Unit", new System.Guid("39df7777-b1b4-b90b-57f1-7144ae4e4a6a"), logger)
        {
            _logger = logger;
        }


        protected override Task BluetoothConnectionStateChangedAsync()
        {
            _logger.LogInformation(BluetoothConnectionState.ToString());
            throw new System.NotImplementedException();
        }


        protected override Task DevicePropertiesChangedAsync()
        {
            return Task.CompletedTask;
        }


        protected override Task GattCharacteristicResolvedAsync(GattCharacteristic1Properties properties)
        {
            var gattCharacteristic = (BluezClientGattCharacteristic)properties;

            if (!string.IsNullOrEmpty(properties.UUID))
            {
                switch (properties.UUID)
                {
                    //case manufacturerNameCharacteristicUuid:
                    //    gattCharacteristic.Name = "Manufacturer name";
                    //_commandCharacteristicProxy = proxy;
                    //    break;

                    //case modelNumberCharacteristicUuid:
                    //    gattCharacteristic.Name = "Model number";
                //_slotCharacteristicProxy = proxy;
                //await _slotCharacteristicProxy.StartNotifyAsync();
                //_slotCharacteristicWatchTask = _slotCharacteristicProxy.WatchPropertiesAsync(slotCharacteristicWatchProperties);
                //    break;

                default:
                        break;
                }
            }
            _gattCharacteristics.Add(gattCharacteristic);
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

    }
}
