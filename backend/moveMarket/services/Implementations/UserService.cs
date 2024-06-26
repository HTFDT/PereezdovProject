﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using core.Dto.Auth;
using core.Dto.Categories;
using core.Dto.Kits;
using core.Dto.User;
using core.Extensions;
using core.Jwt;
using domain.Entities;
using domain.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using services.abstractions.Interfaces;
using services.Exceptions.Kits;
using services.Exceptions.User;

namespace services.Implementations;

internal class UserService(UserManager<ApplicationUser> manager,
        IRepository<ApplicationUser> repo,
        IRepository<Kit> kitsRepo,
        IMapper mapper,
        JwtOptions jwtOptions,
        IWebHostEnvironment env,
        IConfiguration config)
    : IUserService
{
    public async Task<UserResponse> GetByEmailAsync(string email)
    {
        email = email.ToUpper();
        var user = await manager.FindByEmailAsync(email) ?? throw new UserNotFoundException(email);
        return mapper.Map<ApplicationUser, UserResponse>(user);
    }

    public async Task<UserAddressResponse> GetByIdAsync(Guid id)
    {
        var user = await repo.GetByIdAsync(id) ?? throw new UserNotFoundException(id);
        return mapper.Map<ApplicationUser, UserAddressResponse>(user);
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var userEntity = mapper.Map<CreateUserRequest, ApplicationUser>(request);
        var refreshToken = GenerateRefreshToken();
        userEntity.RefreshToken = refreshToken;
        userEntity.RefreshTokenExpiryDate = DateTime.UtcNow.AddSeconds(jwtOptions.RefreshTokenLifetimeSeconds);
        var res = await manager.CreateAsync(userEntity, request.Password);
        if (!res.Succeeded)
            throw new CreateUserBadRequestException(res.ToString(), request);

        var accessToken = GenerateAccessToken(userEntity, jwtOptions);
        
        return new CreateUserResponse(userEntity.Id,
            userEntity.Email!,
            userEntity.DisplayName,
            accessToken,
            refreshToken,
            jwtOptions.AccessTokenLifetimeSeconds,
            jwtOptions.RefreshTokenLifetimeSeconds);
    }

    public async Task<UserAddressResponse> UpdateUserAsync(ClaimsPrincipal userPrincipal, UpdateUserRequest request)
    {
        if (!userPrincipal.TryGetUserId(out var userId))
            throw new InvalidOperationException("no claim in authorized user principal");
        var user = await manager.Users
                       .Include(u => u.Address)
                       .SingleOrDefaultAsync(u => u.Id == userId)
            ?? throw new UserNotFoundException(userId);
        user.DisplayName = request.UserName;
        if (request.AvatarImage is not null)
            user.AvatarPath = await request.AvatarImage.SaveFormFile(Path.Combine(env.ContentRootPath,
                config["ImagesDirPath"]!));
        var res = await manager.UpdateAsync(user);
        if (!res.Succeeded)
            throw new UpdateUserBadRequestException(res.ToString(), request);
        return mapper.Map<ApplicationUser, UserAddressResponse>(user);
    }

    public async Task ChangePasswordAsync(ClaimsPrincipal userPrincipal, ChangePasswordRequest request)
    {
        if (!userPrincipal.TryGetUserId(out var userId))
            throw new InvalidOperationException("no claim in authorized user principal");
        var user = await manager.Users.SingleOrDefaultAsync(u => u.Id == userId) 
                   ?? throw new UserNotFoundException(userId);
        var res = await manager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
        if (!res.Succeeded)
            throw new ChangeUserPasswordBadRequestException(res.ToString(), request);
    }

    public async Task<UserAddressResponse> SetAddressAsync(ClaimsPrincipal userPrincipal, SetAddressRequest request)
    {
        if (!userPrincipal.TryGetUserId(out var userId))
            throw new InvalidOperationException("no claim in authorized user principal");
        var user = await manager.Users.SingleOrDefaultAsync(u => u.Id == userId) 
                   ?? throw new UserNotFoundException(userId);
        await repo.LoadReference(user, u => u.Address);
        user.Address ??= new Address();
        var address = mapper.Map(request, user.Address);
        user.Address = address;
        var res = await manager.UpdateAsync(user);
        if (!res.Succeeded)
            throw new SetUserAddressBadRequestException(res.ToString(), request);
        return mapper.Map<ApplicationUser, UserAddressResponse>(user);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var userEntity = await manager.FindByEmailAsync(request.Login) 
                         ?? throw new UserNotFoundException(request.Login);
        var res = await manager.CheckPasswordAsync(userEntity, request.Password);
        if (!res)
            throw new UserUnauthorizedException(request);
        
        var accessToken = GenerateAccessToken(userEntity, jwtOptions);
        var refreshToken = GenerateRefreshToken();
        userEntity.RefreshToken = refreshToken;
        userEntity.RefreshTokenExpiryDate = DateTime.UtcNow.AddSeconds(jwtOptions.RefreshTokenLifetimeSeconds);
        await manager.UpdateAsync(userEntity);

        return new LoginResponse(userEntity.Id,
            userEntity.Email!,
            userEntity.DisplayName,
            accessToken,
            refreshToken,
            jwtOptions.AccessTokenLifetimeSeconds,
            jwtOptions.RefreshTokenLifetimeSeconds);
    }

    public async Task RevokeTokenAsync(ClaimsPrincipal userPrincipal)
    {
        if (!userPrincipal.TryGetUserId(out var userId))
            throw new InvalidOperationException("no claim in authorized user principal");
        var userEntity = await manager.Users.SingleOrDefaultAsync(u => u.Id == userId)
                         ?? throw new UserNotFoundException(userId);
        userEntity.RefreshToken = null;
        userEntity.RefreshTokenExpiryDate = null;
        await manager.UpdateAsync(userEntity);
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = GetPrincipalFromExpiredToken(request.AccessToken, jwtOptions);
        if (!principal.TryGetUserId(out var userId))
            throw new UserUnauthorizedException(request);
        var user = await manager.Users.SingleOrDefaultAsync(u => u.Id == userId)
                   ?? throw new UserNotFoundException(userId);

        if (!IsRefreshTokenValid(request.RefreshToken, user.RefreshToken, user.RefreshTokenExpiryDate))
            throw new UserUnauthorizedException(request);

        var accessToken = GenerateAccessToken(user, jwtOptions);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryDate = DateTime.UtcNow.AddSeconds(jwtOptions.RefreshTokenLifetimeSeconds);
        await manager.UpdateAsync(user);
        
        return new RefreshTokenResponse(user.Id,
            user.Email!,
            user.DisplayName,
            accessToken,
            refreshToken,
            jwtOptions.AccessTokenLifetimeSeconds,
            jwtOptions.RefreshTokenLifetimeSeconds);
    }

    public async Task<IEnumerable<UserFavoriteResponse>> GetUserFavoritesAsync(ClaimsPrincipal userPrincipal)
    {
        if (!userPrincipal.TryGetUserId(out var userId))
            throw new InvalidOperationException("no claim in authorized user principal");
        var user = await manager.Users.Include(u => u.Favorites)
                       .ThenInclude(f => f.Kit)
                       .ThenInclude(k => k.Category)
                       .SingleOrDefaultAsync(u => u.Id == userId)
                   ?? throw new UserNotFoundException(userId);
        return user.Favorites.Select(f => new UserFavoriteResponse(f.UserId,
            new KitResponse(f.Kit.Id,
                f.Kit.Name,
                f.Kit.Description,
                f.Kit.Discount,
                f.Kit.Popularity,
                Path.GetFileNameWithoutExtension(f.Kit.ImagePath),
                new CategoryResponse(f.Kit.Category.Id, 
                    f.Kit.Category.Name))));
    }

    public async Task<AddToFavoritesResponse> AddToFavoritesAsync(ClaimsPrincipal userPrincipal, AddToFavoritesRequest request)
    {
        if (!userPrincipal.TryGetUserId(out var userId))
            throw new InvalidOperationException("no claim in authorized user principal");
        var user = await manager.Users.Include(u => u.Favorites).
                       SingleOrDefaultAsync(u => u.Id == userId)
                   ?? throw new UserNotFoundException(userId);
        var kit = await kitsRepo.GetByIdAsync(request.KitId) ?? throw new KitNotFoundException(request.KitId);
        var newFav = new Favorite
        {
            UserId = user.Id,
            KitId = kit.Id
        };
        user.Favorites.Add(newFav);
        var res = await manager.UpdateAsync(user);
        if (!res.Succeeded)
            throw new AddToFavoriteBadRequestException(res.ToString());
        return new AddToFavoritesResponse(newFav.Id, newFav.UserId, newFav.KitId);
    }

    private static bool IsRefreshTokenValid(string refreshToken, string? currentToken, DateTime? expiryDate)
    {
        return currentToken is not null && refreshToken == currentToken
                                    && DateTime.UtcNow <= expiryDate;
    }
    
    private static ClaimsPrincipal GetPrincipalFromExpiredToken(string token, JwtOptions jwtOptions)
    {
        var secret = jwtOptions.Secret ?? throw new InvalidOperationException("Secret not configured");

        var validation = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateLifetime = false
        };

        return new JwtSecurityTokenHandler().ValidateToken(token, validation, out _);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];

        using var generator = RandomNumberGenerator.Create();

        generator.GetBytes(randomNumber);

        return Convert.ToBase64String(randomNumber);
    }

    private static string GenerateAccessToken(ApplicationUser userEntity, JwtOptions jwtOptions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwtOptions.Secret ?? throw new InvalidOperationException("Secret not configured")));

        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Name, userEntity.Email!),
            new(ClaimTypes.NameIdentifier, userEntity.Id.ToString())
        };
            
        var token = new JwtSecurityToken(
            expires: DateTime.UtcNow.AddSeconds(jwtOptions.AccessTokenLifetimeSeconds),
            claims: authClaims,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}