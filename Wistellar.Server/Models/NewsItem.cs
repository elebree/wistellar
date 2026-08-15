using System.Text.Json.Serialization;

namespace Wistellar.Server.Models
{
    /// <summary>
    /// Represents a single news item from WiGLE.
    /// Not thread-safe.
    /// </summary>
    public class NewsItem
    {
        /// <summary>
        /// Gets or sets the subject/title of the news item.
        /// </summary>
        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        /// <summary>
        /// Gets or sets the post date of the news item.
        /// </summary>
        [JsonPropertyName("postDate")]
        public string PostDate { get; set; } = "";

        /// <summary>
        /// Gets or sets the URL link to the news item.
        /// </summary>
        [JsonPropertyName("link")]
        public string Link { get; set; } = "";

        /// <summary>
        /// Gets or sets the story/content of the news item (may contain BBCode).
        /// </summary>
        [JsonPropertyName("story")]
        public string Story { get; set; } = "";

        /// <summary>
        /// Gets or sets the unique identifier for the story.
        /// </summary>
        [JsonPropertyName("storyId")]
        public string StoryId { get; set; } = "";

        /// <summary>
        /// Gets or sets a value indicating whether there is more content available.
        /// </summary>
        [JsonPropertyName("more")]
        public bool More { get; set; }

        /// <summary>
        /// Gets or sets the username of the author.
        /// </summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="NewsItem"/> class.
        /// </summary>
        public NewsItem() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NewsItem"/> class with required properties.
        /// </summary>
        /// <param name="subject">The subject/title of the news item.</param>
        /// <param name="story">The story/content of the news item.</param>
        /// <param name="userName">The username of the author.</param>
        /// <param name="postDate">The post date of the news item.</param>
        /// <param name="link">The URL link to the news item.</param>
        [JsonConstructor]
        public NewsItem(string subject, string story, string userName, string postDate, string link)
        {
            Subject = subject;
            Story = story;
            UserName = userName;
            PostDate = postDate;
            Link = link;
        }
    }
}
