using app.Controllers;
using app.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
using System.Security.Claims;

public class AccountController : Controller
{
    private readonly IGraphClient _graphClient; // Pretpostavljamo da koristite Neo4j ili drugi DB klijent
    private readonly PostController _postController;
    public AccountController(IGraphClient graphClient, PostController postController)
    {
        _graphClient = graphClient;
        _postController = postController;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
    

        // Traženje korisnika u bazi na osnovu korisničkog imena
        var user = (await _graphClient.Cypher
                 .Match("(u:User {username: $Username})")
                 .WithParam("Username", username)
                 .Return(u => u.As<User>())
                 .ResultsAsync).FirstOrDefault();

        if (user == null)
        {
            // Korisnik nije pronađen
            ViewBag.Error = "Invalid username.";
            return View();
        }

        // Jednostavna validacija šifre (ako je u bazi u običnom tekstu)
        if (password != user.PasswordHash)
        {
            // Ako šifra ne odgovara
            ViewBag.Error = "Invalid password.";
            return View();
        } 

        // Kreiranje Claims za autentifikaciju
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Username), // Claim za korisničko ime
        new Claim(ClaimTypes.Email, user.Email)  ,  // Claim za email
          new Claim(ClaimTypes.NameIdentifier, user.UserId)
    };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Prijavljivanje korisnika
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToAction("Index", "Home"); // Nakon uspešne prijave, preusmeravanje na Home stranicu
    }


    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

       [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

    [HttpPost]
    public async Task<IActionResult> Register(string username, string fullName, string password, string email, IFormFile profilePicture, string bio)
    {
        // Check if the file is provided
        if (profilePicture == null || profilePicture.Length == 0)
        {
            ViewBag.Error = "Profile picture is required.";
            return View();
        }

        // Check if username already exists
        var existingUser = await _graphClient.Cypher
            .Match("(u:User {username: $Username})")
            .WithParam("Username", username)
            .Return<int>("count(u)")
            .ResultsAsync;

        if (existingUser.Single() > 0)
        {
            ViewBag.Error = "Username already exists.";
            return View();
        }

        // Save the file to wwwroot/images
        var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
        if (!Directory.Exists(uploadsDirectory))
        {
            Directory.CreateDirectory(uploadsDirectory);
        }

        var uniqueFileName = Guid.NewGuid() + Path.GetExtension(profilePicture.FileName);
        var filePath = Path.Combine(uploadsDirectory, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await profilePicture.CopyToAsync(stream);
        }

        // Create the new user
        var user = new User
        {
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            FullName = fullName,
            Email = email,
            PasswordHash = password,
            ProfilePicture = "/images/" + uniqueFileName, // Save the relative URL
            Bio = bio,
            CreatedAt = DateTime.UtcNow,
            IsAdmin = false
        };

        // Save user to Neo4j
        await _graphClient.Cypher
            .Create("(u:User {userId: $UserId, username: $Username, fullName: $FullName, email: $Email, passwordHash: $PasswordHash, profilePicture: $ProfilePicture, bio: $Bio, createdAt: $CreatedAt, isAdmin: $IsAdmin})")
            .WithParams(new
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                ProfilePicture = user.ProfilePicture,
                Bio = user.Bio,
                CreatedAt = user.CreatedAt,
                IsAdmin = user.IsAdmin
            })
            .ExecuteWithoutResultsAsync();

        return RedirectToAction("Login");
    }



}