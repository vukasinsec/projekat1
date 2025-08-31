using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using app.Models;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections;
using Microsoft.Extensions.Hosting;

namespace app.Controllers
{
    [Route("PostController")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly IGraphClient _graphClient;
        private readonly ILogger<PostController> _logger;

        public PostController(IGraphClient graphClient, ILogger<PostController> logger)
        {
            _graphClient = graphClient;
            _logger = logger;
        }



        // GET: /Post/{id}
        [HttpGet("{postId}")]
        public async Task<IActionResult> GetPostById(string postId)
        {
            try
            {
                // Upit za pronalaženje posta na osnovu postId
                var query = _graphClient.Cypher
                    .Match("(p:Post)")
                    .Where((Post p) => p.postId == postId)
                    .Return(p => p.As<Post>());

                // Izvršavanje upita i dobijanje rezultata
                var posts = await query.ResultsAsync;

                // Proverite da li je post pronađen
                var post = posts.FirstOrDefault(); // Uzima prvi post ili null ako nije pronađen

                if (post == null)
                {
                    return NotFound($"Post with ID {postId} not found.");
                }

                return Ok(post); // Vraća post sa statusom 200 OK
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        // GET: /Post
        [HttpGet("GetPosts")]
        public async Task<IActionResult> GetPosts([FromQuery] string userId)
        {
            try
            {

                // Ako je userId prosleđen, filtriraj postove tog korisnika
                if (!string.IsNullOrEmpty(userId))
                {
                    var query = _graphClient.Cypher
                        .Match("(u:User)-[:CREATED]->(p:Post)")
                        .Where((User u) => u.UserId == userId)
                        .Return(p => p.As<Post>());

                    var posts = await query.ResultsAsync;

                    return Ok(posts); // Vraća sve postove koji pripadaju korisniku sa userId
                }

                // Ako userId nije prosleđen, vrati sve postove
                var allPostsQuery = _graphClient.Cypher
                    .Match("(p:Post)")
                    .Return(p => p.As<Post>());

                var allPosts = await allPostsQuery.ResultsAsync;

                return Ok(allPosts); // Vraća sve postove
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }




        // PUT: /Post/{id}
        [HttpPut("{postId}")]
        public async Task<IActionResult> UpdatePost(string postId, [FromBody] Post updatedPost)
        {
            try
            {
                // Log input data
                Console.WriteLine($"Updating post {postId} with caption: {updatedPost.caption}");

                // Prvo, proverite da li post postoji
                var query = _graphClient.Cypher
                    .Match("(p:Post)")
                    .Where((Post p) => p.postId == postId)
                    .Return(p => p.As<Post>());

                var posts = await query.ResultsAsync;
                var post = posts.FirstOrDefault();

                if (post == null)
                {
                    return NotFound($"Post with ID {postId} not found.");
                }

                // Ažuriranje svojstava postojećeg posta sa novim podacima
                post.caption = updatedPost.caption ?? post.caption;
                post.imageURL = updatedPost.imageURL ?? post.imageURL;
                post.author = updatedPost.author ?? post.author;
                post.createdAt = updatedPost.createdAt != default ? updatedPost.createdAt : post.createdAt;
                post.likeCount = updatedPost.likeCount > 0 ? updatedPost.likeCount : post.likeCount;

                // Ažuriranje u bazi podataka
                await _graphClient.Cypher
                    .Match("(p:Post)")
                    .Where((Post p) => p.postId == postId)
                    .Set("p.caption = {caption}, p.imageURL = {imageURL}, p.author = {author}, p.createdAt = {createdAt}, p.likeCount = {likeCount}")
                    .WithParam("caption", post.caption)
                    .WithParam("imageURL", post.imageURL)
                    .WithParam("author", post.author)
                    .WithParam("createdAt", post.createdAt)
                    .WithParam("likeCount", post.likeCount)
                    .ExecuteWithoutResultsAsync();

                // Log successful update
                Console.WriteLine($"Post {postId} updated successfully.");

                //return Ok(new { success = true, post = post });
                return new JsonResult(new { success = true, post = post });

            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error updating post: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");

            }
        }

        // DELETE: /Post/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(string id)
        {
            try
            {
                // Proverite da li post postoji
                var postExists = await _graphClient.Cypher
                    .Match("(p:Post {postId: $postId})")
                    .WithParam("postId", id)
                    .Return<int>("count(p)")
                    .ResultsAsync;

                if (postExists.SingleOrDefault() == 0)
                {
                    return NotFound(new { message = "Post not found" });
                }

                // Brisanje svih veza i samog čvora
                await _graphClient.Cypher
                    .Match("(p:Post {postId: $postId})-[r]-()") // Pronalazi sve veze (relacije)
                    .WithParam("postId", id)
                    .Delete("r, p") // Briše veze i čvor
                    .ExecuteWithoutResultsAsync();

               // return NoContent(); // Uspešno obrisano
                                      return new JsonResult(new { success = true });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        [HttpPost("AddPost")]
        public async Task<IActionResult> AddPost([FromForm] IFormFile imageURL, [FromForm] string caption)
        {
            try
            {
                _logger.LogInformation("Kreiranje posta započeto.");

                if (imageURL == null || string.IsNullOrEmpty(caption))
                {
                    return BadRequest(new { error = "Slika i opis su obavezni." });
                }

                var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                if (!Directory.Exists(uploadsDirectory))
                {
                    Directory.CreateDirectory(uploadsDirectory);
                }

                var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized(new { error = "Korisnik nije autentifikovan." });
                }

                var filePath = Path.Combine(uploadsDirectory, imageURL.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageURL.CopyToAsync(stream);
                }

                var post = new Post
                {
                    postId = Guid.NewGuid().ToString(),
                    imageURL = "/images/" + imageURL.FileName,
                    caption = caption,
                    createdAt = DateTime.UtcNow,
                    likeCount = 0,
                    author = currentUserId
                };

                var parameters = new
                {
                    postId = post.postId,
                    imageURL = post.imageURL,
                    caption = post.caption,
                    createdAt = post.createdAt,
                    likeCount = post.likeCount,
                    userId = post.author
                };

                var result = await _graphClient.Cypher
                    .WithParams(parameters)
                    .Match("(u:User {userId: $userId})")
                    .Create("(p:Post {postId: $postId, imageURL: $imageURL, caption: $caption, createdAt: $createdAt, likeCount: $likeCount,author: $userId})")
                    .Create("(u)-[:CREATED]->(p)")
                    .Return(p => p.As<Post>())
                    .ResultsAsync;

                if (result == null || !result.Any())
                {
                    return StatusCode(500, new { error = "Nije moguće kreirati post." });
                }

                return new JsonResult(new { success = true, post = post });

                //return Ok(new { message = "Post uspešno kreiran.", post });
            }
            catch (Exception ex)
            {
                _logger.LogError("Došlo je do greške: " + ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

    }
}



