using Microsoft.Extensions.Logging;
using RazManagerIO.Host.Services.Utilities;


namespace RazManagerIO.Host.Services.CarreraDigital
{
    public class CarreraDigitalBluezClient : BluezClientServiceBase, ICarreraDigitalBluezClient
    {
        public CarreraDigitalBluezClient(ILogger<CarreraDigitalBluezClient> logger) : base("Control_Unit", new System.Guid("39df7777-b1b4-b90b-57f1-7144ae4e4a6a"), logger)
        {
        }
    }
}
