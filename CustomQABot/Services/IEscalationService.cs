using Microsoft.Bot.Builder;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Services;

public interface IEscalationService
{
    Task EscalateAsync(string payLoad, CancellationToken cancellationToken);
}


