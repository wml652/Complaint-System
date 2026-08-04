using Moq;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using Xunit;

namespace StudentComplaintPortal.UnitTests;

public class MessageServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMessageRepository> _mockMessageRepo;
    private readonly Mock<IComplaintRepository> _mockComplaintRepo;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly MessageService _service;

    public MessageServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMessageRepo = new Mock<IMessageRepository>();
        _mockComplaintRepo = new Mock<IComplaintRepository>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockUnitOfWork.Setup(u => u.Messages).Returns(_mockMessageRepo.Object);
        _mockUnitOfWork.Setup(u => u.Complaints).Returns(_mockComplaintRepo.Object);
        _service = new MessageService(_mockUnitOfWork.Object, _mockNotificationService.Object);
    }

    [Fact]
    public async Task SendMessageAsync_ValidInput_ReturnsMessageDto()
    {
        // Arrange
        var complaintId = 1;
        var senderId = "user123";
        var content = "Test message";

        var complaint = new Complaint
        {
            Id = complaintId,
            Title = "Test",
            Description = "Desc",
            Category = ComplaintCategory.Academic,
            Status = ComplaintStatus.Open,
            StudentId = "student123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedMessage = new Message
        {
            Id = 1,
            ComplaintId = complaintId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            Sender = new AppUser { Id = senderId, FullName = "Test User", Role = UserRole.Student, Email = "test@test.com", CreatedAt = DateTime.UtcNow }
        };

        _mockComplaintRepo.Setup(r => r.GetByIdAsync(complaintId)).ReturnsAsync(complaint);
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>())).ReturnsAsync(savedMessage);
        _mockMessageRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(savedMessage);
        _mockMessageRepo.Setup(r => r.GetByComplaintIdAsync(complaintId)).ReturnsAsync(new List<Message> { savedMessage });
        _mockMessageRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Message, bool>>>()))
            .ReturnsAsync(new List<Message> { savedMessage });
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _service.SendMessageAsync(complaintId, senderId, content);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal(content, result.Content);
        Assert.Equal(senderId, result.SenderId);
        _mockMessageRepo.Verify(r => r.AddAsync(It.IsAny<Message>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_NonExistentComplaint_ThrowsNotFoundException()
    {
        // Arrange
        var complaintId = 999;
        var senderId = "user123";
        var content = "Test message";

        _mockComplaintRepo.Setup(r => r.GetByIdAsync(complaintId)).ReturnsAsync((Complaint?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.SendMessageAsync(complaintId, senderId, content));
    }

    [Fact]
    public async Task GetConversationAsync_ReturnsMessagesForComplaint()
    {
        // Arrange
        var complaintId = 1;
        var messages = new List<Message>
        {
            new Message
            {
                Id = 1,
                ComplaintId = complaintId,
                SenderId = "user1",
                Content = "Message 1",
                SentAt = DateTime.UtcNow.AddHours(-2),
                IsRead = false,
                Sender = new AppUser { Id = "user1", FullName = "User 1", Role = UserRole.Student, Email = "u1@test.com", CreatedAt = DateTime.UtcNow }
            },
            new Message
            {
                Id = 2,
                ComplaintId = complaintId,
                SenderId = "user2",
                Content = "Message 2",
                SentAt = DateTime.UtcNow.AddHours(-1),
                IsRead = false,
                Sender = new AppUser { Id = "user2", FullName = "User 2", Role = UserRole.Admin, Email = "u2@test.com", CreatedAt = DateTime.UtcNow }
            }
        };

        _mockMessageRepo.Setup(r => r.GetByComplaintIdAsync(complaintId)).ReturnsAsync(messages);

        // Act
        var result = await _service.GetConversationAsync(complaintId);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Equal("Message 1", result.First().Content);
    }

    [Fact]
    public async Task GetConversationAsync_EmptyConversation_ReturnsEmpty()
    {
        // Arrange
        var complaintId = 1;
        _mockMessageRepo.Setup(r => r.GetByComplaintIdAsync(complaintId))
            .ReturnsAsync(new List<Message>());

        // Act
        var result = await _service.GetConversationAsync(complaintId);

        // Assert
        Assert.Empty(result);
    }
}