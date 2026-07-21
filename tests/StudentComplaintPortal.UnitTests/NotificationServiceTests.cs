using Moq;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using Xunit;

namespace StudentComplaintPortal.UnitTests;

public class NotificationServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<INotificationPushService> _mockPushService;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockPushService = new Mock<INotificationPushService>();
        _mockUnitOfWork.Setup(u => u.Notifications).Returns(_mockNotificationRepo.Object);
        _service = new NotificationService(_mockUnitOfWork.Object, _mockPushService.Object);
    }

    [Fact]
    public async Task NotifyAsync_CreatesNotificationAndPushes()
    {
        // Arrange
        var userId = "user123";
        var message = "Test notification";
        var type = NotificationType.NewMessage;

        var savedNotification = new Notification
        {
            Id = 1,
            UserId = userId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockNotificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
            .ReturnsAsync(savedNotification);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _service.NotifyAsync(userId, message, type);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(message, result.Message);
        Assert.False(result.IsRead);
        
        _mockNotificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        _mockPushService.Verify(p => p.PushNotificationAsync(userId, It.IsAny<NotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsUserNotifications()
    {
        // Arrange
        var userId = "user123";
        var notifications = new List<Notification>
        {
            new Notification
            {
                Id = 1,
                UserId = userId,
                Message = "Notification 1",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new Notification
            {
                Id = 2,
                UserId = userId,
                Message = "Notification 2",
                IsRead = true,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            }
        };

        _mockNotificationRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Notification, bool>>>()))
            .ReturnsAsync(notifications);

        // Act
        var result = await _service.GetUserNotificationsAsync(userId);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Equal(2, result.First().Id); // Most recent first
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesNotification()
    {
        // Arrange
        var notificationId = 1;
        var userId = "user123";
        var notification = new Notification
        {
            Id = notificationId,
            UserId = userId,
            Message = "Test",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockNotificationRepo.Setup(r => r.GetByIdAsync(notificationId))
            .ReturnsAsync(notification);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _service.MarkAsReadAsync(notificationId, userId);

        // Assert
        Assert.True(result.IsRead);
        _mockNotificationRepo.Verify(r => r.Update(It.IsAny<Notification>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}
