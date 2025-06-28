using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ClientService.Utils;
using ClientService.Models;
using WebApplication1.Utils;
using ClientService.Services;
using SendGrid.Helpers.Errors.Model;

namespace ClientService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientServiceController : ControllerBase
    {
        private readonly ClinService _clientService;
        private readonly TokenService _tokenService;
        public ClientServiceController(ClinService clientService, TokenService tokenService)
        {
            _clientService = clientService;
            _tokenService = tokenService;
        }


        #region
        //[HttpPost("GetCode")]
        //public async Task<IActionResult> GetCode([FromBody] string phone)
        //{
        //    return await _clientService.GetCodeAsync(phone);
        //}

        //[HttpPost("VerifyCode")]
        //public async Task<IActionResult> VerifyCode([FromBody] CodeDTO codeDto)
        //{
        //    return await _clientService.VerifyCodeAsync(codeDto);
        //}

        //[HttpPost("Registration")]
        //public async Task<IActionResult> Registration([FromBody] UserDTO userDto)
        //{
        //    return await _clientService.RegistrationAsync(userDto);
        //}

        #endregion

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] UserDTO userDto)
        {
            string id = await _clientService.LoginAsync(userDto);
            if (string.IsNullOrEmpty(id)) return StatusCode(401, "Номер не зарегистрирован");
            var token = _tokenService.GenerateToken(id.ToString(), "User");
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.Now.AddMinutes(30)
            };
            Response.Cookies.Append("AuthToken", token, cookieOptions);
            return Ok();
        }
        


        [HttpPost("SetMerchant")]
        public async Task<IActionResult> SetMerchant([FromBody] HalykCredentialDTO halykCredentialDto)
        {
            var token = Request.Cookies["AuthToken"];
            if (string.IsNullOrEmpty(token)) return Unauthorized("Token not found");

            var userId = _tokenService.ValidateToken(token);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            return await _clientService.SetMerchantAsync(halykCredentialDto, userId);
        }

        [HttpPost("SetTalegramId")]
        public async Task<IActionResult> SetTelegramId([FromBody] UserDTO userDto)
        {
            var token = Request.Cookies["AuthToken"];
            if (string.IsNullOrEmpty(token)) return Unauthorized("Token not found");

            var userId = _tokenService.ValidateToken(token);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            return Ok();
        }


        [HttpGet("UpdateTable")]
        public async Task<IActionResult> UpdateTable()
        {
            var token = Request.Cookies["AuthToken"];
            if (string.IsNullOrEmpty(token)) return Unauthorized("Token not found");

            var userId = _tokenService.ValidateToken(token);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            return await _clientService.UpdateTableAsync(userId);
        }



        [HttpGet("GetTable")]
        public async Task<IActionResult> GetTable()
        {
            try
            {
                var token = Request.Cookies["AuthToken"];
                if (string.IsNullOrEmpty(token)) return Unauthorized("Token not found");

                var userId = _tokenService.ValidateToken(token);
                if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

                var userSetting = await _clientService.GetTableAsync(userId);
                return Ok(userSetting);
            }
            catch (ForbiddenException)
            {
                return StatusCode(403, "У вас нет данных маркета");
            }
        }



        [HttpPost("SetTable")]
        public async Task<IActionResult> SetTable([FromBody] UserSettingsDTO userSettingsDto)
        {
            var token = Request.Cookies["AuthToken"];
            if (string.IsNullOrEmpty(token)) return Unauthorized("Token not found");

            var userId = _tokenService.ValidateToken(token);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            return await _clientService.SetTableAsync(userSettingsDto, userId);
        }



        [HttpPatch("EnableDamp")]
        public async Task<IActionResult> EnableDamp([FromBody] double maxPrecent)
        {
            var token = Request.Cookies["AuthToken"];
            if (string.IsNullOrEmpty(token)) return Unauthorized("Token not found");

            var userId = _tokenService.ValidateToken(token);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            return await _clientService.EnableDampAsync(maxPrecent, userId);
        }

        [HttpPatch("DisableDamp")]
        public async Task<IActionResult> DisableDamp()
        {
            var token = Request.Cookies["AuthToken"];
            if (string.IsNullOrEmpty(token)) return Unauthorized("Token not found");

            var userId = _tokenService.ValidateToken(token);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            return await _clientService.DisableDampAsync(userId);

        }



    }

}
