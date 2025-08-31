    using Neo4j.Driver;
    using Newtonsoft.Json;
    using System;

    namespace app.Models
    {
        public class User
        {
            [JsonProperty("userId")]
            public string? UserId { get; set; }
            // This will be generated automatically
            [JsonProperty("username")]
            public string? Username { get; set; }

            [JsonProperty("fullName")]
            public string? FullName { get; set; }

            [JsonProperty("email")]
            public string? Email { get; set; }

            [JsonProperty("passwordHash")]

            public string? PasswordHash { get; set; }

            [JsonProperty("profilePicture")]

        public string ProfilePicture { get; set; }  // Menjajte u IFormFile

        [JsonProperty("bio")]

            public string? Bio { get; set; }

            [JsonProperty("createdAt")]

            public DateTime CreatedAt { get; set; }

            [JsonProperty("isAdmin")]

            public bool IsAdmin { get; set; }
            [JsonProperty("postovi")]

            public List<Post>? postovi { get; set; }
            [JsonProperty("prijatelji")]

            public List<User>? prijatelji { get; set; }

            // Parameterless constructor (Required for proper deserialization)
            public User() { }

            //// Constructor for initialization if needed
            public User(string username, string fullName, string email, string passwordHash,
                        string profilePicture, string bio, DateTime createdAt, bool isAdmin)
            {
                Username = username;
                FullName = fullName;
                Email = email;
                PasswordHash = passwordHash;
                ProfilePicture = profilePicture;
                Bio = bio;
                CreatedAt = createdAt;
                IsAdmin = isAdmin;
            }

            public bool isAdmin()
            {
                return IsAdmin;
            }
        }


    }
