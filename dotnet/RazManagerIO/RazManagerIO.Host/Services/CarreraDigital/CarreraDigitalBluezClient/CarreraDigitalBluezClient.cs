using Microsoft.Extensions.Logging;
using RazManagerIO.Host.Services.Utilities;


namespace RazManagerIO.Host.Services.CarreraDigital
{
    public class CarreraDigitalBluezClient : BluezClientServiceBase, ICarreraDigitalBluezClient
    {
        public CarreraDigitalBluezClient(ILogger<CarreraDigitalBluezClient> logger) : base(logger)
        {
        }
    }
}
