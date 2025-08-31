using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
using System;
using System.Linq;
using System.Threading.Tasks;
using app.Models;
using Neo4j.Driver;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace app.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : Controller
    {
        private readonly IGraphClient _graphClient;

        public UserController(IGraphClient graphClient)
        {
            _graphClient = graphClient;
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (user == null)
            {
                return BadRequest("User data is required.");
            }

            // You can add more validation if needed
            if (string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Email))
            {
                return BadRequest("Username and Email are required.");
            }

            try
            {
                // Generate a unique UserId
                user.UserId = Guid.NewGuid().ToString();

                // Create a new user node in the database
                var query = _graphClient.Cypher
                    .Create("(u:User {userId: $UserId, username: $Username, fullName: $FullName, email: $Email, passwordHash: $PasswordHash, profilePicture: $ProfilePicture, bio: $Bio, createdAt: $CreatedAt, isAdmin: $IsAdmin})")
                    .WithParam("UserId", user.UserId)
                    .WithParam("Username", user.Username)
                    .WithParam("FullName", user.FullName)
                    .WithParam("Email", user.Email)
                    .WithParam("PasswordHash", user.PasswordHash)
                    .WithParam("ProfilePicture", user.ProfilePicture)
                    .WithParam("Bio", user.Bio)
                    .WithParam("CreatedAt", user.CreatedAt)
                    .WithParam("IsAdmin", user.IsAdmin)
                    .Return<string>("u.UserId");

                await query.ExecuteWithoutResultsAsync();

                // Return a response with the created UserId
                return CreatedAtAction(nameof(CreateUser), new { id = user.UserId }, user);
            }
            catch (Exception ex)
            {
                // Handle any exceptions (like connection issues, etc.)
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }



        // GET: api/User/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            try
            {
                // Upit za traženje korisnika sa određenim UserId
                var query = _graphClient.Cypher
                    .Match("(u:User {userId: $userId})")  // Tražimo korisnika sa odgovarajućim UserId
                    .WithParam("userId", id)  // Prosleđujemo parametar sa UserId
                    .Return(u => u.As<User>())  // Vraćamo korisničke podatke
                    .ResultsAsync;

                // Izvršavamo upit
                var result = await query;
                var user = result.First();

                // Ako nije pronađen korisnik, vraćamo NotFound
                if (result == null || !result.Any())
                {
                    Console.WriteLine("No user found in the database");
                }
                else
                {
                    foreach (var u in result)
                    {
                        Console.WriteLine($"User found: {u.UserId} - {u.Username}");
                    }
                }

                // Vraćamo korisnika ako je pronađen
                return Ok(user);
            }
            catch (Exception ex)
            {
                // U slučaju greške, vraćamo 500 status sa greškom
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("/User/UserPage")]
        public async Task<IActionResult> UserPage()
        {
            try
            {
                // Dohvatanje korisničkog imena iz trenutne sesije (kako bi se prikazali podaci tog korisnika)
                var username = User.Identity.Name; // Pretpostavljamo da je username sačuvan u identitetu korisnika

                if (string.IsNullOrEmpty(username))
                {
                    return RedirectToAction("Login", "Account");
                }

                // Upit za pretragu korisnika na osnovu username-a
                var query = _graphClient.Cypher
                    .Match("(u:User {username: $username})")  // Tražimo korisnika sa određenim username
                    .WithParam("username", username)  // Prosleđujemo parametar sa username
                    .Return(u => u.As<User>())  // Vraćamo korisničke podatke
                    .ResultsAsync;

                // Izvršavamo upit
                var result = await query;
                if (!result.Any()) // Ako nema rezultata, ispisujemo grešku
                {
                    return NotFound("No user found with the given username.");
                }
                var user = result.FirstOrDefault();

                // Ako korisnik nije pronađen, vraćamo NotFound
                if (user == null)
                {
                    return NotFound("User not found");
                }

                var userId = user.UserId; // Pretpostavljamo da klasa User ima svojstvo UserId

                // Možete proslediti userId kao deo ViewData ili modela
                ViewData["UserId"] = userId;
                // Vraćamo korisničke podatke na view
                return View(user);
            }
            catch (Exception ex)
            {
                // U slučaju greške, vraćamo grešku
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        [HttpGet("/User/UserPage/{username}")]
        public async Task<IActionResult> UserPage(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                {
                    return BadRequest("Username is required.");
                }

                var query = _graphClient.Cypher
                    .Match("(u:User {username: $username})")
                    .WithParam("username", username)
                    .Return(u => u.As<User>())
                    .ResultsAsync;

                var result = await query;
                if (!result.Any())
                {
                    return NotFound("No user found with the given username.");
                }

                var user = result.FirstOrDefault();
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                return View(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("/User/Users/{username}")]
        public async Task<IActionResult> Users(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                {
                    return BadRequest("Username is required.");
                }

                var query = _graphClient.Cypher
     .Match("(u:User {username: $username})")
     .WithParam("username", username)
     .Return(u => new
     {
         UserId = u.As<User>().UserId,
         Username = u.As<User>().Username,
         FullName = u.As<User>().FullName,
         Bio = u.As<User>().Bio,
         ProfilePicture = u.As<User>().ProfilePicture
     })
     .ResultsAsync;

                var result = await query;
                if (!result.Any())
                {
                    return NotFound("No user found with the given username.");
                }

                var user = result.FirstOrDefault();
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                // Mapirajte vrednosti na model User
                var userModel = new User
                {
                    UserId = user.UserId, // Pretpostavka da je id u nekom numeričkom formatu
                    Username = user.Username,
                    FullName = user.FullName,
                    Bio = user.Bio,
                    ProfilePicture = user.ProfilePicture
                };

                return View(userModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        // DELETE: api/User/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                //string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Proverite da li korisnik postoji
                var userExists = await _graphClient.Cypher
                    .Match("(u:User {userId: $userId})")
                    .WithParam("userId", id)
                    .Return<int>("count(u)")  // Broj korisnika sa datim userId
                    .ResultsAsync;

                if (userExists.FirstOrDefault() == 0)
                {
                    return Json(new { success = false, error = "User not found" });
                }

                // Ako korisnik postoji, obrišite ga
                var query = _graphClient.Cypher
                    .Match("(u:User {userId: $userId})")
                    .WithParam("userId", id)
                    .Delete("u")  // Briše čvor korisnika
                    .ExecuteWithoutResultsAsync();

                await query;
                _ = HttpContext.SignOutAsync();
                HttpContext.Session.Clear();
                foreach (var cookie in Request.Cookies.Keys)
                {
                    Response.Cookies.Delete(cookie, new CookieOptions
                    {
                        Path = "/",
                        Domain = "https://localhost:7010", // Set to your app's domain
                    });
                }

                //return NoContent();  // Vraća 204 status kod kada je resurs uspešno obrisan
                // Return success response
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        // PUT: api/User/{id}
        [HttpPut("UpdateUser/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromForm] string bio, [FromForm] IFormFile profilePicture)
        {
            if (string.IsNullOrEmpty(bio))
            {
                return BadRequest("Bio is required.");
            }

            try
            {
                // Provera da li korisnik postoji u bazi
                var existingUser = await _graphClient.Cypher
                    .Match("(u:User {userId: $UserId})")
                    .WithParam("UserId", id)
                    .Return<int>("count(u)")
                    .ResultsAsync;

                if (existingUser.Single() == 0)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Spremanje slike, ako je poslata
                string profilePictureUrl = null;
                if (profilePicture != null)
                {
                    var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                    if (!Directory.Exists(uploadsDirectory))
                    {
                        Directory.CreateDirectory(uploadsDirectory);
                    }

                    var uniqueFileName = Guid.NewGuid() + Path.GetExtension(profilePicture.FileName);
                    var filePath = Path.Combine(uploadsDirectory, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await profilePicture.CopyToAsync(fileStream);
                    }

                    profilePictureUrl = $"/images/{uniqueFileName}";
                }

                // Ažuriranje korisnika u bazi podataka
                var query = _graphClient.Cypher
                    .Match("(u:User {userId: $UserId})")
                    .WithParam("UserId", id)
                    .Set("u.bio = $Bio" +
                         (profilePictureUrl != null ? ", u.profilePicture = $ProfilePicture" : ""))
                    .WithParam("Bio", bio)
                    .WithParam("ProfilePicture", profilePictureUrl)
                    .Return<string>("u.userId")
                    .ResultsAsync;

                var updatedUser = query.Result.FirstOrDefault();

                if (updatedUser == null)
                {
                    return StatusCode(500, new { message = "User update failed" });
                }

                return Ok(new { message = "User updated successfully", userId = updatedUser });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }



        // Action to handle adding a friend
        [HttpPost("AddFriend")]
        public async Task<IActionResult> AddFriend(string currentUserId, string friendUsername)
        {
            // Fetch the current user
            var currentUser = (await _graphClient.Cypher
                .Match("(u:User {userId: $UserId})")
                .WithParam("UserId", currentUserId)
                .Return(u => u.As<User>())
                .ResultsAsync)
                .FirstOrDefault();

            // Fetch the friend user
            var friend = (await _graphClient.Cypher
                .Match("(u:User {username: $Username})")
                .WithParam("Username", friendUsername)
                .Return(u => u.As<User>())
                .ResultsAsync)
                .FirstOrDefault();

            // Ensure both users exist
            if (currentUser == null || friend == null)
            {
                return BadRequest(new { success = false, message = "User not found." });
            }

            // Check if they are already friends
            var existingFriendship = await _graphClient.Cypher
                .Match("(u:User {userId: $currentUserId})-[r:FRIEND]->(f:User {userId: $friendId})")
                .WithParams(new { currentUserId = currentUser.UserId, friendId = friend.UserId })
                .Return(r => r.As<object>()) // Any relationship will do
                .ResultsAsync;

            if (existingFriendship.Any())
            {
                return BadRequest(new { success = false, message = "Users are already friends." });
            }

            // Create the friendship
            await _graphClient.Cypher
                .Match("(u:User {userId: $currentUserId}), (f:User {userId: $friendId})")
                .WithParams(new { currentUserId = currentUser.UserId, friendId = friend.UserId })
                .Merge("(u)-[:FRIEND]->(f)")
                .Merge("(f)-[:FRIEND]->(u)") // Bidirectional friendship
                .ExecuteWithoutResultsAsync();

            // Optionally update their in-memory `prijatelji` lists
            currentUser.prijatelji ??= new List<User>();
            friend.prijatelji ??= new List<User>();

            if (!currentUser.prijatelji.Any(p => p.UserId == friend.UserId))
                currentUser.prijatelji.Add(friend);

            if (!friend.prijatelji.Any(p => p.UserId == currentUser.UserId))
                friend.prijatelji.Add(currentUser);

            return Ok(new { success = true, message = "Friend added successfully." });
        }



        [HttpGet("SearchUsernames")]
        public async Task<IActionResult> SearchUsernames(string query)
        {
            var users = await _graphClient.Cypher
                .Match("(u:User)")
                .Where("toLower(u.username) CONTAINS toLower($query)")
                .WithParam("query", query)
                .Return(u => u.As<User>())
                .ResultsAsync;

            return Ok(users.Select(u => u.Username));
        }


        [HttpGet("AllUsers")]
        public async Task<IActionResult> GetAllUsersExceptLoggedIn(string loggedInUserId)
        {
            try
            {
                // Query to fetch all users except the logged-in user
                var query = _graphClient.Cypher
                    .Match("(u:User)")
                    .Where("u.userId <> $loggedInUserId")
                    .WithParam("loggedInUserId", loggedInUserId)
                    .Return(u => u.As<User>())
                    .ResultsAsync;

                var users = await query;

                // Return the list of users
                return Ok(users);
            }
            catch (Exception ex)
            {
                // Handle exceptions
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        [HttpGet("GetFriends")]
        public async Task<IActionResult> GetFriends(string userId)
        {
            try
            {
                // Query to get the friends of the specified user
                var query = _graphClient.Cypher
                    .Match("(u:User {userId: $userId})-[:FRIEND]->(f:User)")
                    .WithParam("userId", userId)
                    .Return(f => f.As<User>())
                    .ResultsAsync;

                var friends = await query;

                if (friends == null || !friends.Any())
                {
                    return Ok(new List<User>());
                }

                return Ok(friends);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("RemoveFriend")]
        public async Task<IActionResult> RemoveFriend(string currentUserId, string friendId)
        {
            try
            {
                Console.WriteLine($"currentUserId: {currentUserId}, friendId: {friendId}");

                var existingFriendship = await _graphClient.Cypher
                    .Match("(u:User {userId: $currentUserId})-[r:FRIEND]->(f:User {userId: $friendId})")
                    .WithParams(new { currentUserId, friendId })
                    .Return(r => r.As<object>())
                    .ResultsAsync;

                Console.WriteLine($"existingFriendship count: {existingFriendship.Count()}");


                if (!existingFriendship.Any())
                {
                    return BadRequest(new { success = false, message = "Friendship does not exist." });
                }

                // Remove the bidirectional FRIEND relationship
                await _graphClient.Cypher
                    .Match("(u:User {userId: $currentUserId})-[r:FRIEND]->(f:User {userId: $friendId})")
                    .WithParams(new { currentUserId, friendId })
                    .Delete("r")
                    .ExecuteWithoutResultsAsync();

                await _graphClient.Cypher
                    .Match("(f:User {userId: $friendId})-[r:FRIEND]->(u:User {userId: $currentUserId})")
                    .WithParams(new { currentUserId, friendId })
                    .Delete("r")
                    .ExecuteWithoutResultsAsync();

                return Ok(new { success = true, message = "Friend removed successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

    }
}
