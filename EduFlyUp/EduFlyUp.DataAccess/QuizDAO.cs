using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public class QuizDAO : SingletonBase<QuizDAO>
    {
        public async Task<IEnumerable<Quiz>> GetByCourseIdAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Quizzes
                .Include(q => q.QuizQuestions)
                .Where(q => q.CourseId == courseId)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<Quiz?> GetByIdAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Quizzes.FindAsync(id);
        }

        public async Task<Quiz?> GetByIdWithQuestionsAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Quizzes
                .Include(q => q.QuizQuestions.OrderBy(qq => qq.Order))
                    .ThenInclude(qq => qq.Question)
                        .ThenInclude(ques => ques.Options)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<Quiz> AddAsync(Quiz quiz)
        {
            using var context = new AppDbContext();
            await context.Quizzes.AddAsync(quiz);
            await context.SaveChangesAsync();
            return quiz;
        }

        public async Task UpdateAsync(Quiz quiz)
        {
            using var context = new AppDbContext();
            var existingQuiz = await context.Quizzes.FindAsync(quiz.Id);
            if (existingQuiz != null)
            {
                existingQuiz.Title = quiz.Title;
                existingQuiz.Description = quiz.Description;
                existingQuiz.DurationMinutes = quiz.DurationMinutes;
                existingQuiz.PassingScorePercentage = quiz.PassingScorePercentage;
                existingQuiz.IsCAT = quiz.IsCAT;

                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int id)
        {
            using var context = new AppDbContext();
            var quiz = await context.Quizzes
                .Include(q => q.QuizQuestions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz != null)
            {
                context.Quizzes.Remove(quiz);
                await context.SaveChangesAsync();
            }
        }

        public async Task AddQuestionToQuizAsync(int quizId, int questionId, int order = 1)
        {
            using var context = new AppDbContext();
            var exists = await context.QuizQuestions
                .AnyAsync(qq => qq.QuizId == quizId && qq.QuestionId == questionId);

            if (!exists)
            {
                var quizQuestion = new QuizQuestion
                {
                    QuizId = quizId,
                    QuestionId = questionId,
                    Order = order
                };
                await context.QuizQuestions.AddAsync(quizQuestion);
                await context.SaveChangesAsync();
            }
        }

        public async Task RemoveQuestionFromQuizAsync(int quizId, int questionId)
        {
            using var context = new AppDbContext();
            var item = await context.QuizQuestions
                .FirstOrDefaultAsync(qq => qq.QuizId == quizId && qq.QuestionId == questionId);

            if (item != null)
            {
                context.QuizQuestions.Remove(item);
                await context.SaveChangesAsync();
            }
        }

        public async Task SetQuestionsForQuizAsync(int quizId, IEnumerable<int> questionIds)
        {
            using var context = new AppDbContext();
            var existingItems = await context.QuizQuestions
                .Where(qq => qq.QuizId == quizId)
                .ToListAsync();

            context.QuizQuestions.RemoveRange(existingItems);

            int order = 1;
            foreach (var qId in questionIds.Distinct())
            {
                context.QuizQuestions.Add(new QuizQuestion
                {
                    QuizId = quizId,
                    QuestionId = qId,
                    Order = order++
                });
            }

            await context.SaveChangesAsync();
        }

        public async Task<int> CountByCourseIdAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Quizzes.CountAsync(q => q.CourseId == courseId);
        }
    }
}
