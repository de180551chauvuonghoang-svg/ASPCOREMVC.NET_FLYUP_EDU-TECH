namespace EduFlyUp.BusinessObjects;

public sealed record AdminDashboardStats(
    int TotalUsers, int ActiveUsers, int InactiveUsers,
    int TotalCourses, int PublishedCourses, int HiddenCourses, int TotalLessons);
