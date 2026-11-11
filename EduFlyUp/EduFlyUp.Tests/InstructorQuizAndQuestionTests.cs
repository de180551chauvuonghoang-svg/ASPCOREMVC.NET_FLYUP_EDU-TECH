using System.Net;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using EduFlyUp.Services;

namespace EduFlyUp.Tests;

public sealed class InstructorQuizAndQuestionTests
{
    [Theory]
    [InlineData(CourseStatus.Draft, false, CourseStatus.Draft)]
    [InlineData(CourseStatus.PendingApproval, false, CourseStatus.PendingApproval)]
    [InlineData(CourseStatus.Rejected, false, CourseStatus.Rejected)]
    [InlineData(CourseStatus.Archived, false, CourseStatus.Archived)]
    [InlineData(CourseStatus.Approved, false, CourseStatus.Rejected)]
    [InlineData(CourseStatus.Approved, true, CourseStatus.Approved)]
    [InlineData(CourseStatus.PendingApproval, true, CourseStatus.Approved)]
    public void GetEffectiveStatus_maps_stale_publication_flags_consistently(
        CourseStatus storedStatus,
        bool isPublished,
        CourseStatus expected)
    {
        var course = new Course { Status = storedStatus, IsPublished = isPublished };
        Assert.Equal(expected, course.GetEffectiveStatus());
    }

    [Fact]
    public async Task AssignQuestionsToQuiz_rejects_missing_question_ids_and_deduplicates_valid_ones()
    {
        var quizRepo = new StubQuizRepository(new Quiz { Id = 10, CourseId = 1, Title = "Midterm" });
        var questionRepo = new StubQuestionRepository(
            new Question { Id = 101, CourseId = 1, Content = "Q1" },
            new Question { Id = 102, CourseId = 2, Content = "Other course" }
        );
        var service = new QuizService(quizRepo, questionRepo);

        var missingResult = await service.AssignQuestionsToQuizAsync(10, new[] { 101, 999 });
        Assert.False(missingResult.Success);
        Assert.Null(quizRepo.LastAssignedIds);

        var crossCourseResult = await service.AssignQuestionsToQuizAsync(10, new[] { 101, 102 });
        Assert.False(crossCourseResult.Success);
        Assert.Null(quizRepo.LastAssignedIds);

        var duplicateResult = await service.AssignQuestionsToQuizAsync(10, new[] { 101, 101, 0, -5 });
        Assert.True(duplicateResult.Success);
        Assert.NotNull(quizRepo.LastAssignedIds);
        Assert.Equal(new[] { 101 }, quizRepo.LastAssignedIds!);
    }

    [Fact]
    public async Task Quiz_ManageQuestions_view_renders_styles_section_and_safe_onchange_encoding()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(new Course { Id = 1, Title = "CAT Course", InstructorId = "owner@example.test" });
        app.Questions.Questions.Add(new Question
        {
            Id = 1,
            CourseId = 1,
            Content = "What does \"CAT\" & 'quoting' do?",
            Difficulty = QuestionDifficulty.Medium,
            Options = [new() { Id = 1, Text = "A", IsCorrect = true }, new() { Id = 2, Text = "B", IsCorrect = false }]
        });
        app.Quizzes.Quizzes.Add(new Quiz
        {
            Id = 2,
            CourseId = 1,
            Title = "CAT Midterm",
            DurationMinutes = 30,
            PassingScorePercentage = 70,
            IsCAT = true
        });
        await app.CreateUserAsync("owner@example.test", "Instructor");

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/Quiz/ManageQuestions/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(".cursor-pointer", html);
        Assert.Contains("CAT Midterm", html);
        Assert.Contains("\\u0022CAT\\u0022", html);
    }

    private sealed class StubQuizRepository(Quiz quiz) : IQuizRepository
    {
        public IReadOnlyList<int>? LastAssignedIds { get; private set; }

        public Task<IEnumerable<Quiz>> GetByCourseIdAsync(int courseId) => Task.FromResult<IEnumerable<Quiz>>([quiz]);
        public Task<Quiz?> GetByIdAsync(int id) => Task.FromResult(id == quiz.Id ? quiz : null);
        public Task<Quiz?> GetByIdWithQuestionsAsync(int id) => Task.FromResult(id == quiz.Id ? quiz : null);
        public Task<Quiz> AddAsync(Quiz item) => Task.FromResult(item);
        public Task UpdateAsync(Quiz item) => Task.CompletedTask;
        public Task DeleteAsync(int id) => Task.CompletedTask;
        public Task<int> CountByCourseIdAsync(int courseId) => Task.FromResult(1);

        public Task SetQuestionsForQuizAsync(int quizId, IEnumerable<int> questionIds)
        {
            LastAssignedIds = questionIds.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class StubQuestionRepository(params Question[] questions) : IQuestionRepository
    {
        private readonly List<Question> _items = [.. questions];

        public Task<IEnumerable<Question>> GetByCourseIdAsync(int courseId, QuestionDifficulty? difficulty = null, string? search = null)
            => Task.FromResult<IEnumerable<Question>>(_items.Where(q => q.CourseId == courseId).ToList());
        public Task<Question?> GetByIdAsync(int id) => Task.FromResult(_items.FirstOrDefault(q => q.Id == id));
        public Task<Question> AddAsync(Question question) => Task.FromResult(question);
        public Task UpdateAsync(Question question, IEnumerable<QuestionOption> options) => Task.CompletedTask;
        public Task DeleteAsync(int id) => Task.CompletedTask;
        public Task<int> CountByCourseIdAsync(int courseId) => Task.FromResult(_items.Count(q => q.CourseId == courseId));
        public Task<IEnumerable<Question>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var idSet = ids.ToHashSet();
            return Task.FromResult<IEnumerable<Question>>(_items.Where(q => idSet.Contains(q.Id)).ToList());
        }
    }
}

