using System.Threading.Tasks;
using System;
using Tmds.DBus;


namespace RazManagerIO.Host.Services.Utilities
{
    public abstract class BluezPropertiesBase<TV>
    {
        protected readonly TV Properties;

        public BluezPropertiesBase(ObjectPath objectPath, TV properties)
        {
            ObjectPath = objectPath;
            Properties = properties;
        }

        public ObjectPath ObjectPath { get; }

        public Task<T> GetAsync<T>(string prop)
        {
            var propertyValue = Properties.GetType().GetProperty(prop)?.GetValue(Properties);
            return Task.FromResult((T)propertyValue);
        }

        public Task<TV> GetAllAsync()
        {
            return Task.FromResult(Properties);
        }

        public Task SetAsync(string prop, object val)
        {
            Properties.GetType().GetProperty(prop)?.SetValue(Properties, val);
            return Task.CompletedTask;
        }

        public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
        {
            return SignalWatcher.AddAsync(this, nameof(OnPropertiesChanged), handler);
        }

        public event Action<PropertyChanges> OnPropertiesChanged;
    }

}
