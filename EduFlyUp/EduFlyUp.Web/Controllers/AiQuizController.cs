using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/ai/quiz")]
public class AiQuizController : ControllerBase
{
    private readonly IAiQuizService _aiQuizService;

    public AiQuizController(IAiQuizService aiQuizService)
    {
        _aiQuizService = aiQuizService;
    }

    public class GenerateFromLessonRequest
    {
        public int LessonId { get; set; }
    }

    public class GenerateFromTextRequest
    {
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    // POST: /api/ai/quiz/generate-from-lesson
    [HttpPost("generate-from-lesson")]
    public async Task<IActionResult> GenerateFromLesson([FromBody] GenerateFromLessonRequest request, CancellationToken cancellationToken)
    {
        if (request.LessonId <= 0)
        {
            return BadRequest(new { success = false, message = "ID bài học không hợp lệ." });
        }

        var result = await _aiQuizService.GenerateQuizFromLessonMaterialAsync(request.LessonId, cancellationToken);
        return Ok(result);
    }

    // POST: /api/ai/quiz/generate-from-text
    [HttpPost("generate-from-text")]
    public async Task<IActionResult> GenerateFromText([FromBody] GenerateFromTextRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { success = false, message = "Nội dung văn bản không được để trống." });
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? "Tài liệu học tập" : request.Title;
        var result = await _aiQuizService.GenerateQuizFromCustomTextAsync(request.Content, title, cancellationToken);
        return Ok(result);
    }
}
