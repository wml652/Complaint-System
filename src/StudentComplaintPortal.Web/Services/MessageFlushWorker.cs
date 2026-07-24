using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Web.Services;

public class MessageFlushWorker : BackgroundService
{
    private readonly MessageBufferService _bufferService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageFlushWorker> _logger;

    public MessageFlushWorker(
        MessageBufferService bufferService, 
        IServiceScopeFactory scopeFactory,
        ILogger<MessageFlushWorker> logger)
    {
        _bufferService = bufferService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Wake up every 1 minute to check the buffers
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await FlushMessagesAsync(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application stopping. Flushing remaining messages to database...");
        // Flush EVERYTHING immediately on shutdown so no data is lost
        await FlushMessagesAsync(TimeSpan.Zero, cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task FlushMessagesAsync(TimeSpan cooldown, CancellationToken cancellationToken)
    {
        var staleBuffers = _bufferService.ExtractStaleBuffers(cooldown);

        if (!staleBuffers.Any()) return;

        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        int messageCount = 0;
        foreach (var buffer in staleBuffers)
        {
            foreach (var message in buffer.Value)
            {
                // CRITICAL FIX: Create a fresh, untracked Message entity.
                // Do NOT pass the original message object, as EF Core will crash 
                // trying to track the attached User/Sender navigation properties across scopes.
                var newDbMessage = new Message
                {
                    ComplaintId = message.ComplaintId,
                    SenderId = message.SenderId,
                    Content = message.Content,
                    SentAt = message.SentAt,
                    IsRead = message.IsRead
                };

                await unitOfWork.Messages.AddAsync(newDbMessage);
                messageCount++;
            }
        }

        // Now this will successfully commit to the database
        await unitOfWork.SaveChangesAsync();
        _logger.LogInformation($"Successfully flushed {messageCount} inactive messages to the database.");
    }
}