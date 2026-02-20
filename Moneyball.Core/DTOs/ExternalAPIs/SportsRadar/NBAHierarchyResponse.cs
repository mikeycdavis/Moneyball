namespace Moneyball.Core.DTOs.ExternalAPIs.SportsRadar;

// Additional DTO for hierarchy response
public class NBAHierarchyResponse
{
    public List<NBAConference> Conferences { get; set; } = [];
}

public class NBAConference
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<NBADivision> Divisions { get; set; } = [];
}

public class NBADivision
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<NBATeamInfo> Teams { get; set; } = [];
}