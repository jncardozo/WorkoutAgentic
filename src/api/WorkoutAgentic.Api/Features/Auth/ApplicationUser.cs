using Microsoft.AspNetCore.Identity;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Features.Auth;

public sealed class ApplicationUser : IdentityUser
{
    public int StudentId { get; set; }
    public Alumno Student { get; set; } = null!;
}
