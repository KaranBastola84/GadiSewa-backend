using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.DTOs.Customers;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/staff/customers")]
[Authorize(Policy = "StaffOnly")]
public sealed class StaffCustomersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Vehicle> _vehicleRepository;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public StaffCustomersController(
        IUserRepository userRepository,
        IRepository<Customer> customerRepository,
        IRepository<Vehicle> vehicleRepository,
        IPasswordHasherService passwordHasherService,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _vehicleRepository = vehicleRepository;
        _passwordHasherService = passwordHasherService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerRegistrationResponseDto>>> CreateCustomer(
        [FromBody] CreateCustomerRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Vehicles is null || request.Vehicles.Count == 0)
            {
                return BadRequest(ApiResponse<CustomerRegistrationResponseDto>.Failure("At least one vehicle is required.", StatusCodes.Status400BadRequest));
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (existingUser is not null)
            {
                throw new ConflictException("A user with this email already exists.");
            }

            var normalizedVehicles = request.Vehicles
                .Select(vehicle => new
                {
                    Original = vehicle,
                    RegistrationNumber = vehicle.RegistrationNumber.Trim().ToUpperInvariant()
                })
                .ToList();

            var duplicateRegistration = normalizedVehicles
                .GroupBy(x => x.RegistrationNumber)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateRegistration is not null)
            {
                throw new ConflictException($"Duplicate vehicle registration number in request: {duplicateRegistration.Key}.");
            }

            foreach (var vehicle in normalizedVehicles)
            {
                var existingVehicle = await _vehicleRepository.ListAsync(x => x.RegistrationNumber == vehicle.RegistrationNumber, cancellationToken);
                if (existingVehicle.Count > 0)
                {
                    throw new ConflictException($"A vehicle with registration number {vehicle.RegistrationNumber} already exists.");
                }
            }

            var user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber.Trim(),
                PasswordHash = _passwordHasherService.HashPassword(request.Password),
                Role = UserRole.Customer,
                IsActive = true,
                IsEmailVerified = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow
            };

            var customer = new Customer
            {
                UserId = user.Id,
                Address = request.Address.Trim(),
                LoyaltyPoints = 0
            };

            var vehicles = normalizedVehicles.Select(vehicle => new Vehicle
            {
                CustomerId = customer.Id,
                RegistrationNumber = vehicle.RegistrationNumber,
                Make = vehicle.Original.Make.Trim(),
                Model = vehicle.Original.Model.Trim(),
                Year = vehicle.Original.Year,
                Mileage = vehicle.Original.Mileage,
                Color = vehicle.Original.Color.Trim()
            }).ToList();

            await _userRepository.AddAsync(user, cancellationToken);
            await _customerRepository.AddAsync(customer, cancellationToken);
            foreach (var vehicle in vehicles)
            {
                await _vehicleRepository.AddAsync(vehicle, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _emailService.SendWelcomeEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<CustomerRegistrationResponseDto>.Success(CustomerRegistrationResponseDto.FromEntities(user, customer, vehicles), StatusCodes.Status201Created));
        }
        catch (ConflictException ex)
        {
            return Conflict(ApiResponse<CustomerRegistrationResponseDto>.Failure(ex.Message, StatusCodes.Status409Conflict));
        }
    }
}