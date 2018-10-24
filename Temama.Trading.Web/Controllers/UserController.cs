using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Temama.Trading.Web.Server;

namespace Temama.Trading.Web.Controllers
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        public class LoginCredentilas
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [AllowAnonymous]
        [HttpPost("[action]")]
        public async Task Login([FromBody]LoginCredentilas credentials)
        {
            var identity = GetIdentity(credentials.Username, credentials.Password);
            if (identity == null)
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Invalid username or password");
                return;
            }

            var now = DateTime.UtcNow;
            var jwt = new JwtSecurityToken(
                issuer: Security.Issuer,
                audience: Security.Audience,
                notBefore: now,
                claims: identity.Claims,
                expires: now.AddMinutes(Security.Lifetime),
                signingCredentials: new SigningCredentials(Security.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            var response = new
            {
                access_token = encodedJwt,
                username = identity.Name,
                role = identity.Claims.FirstOrDefault(c => c.Type == ClaimsIdentity.DefaultRoleClaimType).Value
            };

            await Response.WriteAsync(JsonConvert.SerializeObject(response, 
                new JsonSerializerSettings { Formatting = Formatting.Indented }));
        }

        private ClaimsIdentity GetIdentity(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return null;

            if (username.ToLower() != "foo")
                return null;

            var claims = new List<Claim>
            {
                new Claim(ClaimsIdentity.DefaultNameClaimType, username),
                new Claim(ClaimsIdentity.DefaultRoleClaimType, "admin")
            };

            return new ClaimsIdentity(claims, "Token", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
        }
    }
}