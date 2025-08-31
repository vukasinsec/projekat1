using app.Models;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

public class Comment
{
    [JsonProperty("commentId")]
    public string CommentId { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty("likeCount")]
    public int LikeCount { get; set; }


    // Ove atribute možete ostaviti praznim ako nisu deo odgovora iz baze
    public User Author { get; set; }
    public Post Post { get; set; }

    public Comment() { }

    public Comment(string commentId, string content, DateTime createdAt, User author, Post post)
    {
        CommentId = commentId;
        Content = content;
        CreatedAt = createdAt;
        Author = author;
        Post = post;
    }
}