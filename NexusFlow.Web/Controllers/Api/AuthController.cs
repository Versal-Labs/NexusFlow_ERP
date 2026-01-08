using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.DTOs.Auth;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Identity;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // 1. Find User
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            // 2. Check Password
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
            {
                return Unauthorized("Invalid email or password.");
            }

            // 3. Generate Token
            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateToken(user.Id, user.Email!, roles);

            return Ok(new LoginResponse
            {
                Token = token,
                Email = user.Email!,
                Roles = roles
            });
        }
    }
}
