namespace EduFlyUp.Domain.Entities;

/// <summary>
/// Lớp thực thể cơ sở (Base Entity) chứa các thuộc tính dùng chung cho tất cả Entity trong hệ thống.
/// Mọi Entity đều kế thừa từ lớp này để đảm bảo tính nhất quán (Id, CreatedAt, UpdatedAt).
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
