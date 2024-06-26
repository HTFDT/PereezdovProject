﻿using core.Entities.Base;

namespace domain.Entities;

public class Item : IEntity<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? NormalizedName { get; set; } = null!;
    public string? Description { get; set; }
    public double Price { get; set; }
    public string? ImagePath { get; set; }

    public ICollection<ItemProperty> ItemProperties { get; set; } = null!;
    public ICollection<KitItem> KitItems { get; set; } = null!;
}