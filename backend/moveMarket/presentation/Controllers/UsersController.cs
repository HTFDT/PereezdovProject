﻿using core.Dto.Auth;
using core.Dto.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using services.abstractions.Interfaces;

namespace presentation.Controllers;

[ApiController]
[Route("api")]
public class UsersController(IUserService userService) : Controller
{
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType<UserAddressResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUser(Guid userId)
    {
        var response = await userService.GetByIdAsync(userId);
        return Ok(response);
    }
    
    [Authorize]
    [HttpPost("users/profile")]
    [ProducesResponseType<UserAddressResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser([FromForm] UpdateUserRequest request)
    {
        var response = await userService.UpdateUserAsync(User, request);
        return Ok(response);
    }
    
    [Authorize]
    [HttpPost("users/profile/address")]
    [ProducesResponseType<UserAddressResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetUserAddress([FromBody] SetAddressRequest request)
    {
        var response = await userService.SetAddressAsync(User, request);
        return Ok(response);
    }
    
    [HttpPost("auth/register")]
    [ProducesResponseType<CreateUserResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] CreateUserRequest request)
    {
        var response = await userService.CreateUserAsync(request);
        return Ok(response);
    }

    [HttpPost("auth/login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await userService.LoginAsync(request);
        return Ok(response);
    }
    
    [Authorize]
    [HttpPost("auth/password")]
    [ProducesResponseType<UserAddressResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeUserPassword([FromBody] ChangePasswordRequest request)
    {
        await userService.ChangePasswordAsync(User, request);
        return Ok();
    }
    
    [Authorize]
    [HttpPost("auth/refresh")]
    [ProducesResponseType<RefreshTokenResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var response = await userService.RefreshTokenAsync(request);
        return Ok(response);
    }
    
    [Authorize]
    [HttpPost("auth/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeToken()
    {
        await userService.RevokeTokenAsync(User);
        return Ok();
    }
    
    [Authorize]
    [HttpGet("users/favorites")]
    [ProducesResponseType<IEnumerable<UserFavoriteResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserFavorites()
    {
        var response = await userService.GetUserFavoritesAsync(User);
        return Ok(response);
    }
    
    [Authorize]
    [HttpPost("users/favorites")]
    [ProducesResponseType<AddToFavoritesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddToFavorites(AddToFavoritesRequest request)
    {
        var response = await userService.AddToFavoritesAsync(User, request);
        return Ok(response);
    }
}