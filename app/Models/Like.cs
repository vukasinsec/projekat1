namespace app.Models
{
    public class Like
    {
        public User user { get; set; }
        public Post post { get; set; }
      

        public Like(User user, Post post) { 
            this.user = user;
            this.post = post;
           
        }
    }
}
