using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.DTOs.Reviews;

public sealed class ReviewDto
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public Guid AppointmentId { get; init; }

    public string AppointmentNumber { get; init; } = string.Empty;

    public string VehicleRegistrationNumber { get; init; } = string.Empty;

    public int Rating { get; init; }

    public string Comment { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public static ReviewDto FromReview(Review review)
    {
        return new ReviewDto
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = $"{review.Customer.User.FirstName} {review.Customer.User.LastName}".Trim(),
            AppointmentId = review.AppointmentId,
            AppointmentNumber = review.Appointment.AppointmentNumber,
            VehicleRegistrationNumber = review.Appointment.Vehicle.RegistrationNumber,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
    }
}
