using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
using System;
using System.Threading.Tasks;
using app.Models;
using System.Security.Claims;

namespace app.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LikeController : ControllerBase
    {
        private readonly IGraphClient _graphClient;

        public LikeController(IGraphClient graphClient)
        {
            _graphClient = graphClient;
        }

        // POST: /like
        [HttpPost("LikePost")]
        public async Task<IActionResult> LikePost([FromBody] Like like)
        {
            try
            {
                if (like.user == null || like.post == null)
                {
                    return BadRequest("User or Post is missing.");
                }
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                var parameters = new
                {
                    userId,
                    postId = like.post.postId
                };

                // Proveri da li je korisnik već lajkovao post
                var alreadyLiked = await _graphClient.Cypher
                    .Match("(u:User {userId: $userId})", "(p:Post {postId: $postId})")
                    .Where("(u)-[:LIKES]->(p)")
                    .WithParams(parameters)
                    .Return<int>("count(*)")
                    .ResultsAsync;
                bool isLiked = alreadyLiked.FirstOrDefault() > 0;

                // Ako je post već lajkovan, unlikuj ga
                if (alreadyLiked.FirstOrDefault() > 0)
                {
                    // Pozovi logiku za unlikovanje
                    await _graphClient.Cypher
                        .Match("(u:User {userId: $userId})-[r:LIKES]->(p:Post {postId: $postId})")
                        .WithParams(parameters)
                        .Delete("r") // Briši relaciju LAJK
                        .Set("p.likeCount = coalesce(p.likeCount, 0) - 1") // Dekrementiraj broj lajkova
                        .ExecuteWithoutResultsAsync();

                    // Vrati ažurirani broj lajkova nakon unlikovanja
                    var updatedLikeCount = await _graphClient.Cypher
                        .Match("(p:Post {postId: $postId})")
                        .WithParams(parameters)
                        .Return<int>("p.likeCount")
                        .ResultsAsync;

                    return Ok(new { success = true, likeCount = updatedLikeCount.FirstOrDefault() });
                }
                else
                {
                    // Ako post nije lajkovan, lajkuj ga
                    await _graphClient.Cypher
                        .Match("(u:User {userId: $userId})", "(p:Post {postId: $postId})")
                        .WithParams(parameters)
                        .Create("(u)-[:LIKES]->(p)")
                        .Set("p.likeCount = coalesce(p.likeCount, 0) + 1") // Inkrementiraj broj lajkova
                        .ExecuteWithoutResultsAsync();

                    // Vrati ažurirani broj lajkova nakon lajkovanja
                    var updatedLikeCount = await _graphClient.Cypher
                        .Match("(p:Post {postId: $postId})")
                        .WithParams(parameters)
                        .Return<int>("p.likeCount")
                        .ResultsAsync;

                    return Ok(new { success = true, likeCount = updatedLikeCount.FirstOrDefault(), isLiked });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        // DELETE: /like
        [HttpDelete("UnlikePost")]
        public async Task<IActionResult> UnlikePost([FromBody] Like like)
        {
            try
            {
                if (like.user == null || like.post == null)
                {
                    return BadRequest("User or Post is missing.");
                }

                var parameters = new
                {
                    userId = like.user.UserId,
                    postId = like.post.postId
                };

                // Proveri da li je korisnik lajkovao post
                var alreadyLiked = await _graphClient.Cypher
                    .Match("(u:User {userId: $userId})", "(p:Post {postId: $postId})")
                    .Where("(u)-[:LIKES]->(p)") // Proveri da li postoji veza LAJKA
                    .WithParams(parameters)
                    .Return<int>("count(*)") // Broj veza između korisnika i posta
                    .ResultsAsync;

                if (alreadyLiked.FirstOrDefault() == 0)
                {
                    return BadRequest(new { Error = "User has not liked this post." });
                }

                // Obriši 'LIKE' relaciju i ažuriraj `likeCount`
                await _graphClient.Cypher
                    .Match("(u:User {userId: $userId})-[r:LIKES]->(p:Post {postId: $postId})")
                    .WithParams(parameters)
                    .Delete("r") // Briši relaciju
                    .Set("p.likeCount = p.likeCount - 1") // Smanji likeCount
                    .ExecuteWithoutResultsAsync();

                // Vrati ažurirani broj lajkova
                var updatedLikeCount = await _graphClient.Cypher
                    .Match("(p:Post {postId: $postId})")
                    .WithParams(parameters)
                    .Return<int>("p.likeCount")
                    .ResultsAsync;

                return Ok(new { success = true, likeCount = updatedLikeCount.FirstOrDefault() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
        [HttpGet("IsPostLikedByUser")]
        public async Task<IActionResult> IsPostLikedByUser(string userId, string postId)
        {
            try
            {
                var isLiked = await _graphClient.Cypher
                    .Match("(u:User {userId: $userId})", "(p:Post {postId: $postId})")
                    .Where("(u)-[:LIKES]->(p)")
                    .WithParams(new { userId, postId })
                    .Return<int>("count(*)")
                    .ResultsAsync;

                return Ok(isLiked.FirstOrDefault() > 0);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

    }
}