using System;

namespace SeBlog.Posts
{
    public class Post
    {
        public string Content { get; set; }

        public string Title { get; set; }

        public DateTime Published { get; set; }

        public string Description { get; set; }

        public string Key => GetKey(Published.Year, Published.Month, Published.Day, Title);

        public static string GetKey(int year, int month, int day, string title)
        {
            return $"posts/{year}/{month:00}/{day:00}/{title.Trim()}";
        }
    }
}