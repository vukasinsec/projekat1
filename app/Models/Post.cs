using Neo4j.Driver;
using Neo4jClient.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.Models
{
    public class Post
    {
        [JsonPropertyName("postId")]

        public String?  postId { get; set; }
        public String? imageURL { get; set; }
        public String? caption { get; set; }

        
        public DateTime createdAt { get; set; }
        public String? author { get; set; }
        public List<User> likes { get; set; }=new List<User>();
        public int likeCount { get; set; }

        public Post()
        {
        }
        public Post(String postId, String imageURL, String caption, DateTime createdAt, String author, int lk)
        {
            this.postId = postId;
            this.imageURL = imageURL;
            this.caption = caption;
            this.createdAt = createdAt;
            this.author = author;
            this.likeCount = lk;
        }

        //public void like(User user)
        //{
        //    likes.add(user);
        //    likeCount = likes.size(); 
        //}

        //public void unlike(User user)
        //{
        //    likes.remove(user);
        //    likeCount = likes.size(); 
        //}


    }

}
