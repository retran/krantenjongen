namespace KrantenJongen.DTO;

public record RegionId(string Id)
{
    public static readonly RegionId Instance = new RegionId("europe-west1");

    public override string ToString() => Id;
}
