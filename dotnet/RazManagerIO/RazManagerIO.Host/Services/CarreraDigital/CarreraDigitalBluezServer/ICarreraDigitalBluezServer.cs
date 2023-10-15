using System.Threading.Tasks;
using System.Threading;


namespace RazManagerIO.Host.Services.CarreraDigital
{
    public interface ICarreraDigitalBluezServer
    {
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
}
