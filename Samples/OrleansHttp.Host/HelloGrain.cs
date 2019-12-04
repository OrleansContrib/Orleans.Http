using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Orleans;
using Orleans.Runtime;
using OrleansHttp.Grains;

namespace OrleansHttp.Host
{
    public class HelloGrain : Grain, IHelloGrain
    {
        private readonly IHttpContextAccessor _httpConntextAcessor;

        public HelloGrain(IHttpContextAccessor acessor)
        {
            this._httpConntextAcessor = acessor;
        }

        public Task<string> Hello(string name)
        {
            return Task.FromResult($"Hello, {name}!");
        }

        public Task<string> SimpleHello(string name)
        {
            return this.Hello(name);
        }

        public Task<string> GetToken(bool admin)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Startup.SECRET);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, "TestUser"),
                    new Claim(ClaimTypes.Role, admin ? "admin" : "user")
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return Task.FromResult(tokenHandler.WriteToken(token));
        }

        public async Task<SiloInfo[]> GetSilos()
        {
            return (await this.GrainFactory.GetGrain<IManagementGrain>(0).GetHosts()).Select(s => new SiloInfo { Silo = s.Key.ToString(), Status = s.Value }).ToArray();
        }

        public Task<UserInfo> GetUserInfo()
        {
            return Task.FromResult(new UserInfo
            {
                User = this._httpConntextAcessor.HttpContext.User.FindFirst(ClaimTypes.Name).Value,
                Roles = this._httpConntextAcessor.HttpContext.User.FindFirst(ClaimTypes.Role).Value
            });
        }
    }
}
