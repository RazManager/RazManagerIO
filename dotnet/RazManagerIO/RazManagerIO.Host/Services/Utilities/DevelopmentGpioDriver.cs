using System.Collections.Generic;
using System.Device.Gpio;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;

namespace RazManagerIO.Host.Services.Utilities
{
    public class DevelopmentGpioDriver : GpioDriver
    {
        private PinMode[] _pinModes;
        private List<PinValueChangedEvent> _pinValueChangedEvents = new List<PinValueChangedEvent>();


        public DevelopmentGpioDriver()
        {
            _pinModes = new PinMode[PinCount];
            for (int i = 0; i < PinCount; i++)
            {
                _pinModes[i] = PinMode.Input;
            }
        }


        protected override int PinCount => 28;


        protected override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
        {
            _pinValueChangedEvents.Add(new PinValueChangedEvent(pinNumber, eventTypes, callback));
        }


        protected override void ClosePin(int pinNumber)
        {
        }


        protected override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber)
        {
            throw new NotImplementedException();
        }


        protected override PinMode GetPinMode(int pinNumber)
        {
            return _pinModes[pinNumber];
        }


        protected override bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            return true;
        }


        protected override void OpenPin(int pinNumber)
        {
        }


        protected override PinValue Read(int pinNumber)
        {
            throw new NotImplementedException();
        }


        protected override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
        {
            foreach (var pinValueChangedEvent in _pinValueChangedEvents.Where(x => x._pinNumber == pinNumber).ToList())
            {
                pinValueChangedEvent.Dispose();
                _pinValueChangedEvents.Remove(pinValueChangedEvent);
            }
        }


        protected override void SetPinMode(int pinNumber, PinMode mode)
        {
            _pinModes[pinNumber] = mode;
        }


        protected override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }


        protected override void Write(int pinNumber, PinValue value)
        {
        }


        private sealed class PinValueChangedEvent : IDisposable
        {
            public readonly int _pinNumber;
            private readonly PinEventTypes _eventType;
            private readonly PinChangeEventHandler _callback;
            private readonly CancellationTokenSource _cancellationTokenSource;


            public PinValueChangedEvent(int pinNumber, PinEventTypes eventType, PinChangeEventHandler callback)
            {
                _pinNumber = pinNumber;
                _eventType = eventType;
                _callback = callback;
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => SimulateEvent(_cancellationTokenSource.Token));
            }


            private async Task SimulateEvent(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10.0 + (new Random().NextDouble()) * 4.0), cancellationToken);
                    _callback.Invoke(this, new PinValueChangedEventArgs(_eventType, _pinNumber));
                }
            }


            public void Dispose()
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
        }
    }

}
