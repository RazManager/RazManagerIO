using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RazManagerIO.Host.Services.CarreraDigital;
using RazManagerIO.Host.Services.CpuInfo;
using RazManagerIO.Host.Services.OsRelease;
using System;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace RazManagerIO.Host.Services.InputOutput
{
    public class InputOutputService : IHostedService
    {
        private readonly GpioController _gpioController;
        private readonly ICpuInfoService _cpuInfoService;
        private readonly IOsReleaseService _osReleaseService;
        private readonly Channel<bool> _resetChannel;
        private readonly ICarreraDigitalBluezClient _carreraDigitalBluezClient;
        private readonly ILogger<InputOutputService> _logger;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _resetTask;

        private Task? _carreraDigitalBluezClientTask;


        public InputOutputService(GpioController gpioController,
                                  ICpuInfoService cpuInfoService,
                                  IOsReleaseService osReleaseService,
                                  Channel<bool> resetChannel,
                                  ICarreraDigitalBluezClient carreraDigitalBluezClient,
                                  ILogger<InputOutputService> logger)
        {
            _gpioController = gpioController;
            _cpuInfoService = cpuInfoService;
            _osReleaseService = osReleaseService;
            _resetChannel = resetChannel;
            _carreraDigitalBluezClient = carreraDigitalBluezClient;
            _logger = logger;
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _carreraDigitalBluezClientTask = _carreraDigitalBluezClient.ExecuteAsync(_cancellationTokenSource.Token);

            if (_resetTask is null)
            {
                _resetTask = ResetAsync(cancellationToken);
            }

            return Task.CompletedTask;
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();

            if (_carreraDigitalBluezClientTask is not null)
            {
                try
                {
                    await _carreraDigitalBluezClientTask.WaitAsync(TimeSpan.FromMinutes(1));
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception.Message);
                }
            }
        }


        private async Task ResetAsync(CancellationToken cancellationToken)
        {
            await foreach (var reset in _resetChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await StopAsync(cancellationToken);
                await StartAsync(cancellationToken);
            }
        }
    }
}
