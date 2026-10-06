namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Lớp thực thể cơ sở (Base Entity) chứa các thuộc tính dùng chung.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
