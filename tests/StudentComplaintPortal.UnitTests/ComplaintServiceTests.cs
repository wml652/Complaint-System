using Moq;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using Xunit;

namespace StudentComplaintPortal.UnitTests;

public class ComplaintServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IComplaintRepository> _mockComplaintRepo;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly ComplaintService _service;

    public ComplaintServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockComplaintRepo = new Mock<IComplaintRepository>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockUnitOfWork.Setup(u => u.Complaints).Returns(_mockComplaintRepo.Object);
        _service = new ComplaintService(_mockUnitOfWork.Object, _mockNotificationService.Object);
    }

    [Fact]
    public async Task CreateComplaintAsync_ValidInput_ReturnsComplaintDto()
    {
        // Arrange
        var studentId = "student123";
        var dto = new CreateComplaintDto
        {
            Title = "Test Complaint",
            Description = "Test Description",
            Category = "Academic"
        };

        var savedComplaint = new Complaint
        {
            Id = 1,
            Title = dto.Title,
            Description = dto.Description,
            Category = ComplaintCategory.Academic,
            Status = ComplaintStatus.Open,
            StudentId = studentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Student = new AppUser { Id = studentId, FullName = "Test Student", Role = UserRole.Student, Email = "test@test.com", CreatedAt = DateTime.UtcNow }
        };

        _mockComplaintRepo.Setup(r => r.AddAsync(It.IsAny<Complaint>()))
            .ReturnsAsync(savedComplaint);
        _mockComplaintRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(savedComplaint);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _service.CreateComplaintAsync(studentId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal(dto.Title, result.Title);
        Assert.Equal("Open", result.Status);
        _mockComplaintRepo.Verify(r => r.AddAsync(It.IsAny<Complaint>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingComplaint_ReturnsComplaintDto()
    {
        // Arrange
        var complaint = new Complaint
        {
            Id = 1,
            Title = "Test",
            Description = "Desc",
            Category = ComplaintCategory.Hostel,
            Status = ComplaintStatus.Open,
            StudentId = "123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Student = new AppUser { Id = "123", FullName = "Student Name", Role = UserRole.Student, Email = "s@test.com", CreatedAt = DateTime.UtcNow }
        };

        _mockComplaintRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(complaint);

        // Act
        var result = await _service.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentComplaint_ReturnsNull()
    {
        // Arrange
        _mockComplaintRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Complaint?)null);

        // Act
        var result = await _service.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesStatus()
    {
        // Arrange
        var complaint = new Complaint
        {
            Id = 1,
            Title = "Test",
            Description = "Desc",
            Category = ComplaintCategory.Academic,
            Status = ComplaintStatus.Open,
            StudentId = "123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Student = new AppUser { Id = "123", FullName = "Student", Role = UserRole.Student, Email = "s@test.com", CreatedAt = DateTime.UtcNow }
        };

        _mockComplaintRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(complaint);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _service.UpdateStatusAsync(1, ComplaintStatus.InProgress);

        // Assert
        Assert.Equal("InProgress", result.Status);
        _mockComplaintRepo.Verify(r => r.Update(It.IsAny<Complaint>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsException()
    {
        // Arrange
        var complaint = new Complaint
        {
            Id = 1,
            Title = "Test",
            Description = "Desc",
            Category = ComplaintCategory.Academic,
            Status = ComplaintStatus.Closed,
            StudentId = "123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Student = new AppUser { Id = "123", FullName = "Student", Role = UserRole.Student, Email = "s@test.com", CreatedAt = DateTime.UtcNow }
        };

        _mockComplaintRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(complaint);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidStatusTransitionException>(
            () => _service.UpdateStatusAsync(1, ComplaintStatus.Open));
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentComplaint_ThrowsNotFoundException()
    {
        // Arrange
        _mockComplaintRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Complaint?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateStatusAsync(999, ComplaintStatus.InProgress));
    }

    [Fact]
    public async Task GetByStudentAsync_ReturnsStudentComplaints()
    {
        // Arrange
        var studentId = "student123";
        var complaints = new List<Complaint>
        {
            new Complaint
            {
                Id = 1,
                Title = "Complaint 1",
                Description = "Desc 1",
                Category = ComplaintCategory.Academic,
                Status = ComplaintStatus.Open,
                StudentId = studentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Student = new AppUser { Id = studentId, FullName = "Student", Role = UserRole.Student, Email = "s@test.com", CreatedAt = DateTime.UtcNow }
            }
        };

        _mockComplaintRepo.Setup(r => r.GetByStudentIdAsync(studentId))
            .ReturnsAsync(complaints);

        // Act
        var result = await _service.GetByStudentAsync(studentId);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result.First().Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllComplaints()
    {
        // Arrange
        var complaints = new List<Complaint>
        {
            new Complaint
            {
                Id = 1,
                Title = "Complaint 1",
                Description = "Desc 1",
                Category = ComplaintCategory.Academic,
                Status = ComplaintStatus.Open,
                StudentId = "student1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Student = new AppUser { Id = "student1", FullName = "Student 1", Role = UserRole.Student, Email = "s1@test.com", CreatedAt = DateTime.UtcNow }
            },
            new Complaint
            {
                Id = 2,
                Title = "Complaint 2",
                Description = "Desc 2",
                Category = ComplaintCategory.Hostel,
                Status = ComplaintStatus.InProgress,
                StudentId = "student2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Student = new AppUser { Id = "student2", FullName = "Student 2", Role = UserRole.Student, Email = "s2@test.com", CreatedAt = DateTime.UtcNow }
            }
        };

        _mockComplaintRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(complaints);

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count());
    }
}
