using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.ServiceHelper;
using StudentComplaintPortal.Application.Services.FileStorage;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public class ConversationService : IConversationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;



    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _directConversationLocks = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _queryConversationLocks = new();

    public ConversationService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
    {
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        
    }

    public async Task<List<ConversationDto>> GetConversationsForUserAsync(string userId)
    {
        var conversationIds = await _unitOfWork.Conversations.GetConversationIdsForUserAsync(userId);
        var conversations = await _unitOfWork.Conversations.GetConversationsWithParticipantsAsync(conversationIds);

        var result = new List<ConversationDto>();

        foreach (var conv in conversations)
        {
            var lastMessage = await _unitOfWork.Conversations.GetLastMessageAsync(conv.Id);

            if (conv.Type == ConversationType.Direct && lastMessage == null)
            {
                continue;
            }

            var myParticipant = conv.Participants.FirstOrDefault(p => p.UserId == userId);
            var myLastReadAt = myParticipant?.LastReadAt;

            var unreadCount = await _unitOfWork.Conversations.GetUnreadCountAsync(conv.Id, userId, myLastReadAt);

            string? displayName = conv.Name;
            string? otherUserId = null;

            if (conv.Type == ConversationType.Direct)
            {
                var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId);
                displayName = otherParticipant?.User.FullName;
                otherUserId = otherParticipant?.UserId;
            }

            result.Add(new ConversationDto
            {
                Id = conv.Id,
                Type = conv.Type.ToString(),
                Name = displayName,
                OtherUserId = otherUserId,
                UnreadCount = unreadCount,
                LastMessagePreview = lastMessage?.Content,
                LastMessageAt = lastMessage?.SentAt
            });
        }

        return result
            .OrderByDescending(c => c.Type == "Group")
            .ThenByDescending(c => c.LastMessageAt)
            .ToList();
    }

    public async Task<int> GetOrCreateDirectConversationAsync(string userId1, string userId2)
    {
        var pairKey = string.CompareOrdinal(userId1, userId2) < 0
            ? $"{userId1}:{userId2}"
            : $"{userId2}:{userId1}";
        var gate = _directConversationLocks.GetOrAdd(pairKey, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync();
        try
        {
            var existing = await _unitOfWork.Conversations.FindDirectConversationAsync(userId1, userId2);

            if (existing != null)
            {
                return existing.Id;
            }

            var newConversation = new Conversation
            {
                Type = ConversationType.Direct,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Conversations.AddAsync(newConversation);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.Conversations.AddParticipantsAsync(new[]
            {
                new ConversationParticipant { ConversationId = newConversation.Id, UserId = userId1 },
                new ConversationParticipant { ConversationId = newConversation.Id, UserId = userId2 }
            });
            await _unitOfWork.SaveChangesAsync();

            return newConversation.Id;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<List<InternalMessageDto>> GetMessagesAsync(int conversationId)
    {
        var participants = await _unitOfWork.Conversations.GetParticipantsAsync(conversationId);
        var messages = await _unitOfWork.Conversations.GetMessagesWithSenderAsync(conversationId);

        return BuildMessageDtos(messages, participants);
    }

    public async Task<CursorResult<InternalMessageDto>> GetMessagesPagedAsync(int conversationId, string? cursor, int pageSize = 20, bool moveForward = true)
    {
        var participants = await _unitOfWork.Conversations.GetParticipantsAsync(conversationId);
        var messages = await _unitOfWork.Conversations.GetMessagesWithSenderAsync(conversationId);

        var messageDtos = BuildMessageDtos(messages, participants);

        return PaginationHelper.PaginateByCursorId(messageDtos, dto => dto.Id, cursor, pageSize, moveForward);
    }

    private static List<InternalMessageDto> BuildMessageDtos(List<InternalMessage> messages, List<ConversationParticipant> participants)
    {
        var result = new List<InternalMessageDto>();
        foreach (var m in messages)
        {
            var others = participants.Where(p => p.UserId != m.SenderId).ToList();

            DateTime? seenByAllAt = null;
            if (others.Count > 0 && others.All(p => p.LastReadAt.HasValue && p.LastReadAt.Value >= m.SentAt))
            {
                seenByAllAt = others.Max(p => p.LastReadAt!.Value);
            }

            result.Add(new InternalMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender.FullName,
                Content = m.Content,
                SentAt = m.SentAt,
                ReadAt = seenByAllAt,
                IsEdited = m.IsEdited,
                EditedAt = m.EditedAt,
                OriginalContent = m.OriginalContent,
                DeletedAt = m.DeletedAt,
                Attachments = m.Attachments.Select(a => new InternalAttachmentDto   // 👈 NAYA
                {
                    Id = a.Id,
                    FileUrl = a.FileUrl,
                    FileType = a.FileType.ToString(),
                    FileSizeBytes = a.FileSizeBytes
                }).ToList()
            });
        }
        return result;
    }

    public async Task<InternalMessageDto> SendMessageAsync(int conversationId, string senderId, string content)
    {
        var message = new InternalMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        var conversation = await _unitOfWork.Conversations.GetByIdAsync(conversationId);
        if (conversation != null)
        {
            conversation.LastMessageAt = message.SentAt;
            _unitOfWork.Conversations.Update(conversation);
        }

        await _unitOfWork.Conversations.AddMessageAsync(message);
        await _unitOfWork.SaveChangesAsync();

        var sender = await _unitOfWork.Conversations.GetUserAsync(senderId);

        return new InternalMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender?.FullName ?? string.Empty,
            Content = message.Content,
            SentAt = message.SentAt,
            ReadAt = message.ReadAt
        };
    }

    public async Task MarkAllAsReadAsync(int conversationId, string readerUserId)
    {
        var participant = await _unitOfWork.Conversations.GetParticipantAsync(conversationId, readerUserId);

        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<List<ParticipantDto>> GetParticipantsAsync(int conversationId)
    {
        var participants = await _unitOfWork.Conversations.GetParticipantsAsync(conversationId);
        return participants.Select(p => new ParticipantDto
        {
            UserId = p.UserId,
            FullName = p.User.FullName,
            LastReadAt = p.LastReadAt
        }).ToList();
    }

    public async Task<List<ParticipantDto>> GetContactsAsync(string currentUserId)
    {
        var users = await _unitOfWork.Conversations.GetStaffAndAdminContactsAsync(currentUserId);
        return users.Select(u => new ParticipantDto
        {
            UserId = u.Id,
            FullName = u.FullName,
            LastReadAt = null
        }).ToList();
    }

    public async Task<CursorResult<ConversationDto>> GetConversationsPagedForUserAsync(string userId, string? cursor, int pageSize = 20, bool moveForward = true)
    {
        if (pageSize < 1) pageSize = 10;

        var dtos = new List<ConversationDto>();

        // Sirf pehli page (cursor null) par pinned "Team" group hamesha top pe dikhao
        if (string.IsNullOrEmpty(cursor))
        {
            var pinnedGroup = await _unitOfWork.Conversations.GetPinnedGroupForUserAsync(userId);
            if (pinnedGroup != null)
            {
                dtos.Add(await BuildConversationDtoAsync(pinnedGroup, userId));
            }
        }

        var cursorTimestamp = PaginationHelper.DecodeTimestampCursor(cursor);
        var conversations = await _unitOfWork.Conversations.GetConversationsPagedForUserAsync(userId, cursorTimestamp, pageSize, moveForward);

        var hasMore = conversations.Count > pageSize;
        if (hasMore) conversations = conversations.Take(pageSize).ToList();

        foreach (var conv in conversations)
        {
            dtos.Add(await BuildConversationDtoAsync(conv, userId));
        }

        string? nextCursor = hasMore ? PaginationHelper.EncodeTimestampCursor(conversations.Last().LastMessageAt ?? conversations.Last().CreatedAt) : null;
        string? previousCursor = conversations.Count > 0 ? PaginationHelper.EncodeTimestampCursor(conversations.First().LastMessageAt ?? conversations.First().CreatedAt) : null;

        return new CursorResult<ConversationDto>
        {
            Items = dtos,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor,
            HasMore = hasMore,
            PageSize = pageSize
        };
    }

    private async Task<ConversationDto> BuildConversationDtoAsync(Conversation conv, string userId)
    {
        var lastMessage = await _unitOfWork.Conversations.GetLastMessageAsync(conv.Id);

        var myParticipant = conv.Participants.FirstOrDefault(p => p.UserId == userId);
        var myLastReadAt = myParticipant?.LastReadAt;

        var unreadCount = await _unitOfWork.Conversations.GetUnreadCountAsync(conv.Id, userId, myLastReadAt);

        string? displayName = conv.Name;
        string? otherUserId = null;

        if (conv.Type == ConversationType.Direct)
        {
            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId);
            displayName = otherParticipant?.User.FullName;
            otherUserId = otherParticipant?.UserId;
        }

        return new ConversationDto
        {
            Id = conv.Id,
            Type = conv.Type.ToString(),
            Name = displayName,
            OtherUserId = otherUserId,
            UnreadCount = unreadCount,
            LastMessagePreview = lastMessage?.Content,
            LastMessageAt = lastMessage?.SentAt
        };
    }
    public async Task<InternalMessageDto> CreateMessageWithAttachmentAsync(
        int conversationId, string senderId, Stream fileStream, string fileName, string contentType,
        FileType fileType, string? content = null)
    {
        var fileUrl = await _fileStorageService.UploadAsync(fileStream, fileName, contentType, fileType, conversationId);

        var message = new InternalMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };
        await _unitOfWork.Conversations.AddMessageAsync(message);
        await _unitOfWork.SaveChangesAsync();

        var attachment = new InternalAttachment
        {
            InternalMessageId = message.Id,
            FileUrl = fileUrl,
            FileType = fileType,
            FileSizeBytes = fileStream.Length,
            UploadedAt = DateTime.UtcNow
        };
        await _unitOfWork.Conversations.AddAttachmentAsync(attachment);
        await _unitOfWork.SaveChangesAsync();

        var sender = await _unitOfWork.Conversations.GetUserAsync(senderId);

        return new InternalMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender?.FullName ?? string.Empty,
            Content = message.Content,
            SentAt = message.SentAt,
            ReadAt = message.ReadAt,
            Attachments = new List<InternalAttachmentDto>
            {
                new InternalAttachmentDto
                {
                    Id = attachment.Id,
                    FileUrl = attachment.FileUrl,
                    FileType = attachment.FileType.ToString(),
                    FileSizeBytes = attachment.FileSizeBytes
                }
            }
        };
    }
    public async Task<string> GetOrAssignQueryAliasAsync(string staffUserId)
    {
        var user = await _unitOfWork.Conversations.GetUserAsync(staffUserId);
        if (user == null) return "Unknown";

        if (!string.IsNullOrEmpty(user.QueryAlias))
        {
            return user.QueryAlias;
        }

        var allUsers = await _unitOfWork.Conversations.GetStaffAndAdminContactsAsync("");
        var existingAliases = allUsers
            .Where(u => !string.IsNullOrEmpty(u.QueryAlias))
            .Select(u => u.QueryAlias)
            .ToList();

        int nextNumber = 1;
        if (existingAliases.Any())
        {
            var numbers = existingAliases
                .Select(a => int.TryParse(a!.Replace("A", ""), out var n) ? n : 0)
                .ToList();
            nextNumber = numbers.Max() + 1;
        }

        var newAlias = $"A{nextNumber}";
        await _unitOfWork.Conversations.UpdateUserQueryAliasAsync(staffUserId, newAlias);
        await _unitOfWork.SaveChangesAsync();

        return newAlias;
    }
    public async Task<int> GetOrCreateQueryConversationAsync(string studentId)
    {
        var gate = _queryConversationLocks.GetOrAdd(studentId, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync();
        try
        {
            var existing = await _unitOfWork.Conversations.FindQueryConversationForStudentAsync(studentId);
            if (existing != null)
            {
                return existing.Id;
            }

            var newConversation = new Conversation
            {
                Type = ConversationType.Query,
                Name = "Support",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Conversations.AddAsync(newConversation);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.Conversations.AddParticipantsAsync(new[]
            {
            new ConversationParticipant { ConversationId = newConversation.Id, UserId = studentId }
        });
            await _unitOfWork.SaveChangesAsync();

            return newConversation.Id;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CursorResult<ConversationDto>> GetQueryConversationsPagedAsync(string viewerId, string? cursor, int pageSize = 20, bool moveForward = true)
    {
        if (pageSize < 1) pageSize = 10;

        var cursorTimestamp = PaginationHelper.DecodeTimestampCursor(cursor);
        var conversations = await _unitOfWork.Conversations.GetQueryConversationsPagedAsync(cursorTimestamp, pageSize, moveForward);

        var hasMore = conversations.Count > pageSize;
        if (hasMore) conversations = conversations.Take(pageSize).ToList();

        var dtos = new List<ConversationDto>();
        foreach (var conv in conversations)
        {
            var lastMessage = await _unitOfWork.Conversations.GetLastMessageAsync(conv.Id);
            var studentParticipant = conv.Participants.FirstOrDefault();

            // Viewer (Staff/Admin) abhi-tak is-Query mein participant-nahi-bana (kabhi-reply-nahi-kiya)
            // to uska LastReadAt null-hoga — GetUnreadCountAsync ye-khud-handle-karta-hai (null = sab-count-ho)
            var myParticipant = conv.Participants.FirstOrDefault(p => p.UserId == viewerId);
            var myLastReadAt = myParticipant?.LastReadAt;

            var unreadCount = await _unitOfWork.Conversations.GetUnreadCountAsync(conv.Id, viewerId, myLastReadAt);

            dtos.Add(new ConversationDto
            {
                Id = conv.Id,
                Type = conv.Type.ToString(),
                Name = studentParticipant?.User.FullName ?? "Unknown Student",
                OtherUserId = studentParticipant?.UserId,
                UnreadCount = unreadCount,
                LastMessagePreview = lastMessage?.Content,
                LastMessageAt = lastMessage?.SentAt
            });
        }

        string? nextCursor = hasMore ? PaginationHelper.EncodeTimestampCursor(conversations.Last().LastMessageAt ?? conversations.Last().CreatedAt) : null;
        string? previousCursor = conversations.Count > 0 ? PaginationHelper.EncodeTimestampCursor(conversations.First().LastMessageAt ?? conversations.First().CreatedAt) : null;

        return new CursorResult<ConversationDto>
        {
            Items = dtos,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor,
            HasMore = hasMore,
            PageSize = pageSize
        };
    }
    public async Task EnsureParticipantAsync(int conversationId, string userId)
    {
        var existing = await _unitOfWork.Conversations.GetParticipantAsync(conversationId, userId);
        if (existing == null)
        {
            await _unitOfWork.Conversations.AddParticipantsAsync(new[]
            {
            new ConversationParticipant { ConversationId = conversationId, UserId = userId }
        });
            await _unitOfWork.SaveChangesAsync();
        }
    }
    public async Task<CursorResult<InternalMessageDto>> GetQueryMessagesPagedAsync(int conversationId, bool viewerCanSeeRealNames, string? cursor, int pageSize = 20, bool moveForward = true)
    {
        var participants = await _unitOfWork.Conversations.GetParticipantsAsync(conversationId);
        var messages = await _unitOfWork.Conversations.GetMessagesWithSenderAsync(conversationId);

        var dtos = BuildMessageDtos(messages, participants);

        foreach (var dto in dtos)
        {
            var sender = await _unitOfWork.Conversations.GetUserAsync(dto.SenderId);

            if (sender != null && (sender.Role == UserRole.Staff || sender.Role == UserRole.Admin))
            {
                if (!viewerCanSeeRealNames)
                {
                    dto.SenderName = await GetOrAssignQueryAliasAsync(dto.SenderId);
                }
            }
        }

        return PaginationHelper.PaginateByCursorId(dtos, dto => dto.Id, cursor, pageSize, moveForward);
    }

    public async Task<InternalMessageDto> EditInternalMessageAsync(int messageId, string userId, string newContent)
    {
        var message = await _unitOfWork.Conversations.GetMessageByIdAsync(messageId);

        if (message == null)
        {
            throw new StudentComplaintPortal.Application.Exceptions.NotFoundException($"Message with ID {messageId} not found.");
        }

        // Sirf-sender apna-message edit-kar-sake (Admin bhi doosron-ka-nahi, Complaint-message-pattern jaisa)
        if (message.SenderId != userId)
        {
            throw new StudentComplaintPortal.Application.Exceptions.UnauthorizedComplaintAccessException("Only the sender can edit this message.");
        }

        if (!message.IsEdited)
        {
            message.OriginalContent = message.Content;
        }

        message.Content = newContent;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        _unitOfWork.Conversations.UpdateMessage(message);
        await _unitOfWork.SaveChangesAsync();

        var sender = await _unitOfWork.Conversations.GetUserAsync(message.SenderId);

        return new InternalMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender?.FullName ?? string.Empty,
            Content = message.Content,
            SentAt = message.SentAt,
            ReadAt = message.ReadAt,
            IsEdited = message.IsEdited,
            EditedAt = message.EditedAt,
            OriginalContent = message.OriginalContent,
            DeletedAt = message.DeletedAt
        };
    }

    public async Task<bool> DeleteInternalMessageAsync(int messageId, string userId, bool isAdmin)
    {
        var message = await _unitOfWork.Conversations.GetMessageByIdAsync(messageId);

        if (message == null)
        {
            throw new StudentComplaintPortal.Application.Exceptions.NotFoundException($"Message with ID {messageId} not found.");
        }

        // Sender apna-khud-ka-message delete-kar-sake, ya Admin-koi-bhi-message (Complaint-message-pattern jaisa)
        if (message.SenderId != userId && !isAdmin)
        {
            throw new StudentComplaintPortal.Application.Exceptions.UnauthorizedComplaintAccessException("You do not have permission to delete this message.");
        }

        message.DeletedAt = DateTime.UtcNow;

        _unitOfWork.Conversations.UpdateMessage(message);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}