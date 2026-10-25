namespace ArtMarketplace.Domain.Modules.Catalog;

public sealed class Artist
{
    private Artist() { }
    public Artist(Guid id, string name, string slug, string? biography) { Id=id; Name=Require(name,200); Slug=Require(slug,160); Biography=biography?.Trim(); }
    public Guid Id { get; private set; } public string Name { get; private set; }=string.Empty; public string Slug { get; private set; }=string.Empty; public string? Biography { get; private set; }
    private static string Require(string v,int m){v=v.Trim();if(v.Length is 0||v.Length>m)throw new ArgumentException("Invalid value.");return v;}
}

public sealed class Category
{
    private Category() { }
    public Category(Guid id,string name,string slug,int displayOrder){Id=id;Name=Require(name,120);Slug=Require(slug,120);DisplayOrder=displayOrder;IsActive=true;}
    public Guid Id { get; private set; } public string Name { get; private set; }=string.Empty; public string Slug { get; private set; }=string.Empty; public bool IsActive { get; private set; } public int DisplayOrder { get; private set; }
    public void Deactivate()=>IsActive=false;
    private static string Require(string v,int m){v=v.Trim();if(v.Length is 0||v.Length>m)throw new ArgumentException("Invalid value.");return v;}
}
