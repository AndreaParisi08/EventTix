using FluentValidation;

namespace EventTix.Booking.Application.Commands.ReserveSeat;

public sealed class ReserveSeatCommandValidator : AbstractValidator<ReserveSeatCommand>
{
    public ReserveSeatCommandValidator()
    {
        RuleFor(x => x.SeatId)
            .NotEmpty().WithMessage("Seat ID is required.")
            .MaximumLength(20).WithMessage("Seat ID cannot exceed 20 characters.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Ticket price must be greater than zero.");
    }
}