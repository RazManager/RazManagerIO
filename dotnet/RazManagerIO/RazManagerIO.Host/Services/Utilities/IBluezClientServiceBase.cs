using System.Threading;
using System.Threading.Tasks;

namespace RazManagerIO.Host.Services.Utilities
{
    public interface IBluezClientServiceBase
    {
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
}