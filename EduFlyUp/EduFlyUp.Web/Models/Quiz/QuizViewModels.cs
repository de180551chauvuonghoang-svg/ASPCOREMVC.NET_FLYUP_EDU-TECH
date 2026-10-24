using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Quiz;

public class QuizIndexViewModel
{
    public Course Course { get; set; } = null!;
    public IEnumerable<EduFlyUp.BusinessObjects.Quiz> Quizzes { get; set; } = new List<EduFlyUp.BusinessObjects.Quiz>();
    public int TotalQuizzes { get; set; }
    public int AdaptiveQuizzes { get; set; }
    public int TotalQuestionsAssigned { get; set; }
    public double AveragePassingScore { get; set; }
}

public class QuizCreateEditViewModel
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public string? CourseTitle { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề đề thi.")]
    [StringLength(200, ErrorMessage = "Tiêu đề đề thi không được vượt quá 200 ký tự.")]
    [Display(Name = "Tiêu đề đề thi")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    [Display(Name = "Mô tả đề thi")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập thời gian làm bài.")]
    [Range(1, 300, ErrorMessage = "Thời gian làm bài phải từ 1 đến 300 phút.")]
    [Display(Name = "Thời gian làm bài (phút)")]
    public int DurationMinutes { get; set; } = 30;

    [Required(ErrorMessage = "Vui lòng nhập điểm sàn đạt yêu cầu.")]
    [Range(1, 100, ErrorMessage = "Điểm sàn phần trăm phải từ 1% đến 100%.")]
    [Display(Name = "Điểm sàn đạt (%)")]
    public int PassingScorePercentage { get; set; } = 70;

    [Display(Name = "Chế độ thi thích ứng CAT (Computer Adaptive Testing)")]
    public bool IsCAT { get; set; } = false;
}

public class QuizManageQuestionsViewModel
{
    public EduFlyUp.BusinessObjects.Quiz Quiz { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public List<Question> AssignedQuestions { get; set; } = new();
    public List<Question> AvailableQuestions { get; set; } = new();
    public List<int> SelectedQuestionIds { get; set; } = new();
}
