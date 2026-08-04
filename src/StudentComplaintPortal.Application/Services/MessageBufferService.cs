using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentComplaintPortal.Application.Services;

public class ChatBufferEntry
{
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public List<Message> Messages { get; set; } = new();
}

public class MessageBufferService
{
    private readonly ConcurrentDictionary<int, ChatBufferEntry> _buffers = new();

    public event Action<int, MessageDto>? OnMessageBroadcast;

    public void AddMessage(int complaintId, Message message)
    {
        var entry = _buffers.GetOrAdd(complaintId, _ => new ChatBufferEntry());

        lock (entry)
        {
            entry.Messages.Add(message);
            entry.LastActivity = DateTime.UtcNow;
        }
    }

    // NEW METHOD: Get buffered messages so the UI can show them instantly
    public List<Message> GetBufferedMessages(int complaintId)
    {
        if (_buffers.TryGetValue(complaintId, out var entry))
        {
            lock (entry)
            {
                // Return a copy of the list to prevent modification while reading
                return new List<Message>(entry.Messages);
            }
        }
        return new List<Message>();
    }

    public Dictionary<int, List<Message>> ExtractStaleBuffers(TimeSpan cooldown)
    {
        var staleMessages = new Dictionary<int, List<Message>>();
        var now = DateTime.UtcNow;

        foreach (var kvp in _buffers)
        {
            lock (kvp.Value)
            {
                if (now - kvp.Value.LastActivity >= cooldown && kvp.Value.Messages.Any())
                {
                    staleMessages.Add(kvp.Key, new List<Message>(kvp.Value.Messages));
                    kvp.Value.Messages.Clear(); // Clears the log so it doesn't save twice
                }
            }
        }
        return staleMessages;
    }

    public Dictionary<int, List<Message>> ExtractAllBuffers()
    {
        return ExtractStaleBuffers(TimeSpan.Zero);
    }

    public void BroadcastToClients(int complaintId, MessageDto message)
    {
        OnMessageBroadcast?.Invoke(complaintId, message);
    }
    public void MarkAsRead(int complaintId, string readerUserId)
    {
        if (_buffers.TryGetValue(complaintId, out var entry))
        {
            lock (entry)
            {
                var now = DateTime.UtcNow;
                foreach (var msg in entry.Messages)
                {
                    if (msg.SenderId != readerUserId && msg.ReadAt == null)
                    {
                        msg.ReadAt = now;
                        msg.IsRead = true;
                    }
                }
            }
        }
    }
}