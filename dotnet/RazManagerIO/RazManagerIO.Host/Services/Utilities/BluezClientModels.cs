using bluez.DBus;
using Microsoft.AspNetCore.Server.IIS.Core;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


namespace RazManagerIO.Host.Services.Utilities
{
    public enum BluezClientBluetoothConnectionStateType
    {
        Disabled,
        Enabled,
        Discovering,
        Connected,
        Initialized
    }


    public class BluezClientGattCharacteristic : GattCharacteristic1Properties
    {
        public string? Name { get; set; }

        public int? Length { get; set; }

        public BluezClientGattCharacteristic(GattCharacteristic1Properties properties)
        {
            this.UUID = properties.UUID;
            this.Service = properties.Service;
            this.Value = properties.Value;
            this.Notifying = properties.Notifying;
            this.Flags = properties.Flags;
            this.WriteAcquired = properties.WriteAcquired;
            this.NotifyAcquired = properties.NotifyAcquired;
            this.MTU = properties.MTU;
        }
    }
}
