using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using ClientService.EF;
using ClientService.Models;
using ClientService.Utils;
using WebApplication1.Utils;
using System.Numerics;
using Microsoft.VisualBasic;
using SendGrid.Helpers.Errors.Model;
using System.Reflection.Metadata;

namespace ClientService.Services
{
    public class ClinService
    {
        private readonly ApplicationDbContext _context;
        private readonly TokenService _tokenService;
        private readonly EncryptionUtils _encryptionUtils;
        private readonly HalykService _halykService;

        public ClinService(ApplicationDbContext context, TokenService tokenService, EncryptionUtils encryptionUtils = null, HalykService halykService = null)
        {
            _context = context;
            _tokenService = tokenService;
            _encryptionUtils = encryptionUtils;
            _halykService = halykService;
        }

        #region

        //public async Task<IActionResult> GetCodeAsync(string phone)
        //{
        //    // Проверить существует ли телефон
        //    var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        //    if (user != null)
        //    {

        //        return new BadRequestObjectResult("Phone number already exist");
        //    }
        //    var code = new Random().Next(100000, 999999).ToString();

        //    var cacheOptions = new DistributedCacheEntryOptions
        //    {
        //        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        //    };

        //    await _cache.SetStringAsync($"Code_{phone}", code, cacheOptions);
        //    return new OkResult();
        // очистить раз в день
        //}

        //public async Task<IActionResult> VerifyCodeAsync(CodeDTO codeDto)
        //{

        //    var cachedCode = await _cache.GetStringAsync($"Code_{codeDto.PhoneNumber}");
        //    if (cachedCode == codeDto.Code)
        //    {

        //        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        //        if (user != null)
        //        {
        //            return new BadRequestObjectResult("Phone number already exist");
        //        }

        //        var token = _tokenService.GenerateToken()

        //        // Сохранить токен в бд или редис

        //        return new OkObjectResult(new { Token = token });

        //    }

        //    return new UnauthorizedResult();

        //}

        //public async Task<IActionResult> RegistrationAsync(UserDTO userDto)
        //{
        //    var user = new User
        //    {
        //        PhoneNumber = userDto.PhoneNumber,
        //        HashPassword = userDto.Password
        //    };

        //    _context.Users.Add(user);
        //    await _context.SaveChangesAsync();
        //    return new OkResult();

        //}

        #endregion

        public async Task<string> LoginAsync(UserDTO userDto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == userDto.PhoneNumber);
            if (user == null) return "";

            if (!Hash.VerifyPassword(userDto.Password, user.HashPassword)) return "";
            return user.Id.ToString();
        }

        public async Task<IActionResult> SetMerchantAsync(HalykCredentialDTO halykCredentialDto, string userID)
        {
            
            Guid guid = Guid.Parse(userID);

            var halykCredential = await _context.HalykCredentials.FirstOrDefaultAsync(h => h.UserId == guid);
            if (halykCredential == null)
            {
                halykCredential = new HalykCredential
                {
                    UserId = guid,
                    Login = halykCredentialDto.Login,
                    EncriptPassword = _encryptionUtils.Encrypt(halykCredentialDto.Password, guid)
                };
                _context.HalykCredentials.Add(halykCredential);
            }
            else
            {
                halykCredential.Login = halykCredentialDto.Login;
                halykCredential.EncriptPassword = _encryptionUtils.Encrypt(halykCredentialDto.Password, guid);
            }

            await _context.SaveChangesAsync();
            return new OkResult();
        }

        public async Task<IActionResult> UpdateTableAsync(string userID)
        {
            UserSettingsDTO usersettingDTO = new UserSettingsDTO();

            Guid guid = Guid.Parse(userID);
            var halykCredential = await _context.HalykCredentials.FirstOrDefaultAsync(h => h.UserId == guid);
            var user = await _context.Users.FirstOrDefaultAsync(h => h.Id == guid);

            if (halykCredential == null) throw new ForbiddenException("Access denied");
            if (user == null) throw new ForbiddenException("Access denied");

            var products = await _halykService.GetSellerProductsAsync(halykCredential.Login, _encryptionUtils.Decrypt(halykCredential.EncriptPassword, guid), user.PhoneNumber, guid);

            usersettingDTO.MaxPersent = 0;
            usersettingDTO.settings = new List<Setting>();
            products.ForEach(x =>
            {
                usersettingDTO.settings.Add(x);
            });

            return new OkResult();
        }

        public async Task<UserSettingsDTO> GetTableAsync(string userId)
        {
            Guid guid = Guid.Parse(userId);
            UserSettingsDTO userSettingDTO = new UserSettingsDTO();

            var halCred = _context.HalykCredentials.FirstOrDefault(x => x.UserId == guid);
            if (halCred == null) throw new ForbiddenException();

            var interUser = _context.IntegratedUsers.FirstOrDefault(x => x.UserId == guid);

            userSettingDTO.MaxPersent = interUser == null ? 0 : interUser.MaxPersent;
            userSettingDTO.EnableDump = interUser != null;  
            userSettingDTO.settings = new List<Setting>();

            var settings = _context.UserSettings.Where(x => x.UserId == guid).ToList();

            settings.ForEach(x =>
            {
                userSettingDTO.settings.Add(Mapper.ToSetting(x));
            });

            return userSettingDTO;
        }

        public async Task<IActionResult> SetTableAsync(UserSettingsDTO userSettingsDto, string userId)
        {
            Guid guid = Guid.Parse(userId);
            IntegratedUsers interUser = await _context.IntegratedUsers.FirstOrDefaultAsync(x => x.UserId == guid);
            if (interUser != null)
            {
                interUser.MaxPersent = userSettingsDto.MaxPersent;
                await _context.SaveChangesAsync();
            }

            foreach (var item in userSettingsDto.settings)
            {
                var userSettings = await _context.UserSettings.FirstOrDefaultAsync(u => u.MerchantProductCode == item.MerchantProductCode);

                if (userSettings == null) _context.UserSettings.Add(Mapper.ToUserSettings(item, guid));
                else
                {
                    userSettings.IsDump = item.IsDump.HasValue ? item.IsDump.Value : false;
                    userSettings.MaxPrice = item.MaxPrice.HasValue ? item.MaxPrice.Value : 0d ;
                    userSettings.MinPrice = item.MinPrice.HasValue ? item.MinPrice.Value : 0d;
                }

            }
            await _context.SaveChangesAsync();
            return new OkResult();
        }

        public async Task<IActionResult> EnableDampAsync(double maxPrecent, string userUid)
        {
            Guid guid = Guid.Parse(userUid);
            var integratedUser = await _context.IntegratedUsers.FirstOrDefaultAsync(i => i.UserId == guid);
            if (integratedUser == null)
            {
                integratedUser = new IntegratedUsers
                {
                    UserId = guid,
                    MaxPersent = maxPrecent,
                    TimeFlag = DateTime.MinValue,
                    IsWorking = true
                };
                _context.IntegratedUsers.Add(integratedUser);
            }
            else
            {
                integratedUser.MaxPersent = maxPrecent;
                integratedUser.TimeFlag = DateTime.MinValue;
                integratedUser.IsWorking = false;
            }

            await _context.SaveChangesAsync();
            return new OkResult();
        }

        public async Task<IActionResult> DisableDampAsync(string userId)
        {
            Guid guid = Guid.Parse(userId);
            var integratedUser = await _context.IntegratedUsers.FirstOrDefaultAsync(i => i.UserId == guid);
            _context.IntegratedUsers.Remove(integratedUser);
            _context.SaveChanges();
            return new OkResult();
        }
    }
}
