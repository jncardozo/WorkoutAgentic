namespace WorkoutAgentic.Api.Features.Legal;

public sealed class AgreementAcceptance
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Version { get; set; } = "";
    public string DocumentHash { get; set; } = "";
    public string DocumentText { get; set; } = "";
    public string ParticipantName { get; set; } = "";
    public string ParticipantEmail { get; set; } = "";
    public DateTime AcceptedAtUtc { get; set; }
}
