using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Lessons
{
    public class LessonSectionGroup
    {
        public string SectionName { get; set; } = string.Empty;
        public List<Lesson> Lessons { get; set; } = new();
        public int TotalDurationMinutes => Lessons.Sum(l => l.DurationMinutes);
    }

    public class LearnLessonViewModel
    {
        public Lesson CurrentLesson { get; set; } = null!;
        public Course Course { get; set; } = null!;
        public List<Lesson> AllLessons { get; set; } = new();
        public List<LessonSectionGroup> Sections { get; set; } = new();
        
        public Lesson? PreviousLesson { get; set; }
        public Lesson? NextLesson { get; set; }

        public bool IsEnrolled { get; set; }
        public bool CanManageCourse { get; set; }
        public bool IsPreviewAllowed => CurrentLesson.IsPreview || IsEnrolled || CanManageCourse;

        public EduFlyUp.BusinessObjects.Quiz? CourseQuiz { get; set; }

        public int CurrentLessonIndex => AllLessons.FindIndex(l => l.Id == CurrentLesson.Id) + 1;
        public int TotalLessonsCount => AllLessons.Count;
        public int TotalCourseDurationMinutes => AllLessons.Sum(l => l.DurationMinutes);

        /// <summary>
        /// Chuyển đổi URL Video sang định dạng Embed nếu là Youtube link
        /// </summary>
        public string GetEmbedVideoUrl()
        {
            if (string.IsNullOrWhiteSpace(CurrentLesson.VideoUrl)) return string.Empty;

            var url = CurrentLesson.VideoUrl.Trim();

            // Nếu là Youtube watch URL
            if (url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var v = query["v"];
                if (!string.IsNullOrEmpty(v))
                {
                    return $"https://www.youtube.com/embed/{v}?autoplay=0&rel=0";
                }
            }

            // Nếu là youtu.be short URL
            if (url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
            {
                var id = url.Substring(url.LastIndexOf('/') + 1).Split('?')[0];
                return $"https://www.youtube.com/embed/{id}?autoplay=0&rel=0";
            }

            return url;
        }

        public bool IsYoutubeVideo()
        {
            if (string.IsNullOrWhiteSpace(CurrentLesson.VideoUrl)) return false;
            return CurrentLesson.VideoUrl.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                   CurrentLesson.VideoUrl.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
        }
    }
}
