using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DataLayer.Modules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Presentation.Helper;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly AppSettings _appSettings;

        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IOptions<AppSettings> appSettings)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _appSettings = appSettings.Value;
        }

        // Register Method

        [HttpPost("[action]")]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel RegData)
        {
            List<string> errorList = new List<string>();
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { Email = RegData.Email, UserName = RegData.Email, SecurityStamp = Guid.NewGuid().ToString() };

                var result = await _userManager.CreateAsync(user, RegData.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Users");
                    // sending Confirmation Email
                    return Ok(new { username = user.UserName, email = user.Email, status = 1, message = "Registration Successful" });
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                        errorList.Add(error.Description);
                    }
                }
            }
            return BadRequest(new JsonResult(errorList));
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Login([FromBody]LoginViewModel LoginData)
        {
            // Get the User from The database
            var user = await _userManager.FindByNameAsync(LoginData.Username);

            var roles = await _userManager.GetRolesAsync(user);

            var Key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_appSettings.Secret));

            var tokenExpiryTime = Convert.ToDouble(_appSettings.ExpireTime);

            if (user != null && await _userManager.CheckPasswordAsync(user, LoginData.Password))
            {
                //Confirmation of email 
                var tokenHandler = new JwtSecurityTokenHandler();

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new Claim[]
                    {
                       new Claim(JwtRegisteredClaimNames.Sub, LoginData.Username),
                       new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                       new Claim(ClaimTypes.NameIdentifier, user.Id),
                       new Claim(ClaimTypes.Role, roles.FirstOrDefault()),
                       new Claim("LoggedOn", DateTime.Now.ToString()),
                    }),
                    SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256Signature),
                    Issuer = _appSettings.Site,
                    Audience = _appSettings.Audience,
                    Expires = DateTime.UtcNow.AddMinutes(tokenExpiryTime)
                };
                //Generate Token
                var token = tokenHandler.CreateToken(tokenDescriptor);

                return Ok(new { token = tokenHandler.WriteToken(token), expiration = token.ValidTo, username = user.UserName, userRole = roles.FirstOrDefault() });
            }
            // return Error
            ModelState.AddModelError("", "UserName/Password was not found");
            return Unauthorized(new { LoginError = "Please Check the Login Credentials - Invalid Username/Password was Entered" });
        }           
    }
}