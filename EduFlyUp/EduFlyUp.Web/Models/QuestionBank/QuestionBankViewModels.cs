using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.QuestionBank;

public class QuestionBankIndexViewModel
{
    public Course Course { get; set; } = null!;
    public IEnumerable<Question> Questions { get; set; } = Enumerable.Empty<Question>();
    public string? CurrentSearch { get; set; }
    public string? CurrentDifficulty { get; set; }

    public int TotalQuestions => Questions.Count();
    public int EasyCount => Questions.Count(q => q.Difficulty == QuestionDifficulty.Easy);
    public int MediumCount => Questions.Count(q => q.Difficulty == QuestionDifficulty.Medium);
    public int HardCount => Questions.Count(q => q.Difficulty == QuestionDifficulty.Hard);
}

public class QuestionOptionInputModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nội dung đáp án không được để trống.")]
    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }
}

public class QuestionCreateEditViewModel
{
    public int Id { get; set; }

    [Required]
    public int CourseId { get; set; }

    public Course? Course { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi.")]
    [StringLength(1000, ErrorMessage = "Nội dung câu hỏi không được vượt quá 1000 ký tự.")]
    public string Content { get; set; } = string.Empty;

    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

    [Range(1, 100, ErrorMessage = "Điểm số câu hỏi phải từ 1 đến 100 điểm.")]
    public int Points { get; set; } = 1;

    public string? Explanation { get; set; }

    public List<QuestionOptionInputModel> Options { get; set; } = new();

    public int CorrectOptionIndex { get; set; } = 0;
}
