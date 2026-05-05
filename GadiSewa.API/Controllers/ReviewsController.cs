using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Reviews;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
public sealed class ReviewsController : ControllerBase
{
    private readonly IRepository<Review> _reviewRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewsController(
        IRepository<Review> reviewRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository;
        _appointmentRepository = appointmentRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("uid")?.Value;
        return Guid.Parse(userIdClaim!);
    }

    private async Task<Customer?> GetCurrentCustomerAsync(CancellationToken cancellationToken)
    {
        return await _customerRepository.Query()
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == GetCurrentUserId(), cancellationToken);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReviewDto>>>> GetReviews(CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<Review> query = _reviewRepository.Query()
                .AsNoTracking()
                .Include(x => x.Customer)
                    .ThenInclude(x => x.User)
                .Include(x => x.Appointment)
                    .ThenInclude(x => x.Vehicle);

            if (!User.IsInRole(UserRole.Admin.ToString()) && !User.IsInRole(UserRole.Staff.ToString()))
            {
                var customer = await GetCurrentCustomerAsync(cancellationToken);
                if (customer is null)
                {
                    return NotFound(ApiResponse<IReadOnlyList<ReviewDto>>.Failure("Customer profile not found.", StatusCodes.Status404NotFound));
                }

                query = query.Where(x => x.CustomerId == customer.Id);
            }

            var reviews = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return Ok(ApiResponse<IReadOnlyList<ReviewDto>>.Success(reviews.Select(ReviewDto.FromReview).ToList()));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<IReadOnlyList<ReviewDto>>.Failure($"Error retrieving reviews: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> GetReview(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var review = await _reviewRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Include(x => x.Customer)
                    .ThenInclude(x => x.User)
                .Include(x => x.Appointment)
                    .ThenInclude(x => x.Vehicle)
                .FirstOrDefaultAsync(cancellationToken);

            if (review is null)
            {
                return NotFound(ApiResponse<ReviewDto>.Failure("Review not found.", StatusCodes.Status404NotFound));
            }

            if (!User.IsInRole(UserRole.Admin.ToString()) && !User.IsInRole(UserRole.Staff.ToString()))
            {
                var customer = await GetCurrentCustomerAsync(cancellationToken);
                if (customer is null || customer.Id != review.CustomerId)
                {
                    return Forbid();
                }
            }

            return Ok(ApiResponse<ReviewDto>.Success(ReviewDto.FromReview(review)));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<ReviewDto>.Failure($"Error retrieving review: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpGet("appointment/{appointmentId:guid}")]
    public async Task<ActionResult<ApiResponse<ReviewDto?>>> GetReviewByAppointmentId(Guid appointmentId, CancellationToken cancellationToken)
    {
        try
        {
            var review = await _reviewRepository.Query()
                .AsNoTracking()
                .Where(x => x.AppointmentId == appointmentId)
                .Include(x => x.Customer)
                    .ThenInclude(x => x.User)
                .Include(x => x.Appointment)
                    .ThenInclude(x => x.Vehicle)
                .FirstOrDefaultAsync(cancellationToken);

            if (review is null)
            {
                return Ok(ApiResponse<ReviewDto?>.Success(null));
            }

            if (!User.IsInRole(UserRole.Admin.ToString()) && !User.IsInRole(UserRole.Staff.ToString()))
            {
                var customer = await GetCurrentCustomerAsync(cancellationToken);
                if (customer is null || customer.Id != review.CustomerId)
                {
                    return Forbid();
                }
            }

            return Ok(ApiResponse<ReviewDto?>.Success(ReviewDto.FromReview(review)));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<ReviewDto?>.Failure($"Error retrieving review: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPost]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> CreateReview(
        [FromBody] CreateReviewRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await GetCurrentCustomerAsync(cancellationToken);
            if (customer is null)
            {
                return NotFound(ApiResponse<ReviewDto>.Failure("Customer profile not found.", StatusCodes.Status404NotFound));
            }

            var appointment = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == request.AppointmentId)
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(cancellationToken);

            if (appointment is null)
            {
                return NotFound(ApiResponse<ReviewDto>.Failure("Appointment not found.", StatusCodes.Status404NotFound));
            }

            if (appointment.CustomerId != customer.Id)
            {
                return BadRequest(ApiResponse<ReviewDto>.Failure("You can only review your own appointments.", StatusCodes.Status400BadRequest));
            }

            if (appointment.Status != AppointmentStatus.Completed)
            {
                return BadRequest(ApiResponse<ReviewDto>.Failure("You can only review completed appointments.", StatusCodes.Status400BadRequest));
            }

            var existingReview = await _reviewRepository.ListAsync(x => x.CustomerId == customer.Id && x.AppointmentId == request.AppointmentId, cancellationToken);
            if (existingReview.Count > 0)
            {
                return Conflict(ApiResponse<ReviewDto>.Failure("A review for this appointment already exists.", StatusCodes.Status409Conflict));
            }

            var review = new Review
            {
                CustomerId = customer.Id,
                AppointmentId = request.AppointmentId,
                Rating = request.Rating,
                Comment = request.Comment.Trim()
            };

            await _reviewRepository.AddAsync(review, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var created = await _reviewRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == review.Id)
                .Include(x => x.Customer)
                    .ThenInclude(x => x.User)
                .Include(x => x.Appointment)
                    .ThenInclude(x => x.Vehicle)
                .FirstOrDefaultAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ReviewDto>.Success(ReviewDto.FromReview(created!), StatusCodes.Status201Created));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<ReviewDto>.Failure($"Error creating review: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> UpdateReview(
        Guid id,
        [FromBody] UpdateReviewRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var review = await _reviewRepository.GetByIdAsync(id, cancellationToken);
            if (review is null)
            {
                return NotFound(ApiResponse<ReviewDto>.Failure("Review not found.", StatusCodes.Status404NotFound));
            }

            var customer = await GetCurrentCustomerAsync(cancellationToken);
            if (customer is null || customer.Id != review.CustomerId)
            {
                return Forbid();
            }

            review.Rating = request.Rating;
            review.Comment = request.Comment.Trim();
            review.UpdatedAt = DateTimeOffset.UtcNow;

            _reviewRepository.Update(review);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var updated = await _reviewRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Include(x => x.Customer)
                    .ThenInclude(x => x.User)
                .Include(x => x.Appointment)
                    .ThenInclude(x => x.Vehicle)
                .FirstOrDefaultAsync(cancellationToken);

            return Ok(ApiResponse<ReviewDto>.Success(ReviewDto.FromReview(updated!)));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<ReviewDto>.Failure($"Error updating review: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var review = await _reviewRepository.GetByIdAsync(id, cancellationToken);
            if (review is null)
            {
                return NotFound(ApiResponse<object?>.Failure("Review not found.", StatusCodes.Status404NotFound));
            }

            var customer = await GetCurrentCustomerAsync(cancellationToken);
            if (customer is null || customer.Id != review.CustomerId)
            {
                return Forbid();
            }

            _reviewRepository.Remove(review);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(ApiResponse<object?>.Success(null));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object?>.Failure($"Error deleting review: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }
}
