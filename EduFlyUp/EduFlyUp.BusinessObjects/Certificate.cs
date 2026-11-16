namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho Chứng chỉ tốt nghiệp khóa học (Certificate) được cấp khi học viên đạt >= 95% điểm Final Test
/// </summary>
public class Certificate : BaseEntity
{
    public string CertificateCode { get; set; } = string.Empty; // Mã chứng chỉ duy nhất: ví dụ CERT-2026-987654
    
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;

    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;

    public string StudentName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public double FinalScore { get; set; } // Điểm số bài thi cuối khóa (>= 95%)
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string VerificationUrl { get; set; } = string.Empty;
}
