using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Models;
using Neo4j.Driver;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Claims;

namespace app.Controllers
{
    [Route("[controller]")]
    [ApiController]

    public class CommentController : ControllerBase
    {
        private readonly IGraphClient _graphClient;

        public CommentController(IGraphClient graphClient)
        {
            _graphClient = graphClient;
        }

        // POST: /Comment
        [HttpPost]
        [HttpPost("AddComment")]
        public async Task<IActionResult> AddComment([FromBody] Comment comment)
        {
            try
            {
                Console.WriteLine($"Received Comment Data: {JsonSerializer.Serialize(comment)}");

                if (comment.Author == null || string.IsNullOrEmpty(comment.Author.UserId) ||
                    comment.Post == null || string.IsNullOrEmpty(comment.Post.postId))
                {
                    return BadRequest("Author UserId and PostId are required.");
                }

                var loggedInUserId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                comment.Author = new User { UserId = loggedInUserId };

                // Proveri postojanje korisnika i posta
                var existsQuery = await _graphClient.Cypher
                    .Match("(u:User {userId: $authorId})", "(p:Post {postId: $postId})")
                    .WithParams(new
                    {
                        authorId = loggedInUserId,
                        postId = comment.Post.postId
                    })
                    .Return((u, p) => new
                    {
                        UserExists = u != null,
                        PostExists = p != null
                    })
                    .ResultsAsync;

                var existsResult = existsQuery.FirstOrDefault();

                if (existsResult == null || !existsResult.UserExists || !existsResult.PostExists)
                {
                    return NotFound(new { Message = "Author or Post not found." });
                }

                // Generiši CommentId ako nije prosleđen
                if (string.IsNullOrEmpty(comment.CommentId))
                {
                    comment.CommentId = Guid.NewGuid().ToString();
                }

                var createdAt = DateTime.UtcNow;

                // Kreiraj komentar i poveži ga sa korisnikom i postom
                await _graphClient.Cypher
                    .Match("(u:User {userId: $authorId})", "(p:Post {postId: $postId})")
                    .WithParams(new
                    {
                        comment.CommentId,
                        content = comment.Content,
                        authorId = loggedInUserId,
                        postId = comment.Post.postId,
                        createdAt
                    })
                    .Create("(c:Comment {commentId: $CommentId, content: $content, createdAt: $createdAt})")
                    .Create("(u)-[:AUTHORED]->(c)")
                    .Create("(c)-[:BELONGS_TO]->(p)")
                    .ExecuteWithoutResultsAsync();

                Console.WriteLine($"CommentId: {comment.CommentId}");
                Console.WriteLine($"Content: {comment.Content}");
                Console.WriteLine($"AuthorId: {comment.Author?.UserId}");
                Console.WriteLine($"PostId: {comment.Post?.postId}");
                return Ok(new
                {
                    Message = "Comment created successfully.",
                    Comment = new
                    {
                        CommentId = comment.CommentId,
                        Content = comment.Content,
                        CreatedAt = createdAt,
                        AuthorId = loggedInUserId,
                        PostId = comment.Post.postId
                    }
                });

               

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("GetComments/{postId}")]
        public async Task<IActionResult> GetComments(string postId)
        {
            try
            {
                if (string.IsNullOrEmpty(postId))
                {
                    Console.WriteLine("Invalid postId provided.");
                    return BadRequest(new { Message = "Invalid postId." });
                }

                // Izvrši upit sa ispravnim povratnim tipom
                var commentsQuery = await _graphClient.Cypher
                    .Match("(p:Post {postId: $postId})<-[:BELONGS_TO]-(c:Comment)<-[:AUTHORED]-(u:User)")
                    .WithParam("postId", postId)
                    .Return((c, u) => new
                    {
                        Comment = c.As<Comment>(),
                        Author = u.As<User>()
                    })
                    .ResultsAsync;

                // Logujte svaki rezultat da proverite šta je vraćeno
                foreach (var result in commentsQuery)
                {
                    Console.WriteLine($"CommentId: {result.Comment.CommentId}, Content: {result.Comment.Content}");
                    Console.WriteLine($"AuthorName: {result.Author.Username}");
                }

                // Proveri da li ima komentara
                if (commentsQuery == null || !commentsQuery.Any())
                {
                    Console.WriteLine($"No comments found for postId: {postId}");
                    return NotFound(new { Message = "No comments found for the provided postId." });
                }

                // Mapiraj rezultate u listu sa selektovanim svojstvima
                var mappedComments = commentsQuery.Select(c => new
                {
                    CommentId = c.Comment.CommentId,
                    Content = c.Comment.Content,
                    CreatedAt = c.Comment.CreatedAt?.ToString("o") ?? "No Date",
                    AuthorName = c.Author.Username
                }).ToList();

                // Ispis broja pronađenih komentara
                Console.WriteLine($"Fetched {mappedComments.Count} comments for postId {postId}.");
                return Ok(mappedComments);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching comments for postId {postId}: {ex}");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPut("EditComment/{commentId}")]
        public async Task<IActionResult> EditComment(string commentId, [FromBody] string content)
        {
            try
            {
                // Proveri da li je sadržaj validan
                if (string.IsNullOrEmpty(content))
                {
                    return BadRequest(new { Message = "Content is required." });
                }

                // Proveri da li komentar sa zadatim ID-jem postoji
                var existingComment = await _graphClient.Cypher
                    .Match("(c:Comment {commentId: $commentId})")
                    .WithParam("commentId", commentId)
                    .Return(c => c.As<Comment>())
                    .ResultsAsync;

                if (existingComment == null || !existingComment.Any())
                {
                    return NotFound(new { Message = "Comment not found." });
                }

                // Ažuriraj sadržaj komentara
                await _graphClient.Cypher
                    .Match("(c:Comment {commentId: $commentId})")
                    .Set("c.content = $content")
                    .WithParams(new
                    {
                        commentId = commentId,
                        content = content
                    })
                    .ExecuteWithoutResultsAsync();

                return Ok(new { Message = "Comment updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("LikeComment")]
        public async Task<IActionResult> LikeComment([FromBody] LikeRequest likeRequest)
        {
            // Validacija ulaznih podataka
            if (likeRequest == null || string.IsNullOrEmpty(likeRequest.UserId) || string.IsNullOrEmpty(likeRequest.CommentId))
            {
                return BadRequest("Invalid data.");
            }

            try
            {
                // Pronalaženje korisnika i komentara
                var userNode = await _graphClient.Cypher
                    .Match("(user:User)")
                    .Where((User user) => user.UserId == likeRequest.UserId)
                    .Return(user => user.As<User>())
                    .ResultsAsync;

                var commentNode = await _graphClient.Cypher
                    .Match("(comment:Comment)")
                    .Where((Comment comment) => comment.CommentId == likeRequest.CommentId)
                    .Return(comment => comment.As<Comment>())
                    .ResultsAsync;

                if (userNode == null || commentNode == null)
                {
                    return NotFound("User or Comment not found.");
                }

                // Dodavanje lajka (LIKES veza)
                await _graphClient.Cypher
                    .Match("(user:User)", "(comment:Comment)")
                    .Where((User user) => user.UserId == likeRequest.UserId)
                    .AndWhere((Comment comment) => comment.CommentId == likeRequest.CommentId)
                    .Create("(user)-[:LIKES]->(comment)")
                    .ExecuteWithoutResultsAsync();

                // Opcionalno: Ažurirajte broj lajkova za komentar
                await _graphClient.Cypher
                    .Match("(comment:Comment)")
                    .Where((Comment comment) => comment.CommentId == likeRequest.CommentId)
                    .Set("comment.likeCount = coalesce(comment.likeCount, 0) + 1")
                    .ExecuteWithoutResultsAsync();

                return Ok(new { message = "Komentar lajkovan" });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        public class LikeRequest
        {
            public string UserId { get; set; }
            public string CommentId { get; set; }
        }

        //[HttpPost("LikeComment")]
        //public async Task<IActionResult> LikeComment([FromBody] Like like)
        //{
        //    try
        //    {
        //        if (like.user == null || like.comment == null)
        //        {
        //            return BadRequest("User or Comment is missing.");
        //        }

        //        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        //        var parameters = new
        //        {
        //            userId,
        //            commentId = like.comment.CommentId
        //        };

        //        // Proveri da li je korisnik već lajkovao komentar
        //        var alreadyLiked = await _graphClient.Cypher
        //            .Match("(u:User {userId: $userId})", "(c:Comment {commentId: $commentId})")
        //            .Where("(u)-[:LIKES]->(c)")
        //            .WithParams(parameters)
        //            .Return<int>("count(*)")
        //            .ResultsAsync;
        //        bool isLiked = alreadyLiked.FirstOrDefault() > 0;

        //        // Ako je komentar već lajkovan, unlikuj ga
        //        if (alreadyLiked.FirstOrDefault() > 0)
        //        {
        //            // Pozovi logiku za unlikovanje
        //            await _graphClient.Cypher
        //                .Match("(u:User {userId: $userId})-[r:LIKES]->(c:Comment {commentId: $commentId})")
        //                .WithParams(parameters)
        //                .Delete("r") // Briši relaciju LAJK
        //                .ExecuteWithoutResultsAsync();

        //            return Ok(new { success = true, isLiked });
        //        }
        //        else
        //        {
        //            // Ako komentar nije lajkovan, lajkuj ga
        //            await _graphClient.Cypher
        //                .Match("(u:User {userId: $userId})", "(c:Comment {commentId: $commentId})")
        //                .WithParams(parameters)
        //                .Create("(u)-[:LIKES]->(c)") // Kreiraj LAJK relaciju
        //                .ExecuteWithoutResultsAsync();

        //            return Ok(new { success = true, isLiked });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { Error = ex.Message });
        //    }
        //}


        //da se proveri ova get metoda, varaca mi 404, a u bazi posotij taj podatak
        //NE ZNAM STO NECE!!!!!!!!!!!!!!!!!!!!
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCommentById(string id)
        {
            try
            {
                // Logovanje vrednosti id
                Console.WriteLine($"Looking for Comment with ID: {id}");

                var query = await _graphClient.Cypher
                    // Match Comment sa njegovim vezama ka User i Post
                    .Match("(c:Comment {commentId: $commentId})-[:AUTHORED]->(u:User), (c)-[:BELONGS_TO]->(p:Post)")
                    .WithParam("commentId", id)
                    // Vraćanje podataka o Comment-u, Author-u i Post-u
                    .Return((c, u, p) => new
                    {
                        Comment = c.As<Comment>(),
                        Author = u.As<User>(),
                        Post = p.As<Post>()
                    })
                    .ResultsAsync;

                var result = query.FirstOrDefault();

                if (result == null)
                {
                    return NotFound(new { Message = "Comment not found" });
                }

                var commentData = result;

                var comment = commentData.Comment;
                comment.Author = commentData.Author;
                comment.Post = commentData.Post;

                return Ok(comment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }





        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(string id, [FromBody] Comment updatedComment)
        {
            if (string.IsNullOrEmpty(updatedComment.CommentId) || string.IsNullOrEmpty(updatedComment.Content))
            {
                return BadRequest("CommentId and Content are required.");
            }

            try
            {
                // Proverite da li je CommentId ispravan
                var query = await _graphClient.Cypher
                    .Match("(c:Comment {commentId: $commentId})")
                    .WithParam("commentId", id)
                    .Set("c.content = $content, c.createdAt = $createdAt")
                    .WithParams(new
                    {
                        content = updatedComment.Content,
                        createdAt = updatedComment.CreatedAt
                    })
                    .Return(c => c.As<Comment>())
                    .ResultsAsync;

                var result = query.FirstOrDefault();

                if (result == null)
                {
                    return NotFound(new { Message = "Comment not found" });
                }

                return Ok(new { Message = "Comment updated successfully", UpdatedComment = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }



        [HttpDelete("DeleteComment/{id}")]
        public async Task<IActionResult> DeleteComment(string id)
        {
            try
            {
                Console.WriteLine($"Attempting to delete comment with ID: {id}");

                // Cypher query to delete the comment
                await _graphClient.Cypher
                    .Match("(c:Comment {commentId: $commentId})")
                    .WithParam("commentId", id)
                    .DetachDelete("c")
                    .ExecuteWithoutResultsAsync();

                // Return success message
                return Ok(new { Message = "Comment deleted successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while deleting comment with ID: {id}. Error: {ex.Message}");

                // Handle errors
                return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }


    }
}