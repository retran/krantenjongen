namespace KrantenJongen.DTO;

public record ProjectId(string Id)
{
    public static readonly ProjectId Instance = new ProjectId("krantenjongen");

    public override string ToString() => Id;
}
